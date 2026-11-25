# Research: Writing telemetries

**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-11-26

Numbering continues from spec 003 (R32-R43). Each decision is a proposal until the developer
confirms it (C5).

## R44 - The common options base

- **Decision**: An abstract `TelemetrySerializerOptions` holds what both directions share: the
  telemetry types (`Telemetries`), the converters (`Converters`, plus the format's
  `FormatConverters`), locking (`MakeReadOnly`, `IsReadOnly`) and the type and property
  definitions built when the options lock. `DeserializeOptions` derives from it and keeps what is
  reading-only: policies, the deserialization context limits, the enumerator factories and the
  byte order mark. `SerializeOptions` derives from it and adds what is writing-only: the object
  converter factory a format supplies, and `DefaultCollectionOptions` (FR-026).
- **Rationale**: The clarification (FR-009): deserializing goes from a format to telemetries,
  serializing from telemetries to a format; definitions and converters are what they share.
  `TypeDefinition.Create` and `PropertyDefinition.Create` today take `DeserializeOptions`; they
  move to the base, so the flat rule (R32) and converter resolution (R33) are enforced by the same
  code both ways. The name mirrors `JsonSerializerOptions` next to `TelemetrySerializer`.
- **Policies**: stay on `DeserializeOptions`. Definition policies choose members while reading;
  writing writes every member the type definition maps. Member choice for writing is not needed
  for the milestone and is left out (a later spec can add it to the base).
- **Alternatives considered**: two independent options built by shared internal helpers (rejected
  in clarification); one options type for both directions (rejected in #142).

## R45 - The converter write signature

- **Decision**: `Converter<T>` gets `public abstract T Read(ReadOnlySpan<byte> utf8)` (renamed from
  `Convert`) and `public virtual void Write(T value, IBufferWriter<byte> writer)`. The default
  `Write` throws `NotSupportedException` naming the converter; every built-in converter overrides
  it. `Write` is never called with a null value (FR-012: the format writes null).
- **Rationale**: Mirrors `Read` and `JsonConverter<T>.Write`. `IBufferWriter<byte>` is a BCL type,
  so no format type enters the core (C0). A converter asks for room with `GetSpan(sizeHint)` and
  calls `Advance`; when the hint was too small it asks again with a larger hint, so there is no
  "did not fit" result (FR-002). A virtual default keeps third-party reading-only converters
  compiling; definitions built by `SerializeOptions` check at lock that every resolved converter
  overrides `Write` (S1, FR-008), so the `NotSupportedException` is never reached at write time.
- **Built-ins**: `Utf8SpanParsableConverter<T>` writes through `IUtf8SpanFormattable.TryFormat`
  with the invariant culture, growing the hint on false. `StringConverter` transcodes with
  `Encoding.UTF8.GetMaxByteCount`. `BooleanConverter` writes `true`/`false`. `DateTimeConverter`
  and `DateTimeOffsetConverter` write round-trip ("O") format. `NullableConverter<T>` writes the
  inner value.
- **Alternatives considered**: `TryWrite(T, Span<byte>, out int)` (rejected in #137: the retry
  contract, and `TryWrite` in System.Text.Json means resumable collections); an abstract `Write`
  (breaks every reading-only converter).

## R46 - Composed getters

- **Decision**: `PropertyDefinition<T>` compiles a getter, `Func<Telemetry, T>`, next to the
  existing compiled setter (`CreateSetter` uses expression trees today), and exposes an internal
  `WriteValue(Telemetry, IBufferWriter<byte>, ...)` that gets the value and calls the resolved
  converter's `Write`, so the call stays generic from telemetry to `T` to `Write` (FR-006). A null
  value (a nullable or a `string`) is reported to the format's object converter instead of being
  written (R50).
- **Rationale**: Same mechanism as the setter, compiled once per definition, no boxing.
- **Alternatives considered**: `PropertyInfo.GetValue` (boxes, slow); source generators (not yet
  used anywhere in the SDK).

## R47 - SerializeAsync return types

- **Decision**: `ValueTask` for the per-telemetry `SerializeAsync`, `Task` for the
  `IAsyncEnumerable<Telemetry>` overload.
- **Rationale**: The per-telemetry overload is called once per telemetry in a loop; most of its
  awaits complete synchronously (the per-call buffer fits, a `MemoryStream` or a buffered stream
  accepts the write, a `PipeWriter` does not ask for backpressure), so `ValueTask` avoids a `Task`
  allocation per telemetry. `Stream.WriteAsync(ReadOnlyMemory<byte>)` and `PipeWriter.FlushAsync`
  already return value tasks. The many-telemetries overload runs once per output, where one `Task`
  does not matter and `Task` has none of `ValueTask`'s rules (await once, never concurrently, no
  `.Result` before completion). The XML documentation states those rules on the per-telemetry
  overload.
