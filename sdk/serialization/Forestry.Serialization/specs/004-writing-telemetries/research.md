# Research: Writing telemetries

**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-11-26

Numbering continues from spec 003 (R32-R43). Each decision is a proposal until the developer
confirms it (C5).

## R44 - The common options base

- **Decision**: An abstract `TelemetrySerializationOptions` holds what both directions share: the
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
  code both ways. The names follow the package and both directions (developer, 2026-11-26): the static class is
  `TelemetrySerialization` (it was `TelemetrySerializer`, named in #142 after System.Text.Json's
  `JsonSerializer`) and its options base `TelemetrySerializationOptions`.
- **Policies**: stay on `DeserializeOptions`. Definition policies choose members while reading;
  writing writes every member the type definition maps. Member choice for writing is not needed
  for the milestone and is left out (a later spec can add it to the base).
- **What moves to the base** (developer review, 2026-11-26), from `DeserializeOptions.cs` and
  `DeserializeOptions.Caching.cs`:
  - `Telemetries`, `Converters` (with `OptionsObservableList`, the read-only guard, as a
    `private protected` nested type: formats only need the `IList<Converter>` it returns, so it does
    not become public API), `FormatConverters`, `IsReadOnly`/`MakeReadOnly`/`OnMakingReadOnly`;
  - the frozen type definitions (`TryGetTypeDefinition`, `ThrowingGetTypeDefinition`);
  - converter resolution (`ResolveConverter`, `FindConverter`, the primitive converters, the
    resolved-converter cache);
  - building the definitions through `TypeDefinition.Create(type, options, includeMember)`.
  The base calls two `private protected virtual` hooks while locking: `IncludeMember(Type)`
  (default: every mapped member; `DeserializeOptions` applies its definition policies) and
  `OnDefinitionsBuilt(definitions)` (`DeserializeOptions` resolves its enumeration policies:
  enumerated types, type chains, members applying; `SerializeOptions` checks every converter
  overrides `Write`; its writer comes from `CreateWriter`, R49). The base's
  constructor is `private protected`: only `DeserializeOptions` and `SerializeOptions` derive from
  it; format packages derive from those two.
- **Stays on `DeserializeOptions`**: `Policies`, `MaxContextTelemetries`, enumeration
  (`Enumerates`, type chains, members applying, `BeginEnumeration`/`EndEnumeration`), the
  enumerator factories, `SkipsByteOrderMark`.
- **Name and files** (developer, 2026-11-26): the base is `TelemetrySerializationOptions` (named for the
  package and both directions; `TelemetrySerializerOptions`, after System.Text.Json's
  `JsonSerializerOptions`, was the first name), split like `DeserializeOptions`: the members and
  hooks in `TelemetrySerializationOptions.cs`, the frozen definitions and converter resolution in
  `TelemetrySerializationOptions.Caching.cs`. `FormatConverters` stays on the base: reading uses it
  (XML booleans), and a writing format can still convert a type as a whole.
- **Public ripples (breaking, in the changelog)**: `ConverterFactory.CreateConverter(Type,
  TelemetrySerializationOptions)` (was `DeserializeOptions`), `TypeDefinition.Options` typed as the
  base.
- **Constructor rules**: `TypeDefinition.Create` keeps validating constructors for both directions
  (one public constructor, or a marked one), although writing never constructs: the same rules
  both ways (clarification), and telemetry types are usually both read and written.
- **One `Converters` list, the direction comes from the options** (developer review, 2026-11-26):
  `Converters` (the developer's), `FormatConverters` (the format package's) and the primitives are
  about who supplies a converter, not about reading or writing, and every converter has both
  `Read` and `Write`. It must be clear to users and developers that **`DeserializeOptions` reads,
  calling a converter's `Read`, and `SerializeOptions` writes, calling its `Write`**. That is said
  in: the XML docs of `Converters` and `FormatConverters` on the base ("in `DeserializeOptions` a
  converter is used to read (`Read`); in `SerializeOptions` to write (`Write`)"), of
  `DeserializeOptions` and `SerializeOptions` themselves, and of `Converter<T>.Read` ("called when
  deserializing, through `DeserializeOptions`") and `Write` ("called when serializing, through
  `SerializeOptions`"); the message when a serialize options' converter does not override `Write`
  (T024) names `SerializeOptions` and writing; and the package README's serializing section
  (T047), with `readOptions.Converters` and `writeOptions.Converters` in its example. Separate
  names per direction ("formatting converters") were considered and rejected: a converter is one
  object with both methods, often added to both options, and System.Text.Json uses one
  `Converters` for both directions.

## R45 - The converter write signature

- **Decision**: `Converter<T>` gets `public abstract T Read(ReadOnlySpan<byte> utf8)` (renamed from
  `Convert`) and `public virtual void Write(T value, TelemetryWriter writer)` (revised 2026-11-26,
  R49: it was `IBufferWriter<byte> writer`). The default `Write` throws `NotSupportedException`
  naming the converter; every built-in converter overrides it by delegating to the writer's method
  for its kind. `Write` is never called with a null value (FR-012: the writer's `WriteNull`).
- **Rationale**: Mirrors `Read` and `JsonConverter<T>.Write(Utf8JsonWriter, ...)`. The writer is
  abstract and owned by the core, so no format type enters the core (C0, revised). A converter asks for room with `GetSpan(sizeHint)` and
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
  `WriteMember(Telemetry, TelemetryWriter)`, our counterpart of System.Text.Json's
  `GetMemberAndWriteJson` (R49): it gets the value and calls the writer's `WriteNull`, or its
  `WriteName` and then the resolved converter's `Write`, so the call stays generic from telemetry
  to `T` to `Write` (FR-006). A null value (a nullable or a
  `string`) never reaches the converter (R50).
- **Rationale**: Same mechanism as the setter, compiled once per definition, no boxing.
- **Names and positions for writers** (developer, 2026-11-26): `WriteName` is the one per-member
  hook (a name for JSON, a separator for the string test format or CSV, the column for Parquet). Its
  call is cheap; what would cost is encoding `Name` to UTF-8 for every member of every telemetry. So
  `PropertyDefinition` gets `Utf8Name` (`ReadOnlyMemory<byte>`, computed once when the definition is
  built; member names are C# identifiers, so a JSON writer can check once whether escaping is
  needed) and a public `Index` (its position in `TypeDefinition.Properties`, internal until now) for
  columnar writers. No flag to skip `WriteName`: nearly every writer needs the per-member call;
  a `virtual bool` on the writer can be added later if performance work shows a need (C6).
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
    segments behind an `IBufferWriter<byte>`), resets the format's writer to it, and writes the
    written span to the `Stream` after each telemetry (`Write`, or `WriteAsync` on the async
    path), then returns the buffer to the pool. Many-telemetries overloads reuse one buffer for
    the whole call and write when it passes a threshold (default 16 KiB) and at the end.
  - `PipeWriter` targets: the writer is reset to the `PipeWriter` itself (no copy); after each
    telemetry the call `FlushAsync`es (many-telemetries: when unflushed bytes pass the threshold,
    and at the end), honoring backpressure.
  - Neither path disposes a `Stream` or completes a `PipeWriter` (FR-014); both flush what they
    wrote before returning, so a caller can read immediately.
  - The async path only calls `WriteAsync`/`FlushAsync` (FR-013).
- **Rationale**: `Utf8JsonWriter` over a `Stream` uses an internal pooled buffer the same way; a
  `PipeWriter` needs none (no copy). The buffer lives only for the call (FR-015, FR-016, C8).
- **Alternatives considered**: `ArrayBufferWriter<byte>` (not pooled; allocates per call);
  wrapping a `Stream` in `PipeWriter.Create` (adds a pipe's machinery for a one-call buffer).

## R49 - The telemetry writer: how a format, or a developer, plugs in

- **Decision** (revised again 2026-11-26, developer: "converters delegate to an abstract writer
  that the developer can totally customize, i.e. strings, bools, customs"): an abstract
  `TelemetryWriter` in `Forestry.Serialization.Writing`, implementing `IBufferWriter<byte>`, is the
  one class a format or developer overrides. It replaces the earlier `ObjectConverter` and
  `MemberSections`.
  - **Framing a telemetry** (called by the serializer): `WriteStartTelemetry(TypeDefinition)`,
    `WriteName(PropertyDefinition)`, `WriteNull(PropertyDefinition)` (in place of the name and value,
    so a writer can also leave the member out; default: the name and no value),
    `WriteEndTelemetry()`. Separators between members are the writer's own state, as in
    `Utf8JsonWriter`; `OnReset()` clears that state for each call.
  - **Values by type** (called by converters; developer, 2026-11-26: named for what the value is,
    not how one format represents it, since JSON quotes a `Guid` while Parquet stores dates as
    integers): `WriteValue` overloads for `ReadOnlySpan<char>` (text), `bool`, `int`, `long`,
    `double`, `decimal`, `Guid`, `DateTime` and `DateTimeOffset`; `WriteValue<T>(T) where T :
    IUtf8SpanFormattable` for every other value (e.g. a developer's struct); `WriteRaw(ReadOnlySpan<byte>)`.
    Each has a default that writes plain UTF-8 with the invariant culture (unescaped text,
    `true`/`false`, round-trip `O` for dates); a protected `WriteFormatted<T>` helper is shared by the
    defaults and available to subclasses. Overriding the generic overload does not change the typed
    ones, nor the other way round. More overloads (`char`, `float`, `DateOnly`, `TimeOnly`,
    `TimeSpan`, enums) can come with their converters (R57).
  - **Dispatch in the generic built-in converter** (option A, developer, 2026-11-26):
    `Utf8SpanParsableConverter<T>` serves `int`, `long`, `double`, `decimal`, `Guid` and more from one
    class, and `writer.WriteValue(value)` with an unknown `T` would always pick the generic overload.
    It checks `typeof(T)` and calls the typed overload through `Unsafe.As<T, …>(ref value)`; the JIT
    compiles the method per `T` and keeps only the matching branch, with no boxing. The alternative,
    a dedicated converter per type as System.Text.Json has, was not chosen: more classes, and reading
    does not need them.
  - **Raw bytes**: the `IBufferWriter<byte>` members forward to `Output`, the current call's output
    (set by the serializer through an internal `Reset`), for custom converters writing bytes
    themselves.
  - **Where it comes from**: `SerializeOptions.CreateWriter()`, `protected abstract` (a format
    package needs no access to the core's internals to override it). The serializer creates one
    writer per call and resets it to the call's output.
  - **The serializer's walk**: `WriteStartTelemetry`; for each property definition,
    `PropertyDefinition<T>.WriteMember` gets the value through its compiled getter (R46) and calls
    `WriteNull`, or `WriteName` then the resolved converter's `Write`; then `WriteEndTelemetry`.
- **Rationale**: System.Text.Json hands converters a rich writer (`Utf8JsonWriter`) rather than
  bytes; making ours abstract lets every format, and every developer, customize strings, booleans
  and custom values in one class instead of three mechanisms (object converter, member sections,
  format converters). Constitution 4.0.0's C0 was revised before merge to allow it: the core owns
  the abstract writer, formats subclass it, the core never interprets written bytes.
- **Typed overloads** (developer, 2026-11-26): settled as above; the earlier "later" question
  (typed overloads per kind of number, as `Utf8JsonWriter.WriteNumberValue`) is answered by
  `WriteValue` per type.
- **Columnar formats** (Parquet) subclass the writer too: `WriteName` selects the column the next
  value goes to, and values are appended to column buffers rather than written as text.
- **Alternatives considered**: converters write plain bytes into `IBufferWriter<byte>`, with an
  `ObjectConverter` (start, end) and per-property `MemberSections` (name, separator, null) and
  per-kind framing in format converters (the previous R49/R50, after System.Text.Json's
  `ObjectDefaultConverter`): three plug points, and a developer could not change how strings are
  written without replacing converters.

## R50 - Nulls and per-kind framing

- **Decision**: a missing value (null `string`, nullable without a value) never reaches a converter
  (FR-012); the serializer calls the writer's `WriteNull`, which writes the name and the format's
  null (e.g. `null` in JSON), nothing at all (the member left out), or the name and no value (the
  default). Framing by kind (quoting and escaping strings, how booleans or numbers look) is the
  writer's value methods. Format converters remain only for types a format reads or writes
  differently as a whole (e.g. XML's booleans when reading).
- **The string test format** overrides only `WriteName` (`;` before every member but the first
  written), `WriteNull` (omit, or the name with no value) and `WriteEndTelemetry` (a new line).

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
- **Rationale**: An interface lets `TelemetrySerialization` recognize both by type
  (`utf8 is ICollectionWriter`) while each derives from its own BCL base (C# has no multiple
  inheritance). Public and non-sealed so monitoring formatters override them (FR-018).
- **Alternatives considered**: one abstract base (impossible across `Stream` and `PipeWriter`);
  `Sequence` bumped in `Flush` (a flush is not a telemetry boundary on every path).

## R52 - Sequence at telemetry boundaries

- **Decision**: After the writer's `WriteEndTelemetry` for a whole telemetry, once its bytes have been
  handed to the target (written to the `Stream`, or advanced in the `PipeWriter`),
  `TelemetrySerialization` calls `OnTelemetryWritten()` when the target is an `ICollectionWriter`.
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
  whose writer (`CreateWriter`, R49) writes `;` before every member but the first written
  (`WriteName`), a new line at the end (`WriteEndTelemetry`), and a null as the name with no value
  or not at all (`WriteNull`, with an `OmitNulls` option): members' values joined by `;`, one
  telemetry per line, no escaping (the writer's defaults). Tests serialize into a
  `MemoryStream` (rewound, read with `new StreamReader(memory, leaveOpen: true).ReadToEnd()`) and
  over a `System.IO.Pipelines.Pipe` (reading its `Reader`), and through both collection writers.
- **Rationale**: C3 (4.0.0): writing is proven in this package through a string format over a
  `Stream`, without imitating a reader. Lives in tests, not the public API (spec assumption).

## R55 - Where the C8 memory test lives

- **Decision**: In `Forestry.Serialization.Xml.Tests`, next to the existing memory tests
  (`MemoryTests`, which measure heap growth for reading): a `foreach` over
  `TelemetrySerialization.Deserialize` of a generated document serializes each telemetry into one
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