- **Alternatives considered**: `Task` for both until measured (the spec's fallback, C6). Kept as
  the fallback if the developer prefers one return type.

## R48 - Targets and the per-call buffer

- **Decision**:
  - `Stream` targets: each call rents an internal pooled buffer writer (`ArrayPool<byte>.Shared`
    segments behind an `IBufferWriter<byte>`), gives it to the object converter, and writes the
    written span to the `Stream` after each telemetry (`Write`, or `WriteAsync` on the async
    path), then returns the buffer to the pool. Many-telemetries overloads reuse one buffer for
    the whole call and write when it passes a threshold (default 16 KiB) and at the end.
  - `PipeWriter` targets: the `PipeWriter` itself is the `IBufferWriter<byte>`; after each
    telemetry the call `FlushAsync`es (many-telemetries: when unflushed bytes pass the threshold,
    and at the end), honoring backpressure.
  - Neither path disposes a `Stream` or completes a `PipeWriter` (FR-014); both flush what they
    wrote before returning, so a caller can read immediately.
  - The async path only calls `WriteAsync`/`FlushAsync` (FR-013).
- **Rationale**: `Utf8JsonWriter` over a `Stream` uses an internal pooled buffer the same way; a
  `PipeWriter` needs none (no copy). The buffer lives only for the call (FR-015, FR-016, C8).
- **Alternatives considered**: `ArrayBufferWriter<byte>` (not pooled; allocates per call);
  wrapping a `Stream` in `PipeWriter.Create` (adds a pipe's machinery for a one-call buffer).

## R49 - Object converters and how a format plugs in

- **Decision**: An abstract `ObjectConverter` in the core, created per type definition by the
  format through `SerializeOptions.CreateObjectConverter(TypeDefinition)` (abstract on
  `SerializeOptions`, as `CreateTelemetryEnumerator` is abstract on `DeserializeOptions`). It has
  `WriteTelemetry(Telemetry, IBufferWriter<byte>)`, and a format overrides `WriteStart`,
  `WriteMember(PropertyDefinition, ...)`, `WriteNull(PropertyDefinition, ...)` and `WriteEnd` (or
  the format writes the whole telemetry itself). The core walks the property definitions in order
  and calls the format for each, using the composed getter and the resolved converter (R46).
- **Rationale**: The format owns escaping, nulls and framing (C0); the core owns walking
  definitions, getters and converters. Mirrors the reading side, where the core owns definitions
  and the format supplies the resumable step.
- **Escaping**: a format that must escape (JSON) gives the converter a scratch buffer writer and
  copies escaped bytes into the target; the string test format does not escape.
- **Alternatives considered**: the format writes everything, including member lookup (duplicates
  the definition walk in every format).

## R50 - Nulls

- **Decision**: For a member whose value is null (`string` null, nullable without a value), the
  core calls the format's `WriteNull` instead of the converter. The string test format writes an
  empty value; JSON Lines (#138) decides between `null` and omitting.
- **Rationale**: FR-012 and spec edge case: converters never see a missing value.
- **Per-kind framing belongs to the format** (developer, 2026-11-26): `Utf8JsonWriter` frames each
  kind of value differently, in partial classes: `WriteStringValue` (quote and escape),
  `WriteNumberValue`/`WriteBooleanValue` (bare), `WriteNullValue`, `WriteRawValue` (bytes as
  they are). Here `WriteNull` is the `WriteNullValue` counterpart, and `WriteMember` is where a
  format decides string or raw: it receives the `PropertyDefinition`, whose `MemberType` tells it
  (e.g. in JSON, `string`, enums, `Guid` and dates are quoted). A per-kind writer in the core
  would be a format-neutral token model (C0) and every format frames kinds differently (JSON
  quotes, Parquet writes typed binary), so the core keeps only `WriteMember` and `WriteNull`. The
  partial-class-per-kind writer is a good fit inside `Forestry.Serialization.JsonLines` (#138),
  possibly `Utf8JsonWriter` itself over the converters' bytes. The string test format does not
  escape, so it needs none of this.

## R51 - Collection writer contract

- **Decision**: A public interface `ICollectionWriter` with `MonitoringCollectionOptions Options`,
  `long Sequence`, `long BytesWritten`, `bool IsFull`, and an internal hook the serializer calls,
  `void OnTelemetryWritten()`, exposed through an internal interface method (explicit
  implementation) so callers cannot bump the sequence. Two public, non-sealed classes implement
  it:
  - `MonitoringCollectionStream : Stream`: over an inner `Stream`; counts in `Write`/`WriteAsync`
    overrides; `Flush`/`FlushAsync` forward; `CanRead`/`CanSeek` false.
  - `MonitoringCollectionPipeWriter : PipeWriter`: decorates an inner `PipeWriter`; forwards
    `GetSpan`, `GetMemory`, `FlushAsync`, `CancelPendingFlush`, `CanGetUnflushedBytes`,
    `UnflushedBytes`; counts in `Advance`. A static `Create(Stream, MonitoringCollectionOptions)`
    builds the inner writer with `PipeWriter.Create(stream, new StreamPipeWriterOptions(pool,
    minimumBufferSize, leaveOpen))`, since the BCL's `StreamPipeWriter` is internal and sealed.
  - Both: a constructor subclasses chain to (`base(inner, options)`); `Complete(Exception?)` /
    `CompleteAsync(Exception?)` gets every byte to the inner target, then disposes or completes
    the inner target unless `LeaveOpen`, then calls `protected virtual void
    OnCompleted(Exception? exception)` (null: ready; otherwise abandoned) (FR-022, FR-023).
    `Dispose`/`DisposeAsync` on the stream completes without an exception if not yet completed.
- **Names**: `MonitoringCollectionStream` and `MonitoringCollectionPipeWriter` follow
  `MonitoringCollectionOptions` (clarification). To be confirmed with the developer.
- **Rationale**: An interface lets `TelemetrySerializer` recognize both by type
  (`utf8 is ICollectionWriter`) while each derives from its own BCL base (C# has no multiple
  inheritance). Public and non-sealed so monitoring formatters override them (FR-018).
- **Alternatives considered**: one abstract base (impossible across `Stream` and `PipeWriter`);
  `Sequence` bumped in `Flush` (a flush is not a telemetry boundary on every path).

## R52 - Sequence at telemetry boundaries

- **Decision**: After the object converter has written a whole telemetry and its bytes have been
  handed to the target (written to the `Stream`, or advanced in the `PipeWriter`),
  `TelemetrySerializer` calls `OnTelemetryWritten()` when the target is an `ICollectionWriter`.
  `IsFull` is evaluated against `Sequence` and `BytesWritten` only then (FR-019, FR-021).
- **Rationale**: Converters write several buffers per telemetry, so only the serializer knows the
  boundary. For a `Stream` target with a many-telemetries buffer, the serializer writes the buffer
  to a collection stream after every telemetry (not at the threshold), so `BytesWritten` is exact
  at each boundary.

## R53 - MonitoringCollectionOptions

- **Decision**: A sealed class with `init` properties, immutable once created:
  - `bool LeaveOpen` (default false)
  - `long? TargetSize` (bytes, default null: no limit)
  - `long? MaxTelemetries` (default null)
  - `bool SizeIsCompressed` (default false: size counts the bytes the collection writer sees; a
    compressing inner target is a later format concern)
  - `int MinimumBufferSize` (default 4096) and `MemoryPool<byte>? Pool` (default null: shared),
    used only by `MonitoringCollectionPipeWriter.Create(Stream, …)`
  - `static MonitoringCollectionOptions Default`
  - Validation: `TargetSize` and `MaxTelemetries` positive when set; `MinimumBufferSize` positive.
- `SerializeOptions.DefaultCollectionOptions` is a `MonitoringCollectionOptions` (default
  `MonitoringCollectionOptions.Default`), settable until the options lock. Collection writers
  created through an overload that takes `SerializeOptions` and no collection options use it.
- **Rationale**: FR-024 to FR-026; `init` like `StreamPipeWriterOptions`'s read-only properties.
- **Open with the developer**: whether `SizeIsCompressed` is needed in this spec at all, since no
  format compresses yet (could wait for #138's gzip option).

## R54 - The string test format

- **Decision**: In `Forestry.Serialization.Tests`: `TestingSerializeOptions : SerializeOptions`
  whose object converter writes a telemetry as its members' converter output joined by `;`, one
  telemetry per line (`\n`), nulls as an empty value, no escaping. Tests serialize into a
  `MemoryStream` (rewound, read with `new StreamReader(memory, leaveOpen: true).ReadToEnd()`) and
  over a `System.IO.Pipelines.Pipe` (reading its `Reader`), and through both collection writers.
- **Rationale**: C3 (4.0.0): writing is proven in this package through a string format over a
  `Stream`, without imitating a reader. Lives in tests, not the public API (spec assumption).

## R55 - Where the C8 memory test lives

- **Decision**: In `Forestry.Serialization.Xml.Tests`, next to the existing memory tests
  (`MemoryTests`, which measure heap growth for reading): a `foreach` over
  `TelemetrySerializer.Deserialize` of a generated document serializes each telemetry into one
  stream that discards bytes but counts them (a collection stream over `Stream.Null`), comparing
  heap growth for N and 10×N telemetries and asserting the sequence (FR-028, SC-005). It needs the
  string test format, so that fixture is shared as a linked test source file (or a small
  test-only helper) between the two test projects.
- **Rationale**: Reading a document needs a real format package (C3 for reading); the XML package's
  tests already reference both packages. The measurement pattern already exists there.

## R56 - The converter rename across packages

- **Decision**: Rename `Convert` → `Read` in Forestry.Serialization, Forestry.Serialization.Xml
  (`XmlBooleanConverter`) and Forestry.StanForD (its enum converter) in one commit, before any
  writing code, so every package builds and its tests pass at every commit (FR-005, as R40 did for
  spec 003).

## R57 - Known gaps kept out

- Built-in converters for enums, `DateOnly`, `TimeOnly` and `TimeSpan` are still missing on the
  reading side; writing gets them when reading does (tracked with #138's "every member type"
  requirement). The string test format's fixtures use types that have converters today.
- Definition policies for writing (choosing members) are not added (R44).
