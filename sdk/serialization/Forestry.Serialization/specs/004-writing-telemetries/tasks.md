---

description: "Task list for writing telemetries (spec 004)"
---

# Tasks: Writing telemetries

**Input**: Design documents from `specs/004-writing-telemetries/` (plan.md, spec.md, research.md
R44-R57, data-model.md, contracts/public-api.md, quickstart.md) and `.specify/memory/constitution.md`
4.0.0.

**Prerequisites**: research items R44-R57 confirmed by the developer (C5). Open in research: the
collection writer names (R51) and whether `SizeIsCompressed` is needed now (R53).

**Board**: feature #137; story issues #144 (US1), #145 (US2), #146 (US3), each with its review gate.

**Tests**: Required (constitution C4), as in specs 001-003. In every story phase: test shells
first, one per acceptance scenario, quickstart row or edge case; then a **test review gate** (the
developer reviews the shells on the board before any code); then implementation. A story's shells
may not compile until its implementation declares the surface they call (accepted in 001-003).

- **Writing is tested in the core** (C3, 4.0.0): `Forestry.Serialization/test`, through the string
  test format over a `Stream` (R54). Reading stays tested through the XML package.
- **Async shells are theories over the targets** where the bytes must match: sync `Stream`, async
  `Stream`, `PipeWriter` over a `System.IO.Pipelines.Pipe` (SC-003).
- **Every test** uses `// Arrange`, `// Act`, `// Assert` comments (A4); a test reading a document
  uses its own local constant, one element per line.
- **Reading a `MemoryStream` back**: `memory.Position = 0;` then
  `new StreamReader(memory, leaveOpen: true).ReadToEnd()`.
- **Performance gate at the end of every story**: `dotnet test -s Performance.runsettings` on both
  serialization test projects.
- **Every package builds on main** (R56): the `Convert` → `Read` rename lands in all three packages
  in one commit, before any writing code.

**Organization**: Phases by user story in priority order: US1 (P1), US2 (P1), US3 (P2). No Setup
phase: the projects exist. The stories run in order: US2 needs US1's `Write`, US3 needs US2's
`TelemetrySerialization`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: The user story the task belongs to (US1, US2, US3)
- Paths are relative to `sdk/serialization/`: the core is `Forestry.Serialization/`, the XML format
  package is `Forestry.Serialization.Xml/`, and Forestry.StanForD is `../stanford/Forestry.StanForD/`

## Not in this slice

- JSON Lines and its per-kind framing (#138, R50 note); Parquet.
- The collecting directive, collection stores, transmitting and partitions (#141).
- Built-in converters for enums, `DateOnly`, `TimeOnly`, `TimeSpan` (R57).
- Definition policies for writing (choosing members) (R44, R57).

---

## Phase 1: User Story 1 - Converters read and write (Priority: P1) 🎯 MVP (#144)

**Goal**: `Convert` is renamed `Read` in all three packages, and every built-in converter has a
`Write` that writes the text it reads (R45, R56).

**Independent Test**: each built-in converter writes a value into a buffer and reads the same value
back; a custom converter added to the options is the one that writes (quickstart 1).

### Test shells for User Story 1 ⚠️

- [X] T001 [P] [US1] Test helper `ArrayBufferWriter<byte>`-based round trip in `Forestry.Serialization/test/Fixtures/ConverterRoundTrip.cs`: write a value through a `Converter<T>` into a buffer writer, then `Read` the written span (2026-11-26: also `SmallOfferBufferWriter`, offering 16 bytes without a size hint and recording hints, for T003; and `Fixtures/WritingMarkerInt32Converter.cs` for T004)
- [X] T002 [US1] Test shell, US1-1 / SC-001 (quickstart 1): a theory per built-in kind (`bool`, `char`, `int`, `long`, `double`, `decimal`, `string`, `Guid`, `DateTime`, `DateTimeOffset`, `int?` with a value, and `Measure` from spec 003's `FlatTelemetry` fixture with `MeasureConverter`): write then read gives the same value, in `Forestry.Serialization/test/ConverterWriteTests.cs`
- [X] T003 [US1] Test shell, US1-2 / FR-002: a `string` of 10 000 characters, with a buffer writer that offers 16 bytes on the first `GetSpan`, is written whole (no retry contract), in `ConverterWriteTests.cs`
- [X] T004 [US1] Test shell, US1-3 / FR-004: a custom `Converter<int>` that writes `"x"` added to a test options' `Converters` is the resolved converter used by `PropertyDefinition<int>` for writing, in `ConverterWriteTests.cs`
- [X] T005 [US1] Test shell, R45: a converter that does not override `Write` throws `NotSupportedException` naming the converter type when `Write` is called directly, in `ConverterWriteTests.cs`
- [X] T006 [US1] **Test review gate**: the developer reviews T001-T005 on the board and closes the review task before T007 (C4) (developer, 2026-11-26: passed with no changes)

### Implementation for User Story 1

- [X] T007 [US1] Rename `Convert` to `Read` (R56), in one commit: `Forestry.Serialization/src/Converters/Converter.OfType.cs` and every built-in converter in `Forestry.Serialization/src/Converters/Primitive/`, every caller in `Forestry.Serialization/src` (e.g. `PropertyDefinition.OfType.cs`'s `ReadAndSet`), `Forestry.Serialization.Xml/src/Converters/XmlBooleanConverter.cs`, `../stanford/Forestry.StanForD/src/Converters/StanForDEnumConverter.cs`, and test converters (e.g. `MeasureConverter`, `MeasurementConverter`); run the three packages' test suites, all green with no behavior change (US1-4, SC-002) (2026-11-26: commit 2aca98e; XML 188 and StanForD 46 + 1 skipped green; the core suite waited on T008 because of the shells)
- [X] T008 [US1] Add `public virtual void Write(T value, IBufferWriter<byte> writer)` to `Converter<T>` in `Forestry.Serialization/src/Converters/Converter.OfType.cs`; the default throws `NotSupportedException` through a new `Throwing` method and message naming the converter type (R45); XML docs: "never called with null; ask for more room with `GetSpan(sizeHint)` instead of failing" (revised 2026-11-26, R49: `Write(T value, TelemetryWriter writer)`, commit 5925f21)
- [X] T009 [P] [US1] `Write` overrides in `Forestry.Serialization/src/Converters/Primitive/` (R45): `Utf8SpanParsableConverter<T>` through `IUtf8SpanFormattable.TryFormat` with `CultureInfo.InvariantCulture`, growing the size hint (doubling from 32) while it returns false; `StringConverter` with `Encoding.UTF8.GetMaxByteCount(value.Length)` and `Encoding.UTF8.GetBytes`; `BooleanConverter` writes `true`/`false`; `DateTimeConverter` and `DateTimeOffsetConverter` write round-trip format `"O"`; `NullableConverter<T>` writes the inner converter's output for `value.Value` (2026-11-26: through a shared internal `Utf8Writing.Write`, first hint 64 bytes; `Utf8SpanParsableConverterFactory` now also requires `IUtf8SpanFormattable`, so the converter can write without boxing) (revised 2026-11-26, R49: the built-ins delegate to the writer: `WriteFormattable` for UTF-8 formattable types and round-trip dates, `WriteString`, `WriteBoolean`; `Utf8Writing` became `TelemetryWriter.WriteFormattable`'s default; added `Forestry.Serialization/src/Writing/TelemetryWriter.cs`) (revised again 2026-11-26, R49 option A: `TelemetryWriter.WriteValue` overloads per type; `Utf8SpanParsableConverter<T>` dispatches on `typeof(T)` to the typed overload; a test with `Fixtures/TypedTelemetryWriter.cs` shows each built-in converter calls its type's overload)
- [X] T010 [P] [US1] `Write` override in `MeasureConverter` (test fixture) so T002 covers a custom struct
- [X] T011 [US1] Run `dotnet test` on `Forestry.Serialization/test`, `Forestry.Serialization.Xml/test` and `../stanford/Forestry.StanForD/test`: T002-T005 pass, every earlier test green; then the performance gate (depends on T007-T010) (2026-11-26: passed on net10.0: core 73 (58 + 15 new), XML 188, StanForD 46 + 1 skipped, 0 build warnings; performance gate: XML 4 of 4, the core project has no performance tests)

**Checkpoint**: converters read and write; nothing serializes telemetries yet.

---

## Phase 2: User Story 2 - Serialize telemetries to a Stream or a PipeWriter (Priority: P1) (#145)

**Goal**: `TelemetrySerialization.Serialize`/`SerializeAsync` write one telemetry or a sequence to a
caller's `Stream` (sync and async) or `PipeWriter` (async), through a format's `TelemetryWriter`,
never disposing or completing the target (R44, R46-R50, R54, R55).

**Independent Test**: two telemetries serialized with the string test format into a `MemoryStream`,
read back as two lines; same bytes over an async `Stream` and a `PipeWriter`; a `foreach` over
`Deserialize` serializes into one stream with no pipeline (quickstart 2-4, 9).

### Test shells for User Story 2 ⚠️

- [X] T012 [P] [US2] String test format in `Forestry.Serialization/test/Fixtures/TestingSerializeOptions.cs` (R54): `TestingSerializeOptions : SerializeOptions` whose `CreateObjectConverter` returns an object converter with `WriteStart` writing nothing, `WriteEnd` writing `\n`, and `CreateMemberSections` returning an empty name section, `;` as separator and an empty null section (R49, R54): members' converter output joined by `;`, one telemetry per line, no escaping; telemetry fixtures `Reading` (`int Id`, `string? Name`, `decimal Value`, `Guid Key`) in `Forestry.Serialization/test/Fixtures/Readings.cs` (2026-11-26: the fixtures are `Sample` and `OptionalSample` in `Fixtures/Samples.cs`, since `Reading` clashes with the namespace `Forestry.Serialization.Reading`; `OmitNulls` and `OnTelemetryWritten` are init options of the one test format rather than a second format; `CreateObjectConverter` is `protected abstract`, see R49) (revised 2026-11-26, R49: `CreateWriter` returns a `TelemetryWriter` writing `;` before every member but the first written (`WriteName`), `
` at `WriteEndTelemetry`, and `WriteNull` as the name with no value or nothing when `OmitNulls`)
- [X] T013 [US2] Test shell, US2-1 / quickstart 2: two `Reading` telemetries serialized one by one (sync) into a `MemoryStream`; reading it back gives exactly two lines in order; `memory.CanRead` is still true (not disposed), in `Forestry.Serialization/test/SerializeTests.cs`
- [X] T014 [US2] Test shell, US2-2 / SC-003 / SC-004 (quickstart 3): a theory over sync `Stream`, async `Stream` and `PipeWriter` (a `Pipe`, read back from its `Reader` after the call): the same bytes; the `Stream` is not disposed and the `PipeWriter` can still be written to (not completed), in `SerializeTests.cs`
- [X] T015 [US2] Test shell, US2-3 / FR-011: `Serialize(IEnumerable<Telemetry>, …)` and `SerializeAsync(IAsyncEnumerable<Telemetry>, …)` write every telemetry in order, and an enumerable that records when each item is pulled shows one item is taken only after the previous one was written, in `SerializeTests.cs`
- [X] T016 [US2] Test shell, US2-5 / R50: a `Reading` with `Name` null writes no value between separators (the writer's `WriteNull`), and a converter that throws on null is never called; the `OmitNulls` writer leaves the member out with no stray separator, also when the first member is null (R49), in `SerializeTests.cs`
- [X] T017 [US2] Test shell, US2-6 / FR-008 / SC-007: a telemetry type whose member's converter does not override `Write` (a test `Converter<Grade>` for a test struct, reading only) fails when `TestingSerializeOptions` lock, naming the type and member; nothing has been written to the `MemoryStream` (`Length` 0), in `SerializeTests.cs`
- [X] T018 [US2] Test shell, US2-7 / FR-017: serializing a telemetry whose type was not added fails naming the type, with `Length` 0, in `SerializeTests.cs`
- [X] T019 [US2] Test shell, edge cases: an empty sequence writes nothing; a cancelled token on `SerializeAsync(IAsyncEnumerable…)` stops before the next telemetry and leaves the `Stream` open; a read-only `Stream` fails before writing; changing options after the first serialize throws `InvalidOperationException`, in `SerializeTests.cs` (2026-11-26: a read-only stream throws `ArgumentException`)
- [X] T020 [P] [US2] Test shell, US2-4 / C8 / SC-005 (quickstart 9, R55): in `Forestry.Serialization.Xml/test/SerializeMemoryTests.cs`, a generated document of N and 10×N `Log` telemetries is read with `foreach` over `TelemetrySerialization.Deserialize` and each telemetry serialized into one `MonitoringCollectionStream` over `Stream.Null`; live-heap growth for 10×N stays within the allowance used by `MemoryTests`, and `Sequence` equals the number read. Link `TestingSerializeOptions.cs` into the XML test project (`<Compile Include="..\..\Forestry.Serialization\test\Fixtures\TestingSerializeOptions.cs" Link="Fixtures\TestingSerializeOptions.cs" />`). Marked with the performance trait so it runs in the performance gate (2026-11-26: 10 000 vs 100 000 `Log` telemetries; counts lines through a test `LineCountingStream`, a write-only stream that keeps nothing, until US3's `MonitoringCollectionStream` exists)
- [X] T021 [US2] **Test review gate**: the developer reviews T012-T020 on the board and closes the review task before T022 (C4) (developer, 2026-11-26: passed, after the TelemetryWriter, WriteValue, Utf8Name and Index changes)

### Implementation for User Story 2

- [X] T022 [US2] Options base (R44) in `Forestry.Serialization/src/TelemetrySerializationOptions.cs`: move `Telemetries`, `Converters`, `FormatConverters`, `IsReadOnly`, `MakeReadOnly`, `OnMakingReadOnly`, converter resolution and the definition cache from `DeserializeOptions.cs` and `DeserializeOptions.Caching.cs` into the abstract base; `DeserializeOptions` derives from it and keeps `Policies`, `MaxContextTelemetries`, the enumerator factories and `SkipsByteOrderMark`; `TypeDefinition.Create` and `PropertyDefinition.Create` take the base. Run the three packages' tests: all green (R44 split, developer review 2026-11-26): `OptionsObservableList` moves as a `private protected` nested type; the base's constructor is `private protected`; the base calls `private protected virtual` hooks `IncludeMember(Type)` (DeserializeOptions: definition policies) and `OnDefinitionsBuilt(definitions)` (DeserializeOptions: enumeration policies, enumerated types, type chains, members applying); `ConverterFactory.CreateConverter` takes `TelemetrySerializationOptions` and `TypeDefinition.Options` is typed as the base (public, breaking) (2026-11-26: `TelemetrySerializationOptions.cs`; hooks `OnBuildingDefinitions`, `IncludeMember`, `OnDefinitionsBuilt`; the read-only message no longer says "deserialize"; renamed `TelemetrySerializationOptions` and split into a `.Caching` partial, developer 2026-11-26)
- [X] T023 [US2] Compiled getter in `Forestry.Serialization/src/Definitions/PropertyDefinition.OfType.cs` (R46): `Func<Telemetry, T>` built with expression trees next to `CreateSetter`; internal `WriteMember(Telemetry, TelemetryWriter)`, the counterpart of System.Text.Json's `GetMemberAndWriteJson` (R46, R49): gets the value; a null (null `string`, or `Nullable<T>` without a value) calls the writer's `WriteNull(this)`; otherwise `WriteName(this)` then `((Converter<T>)ResolvedConverter).Write(value, writer)`; and on `PropertyDefinition` (R46, developer 2026-11-26): `public ReadOnlyMemory<byte> Utf8Name`, the member's name in UTF-8, computed once when the definition is built, so writers never encode names per telemetry; and `Index` made public (its position in `TypeDefinition.Properties`), so a columnar writer picks the column in `WriteName` without a lookup (2026-11-26: a set-only property has no getter; `CanGet` lets the write check refuse it)
- [X] T024 [US2] Write check at lock (R45, FR-008): when definitions are built for `SerializeOptions`, a property definition whose resolved converter does not override `Write` is a failure gathered like spec 003's (R33), with a new message `MemberConverterCannotWrite` (member, its type, the converter type, the fix "override `Write`"; it says `SerializeOptions` writes, so its converters must write, R44) in `Forestry.Serialization/src/Formatting.Resource.cs` (2026-11-26: `Converter.CanWrite` (internal) checks `Write` is overridden, `NullableConverter<T>` asks its inner converter; messages `TelemetryTypeCannotBeWritten`, `MemberConverterCannotWrite`, `MemberNotReadable`)
- [X] T025 [US2] The serializer's walk in `Forestry.Serialization/src/TelemetrySerialization.Serialize.cs` (R49): an internal `WriteTelemetry(Telemetry, TypeDefinition, TelemetryWriter)` that calls `WriteStartTelemetry(definition)`, then `WriteMember` of each property definition in order, then `WriteEndTelemetry()`; one writer per call from `SerializeOptions.CreateWriter()`, reset to the call's output (`TelemetryWriter.Reset`, internal). `TelemetryWriter` itself exists since US1 (T009)
- [X] T026 [US2] `SerializeOptions` in `Forestry.Serialization/src/SerializeOptions.cs`: derives from the base; `protected abstract TelemetryWriter CreateWriter()` (the format's writer, R49; protected so formats need no internals) and an internal wrapper the serializer calls; `DefaultCollectionOptions` (US3 fills its type; a placeholder property is added with US3's T033 if US3 lands later) (2026-11-26: `DefaultCollectionOptions` waits for US3's T045, as planned)
- [X] T027 [P] [US2] Internal `PooledBufferWriter` in `Forestry.Serialization/src/Writing/PooledBufferWriter.cs` (R48): `IBufferWriter<byte>` over `ArrayPool<byte>.Shared` (grows by renting a larger array and copying), `WrittenSpan`/`WrittenMemory`, `Clear`, `Dispose` returns the array
- [X] T028 [US2] `TelemetrySerialization.Serialize` (sync, `Stream`) in `Forestry.Serialization/src/TelemetrySerialization.Serialize.cs` (R48, FR-010 to FR-017): validate arguments and that the stream `CanWrite`; lock the options; look up the type definition (fail for an unknown type before writing, FR-017); write into a `PooledBufferWriter`; `utf8.Write(buffer.WrittenSpan)` after each telemetry (many-telemetries overload: when the buffer passes 16 KiB and at the end); `Flush`; never dispose the stream
- [X] T029 [US2] `SerializeAsync` to a `Stream` (R47, R48): per-telemetry returns `ValueTask`, `IAsyncEnumerable` overload returns `Task`; only `WriteAsync`/`FlushAsync` (FR-013); cancellation checked between telemetries, in `TelemetrySerialization.Serialize.cs`
- [X] T030 [US2] `SerializeAsync` to a `PipeWriter` (R48): the writer is reset to the `PipeWriter` itself (no copy); `FlushAsync` after each telemetry (many-telemetries: when `UnflushedBytes` passes 16 KiB and at the end), honoring `FlushResult.IsCanceled`/`IsCompleted`; never `Complete`, in `TelemetrySerialization.Serialize.cs`
- [X] T031 [US2] Run `dotnet test` on all three test projects: T013-T020 pass (T020 after US3's `MonitoringCollectionStream` exists, or temporarily over a counting `Stream`), every earlier test green; then the performance gate (depends on T022-T030) (2026-11-26: passed on net10.0: core 99 (83 + 16 for US2), XML 188, StanForD 46 + 1 skipped, 0 build warnings; performance gate: XML 5 of 5, including T020)

**Checkpoint**: telemetries serialize to a caller's `Stream` or `PipeWriter`; no collections yet.

---

## Phase 3: User Story 3 - Collect telemetries with a collection writer (Priority: P2) (#146)

**Goal**: `MonitoringCollectionStream` and `MonitoringCollectionPipeWriter`, sharing
`ICollectionWriter` and `MonitoringCollectionOptions`, count telemetries and bytes, report full
between telemetries, and complete as ready or abandoned (R51-R53).

**Independent Test**: three telemetries into a collection stream over a `MemoryStream`: `Sequence`
3, `BytesWritten` = length; limits make it full only after a whole telemetry; completion with and
without an exception; the same through the collection `PipeWriter` (quickstart 5-8).

### Test shells for User Story 3 ⚠️

- [X] T032 [US3] Test shell, US3-1 / US3-2 / SC-006 (quickstart 5): three `Reading` telemetries (one with a 10 000-character `Name`, so it takes several writes) into a `MonitoringCollectionStream` over a `MemoryStream`; `Sequence` is 3 and `BytesWritten` equals `memory.Length`, in `Forestry.Serialization/test/CollectionWriterTests.cs` (2026-11-26: `Sample` telemetries, the US2 fixture renamed from `Reading`; fixtures `RecordingCollectionStream` and `CountingCollectionStream` in `Fixtures/CollectionWriters.cs`)
- [X] T033 [US3] Test shell, US3-3 / FR-021 (quickstart 6): with `MaxTelemetries = 2`, `IsFull` is false after one telemetry and true after two; with `TargetSize` smaller than one telemetry, `IsFull` is false before the first and true after it (never mid-telemetry), in `CollectionWriterTests.cs`
- [X] T034 [US3] Test shell, US3-4 / FR-022 (quickstart 7): a subclass overriding `OnCompleted` records its argument; `Complete()` records null after every byte is in the inner `MemoryStream`; `Complete(new IOException())` records the exception; writing after completion throws `InvalidOperationException`, in `CollectionWriterTests.cs` (2026-11-26: the write after completion goes to the stream directly, since `Serialize` checks `CanWrite` first and would throw `ArgumentException`)
- [X] T035 [US3] Test shell, US3-5 / FR-023: with `LeaveOpen = true` the inner `MemoryStream` is still readable after `Complete`; with `LeaveOpen = false` it is disposed; the same for the collection `PipeWriter` over a `Pipe` (the reader sees completion only without `LeaveOpen`), in `CollectionWriterTests.cs`
- [X] T036 [US3] Test shell, US3-6 (quickstart 8): the same three telemetries through `MonitoringCollectionPipeWriter` over a `Pipe` and through `MonitoringCollectionPipeWriter.Create(memory, new() { LeaveOpen = true })`: same bytes, `Sequence` and `BytesWritten` as T032, in `CollectionWriterTests.cs`
- [X] T037 [US3] Test shell, US3-7: a subclass of `MonitoringCollectionStream` overriding `Write(ReadOnlySpan<byte>)` to count calls sees its override run, in `CollectionWriterTests.cs`
- [X] T038 [US3] Test shell, R53: `MonitoringCollectionOptions` with `TargetSize = 0`, `MaxTelemetries = -1` or `MinimumBufferSize = 0` throws `ArgumentOutOfRangeException`; `Default` has `LeaveOpen` false and no limits; `SerializeOptions.DefaultCollectionOptions` is `Default` until set and cannot be set after the options lock, in `CollectionWriterTests.cs`
- [X] T039 [US3] **Test review gate**: the developer reviews T032-T038 on the board and closes the review task before T040 (C4) (developer, 2026-11-26: passed with no changes)

### Implementation for User Story 3

- [X] T040 [P] [US3] `MonitoringCollectionOptions` in `Forestry.Serialization/src/Writing/MonitoringCollectionOptions.cs` (R53): sealed, `init` only: `LeaveOpen` (default false), `TargetSize` (`long?`, "positive when set"), `MaxTelemetries` (`long?`, "positive when set"), `SizeIsCompressed` (default false), `MinimumBufferSize` (default 4096, "positive"), `Pool` (`MemoryPool<byte>?`), static `Default`; validation in the `init` accessors
- [X] T041 [P] [US3] `ICollectionWriter` in `Forestry.Serialization/src/Writing/ICollectionWriter.cs` (R51): `Options`, `Sequence`, `BytesWritten`, `IsFull`; and an internal interface `ICollectionWriterSequence` with `void OnTelemetryWritten()` that both classes implement explicitly (2026-11-26: the internal interface is `ICollectionWriterSequence`)
- [X] T042 [US3] `MonitoringCollectionStream` in `Forestry.Serialization/src/Writing/MonitoringCollectionStream.cs` (R51): public, not sealed; constructor `(Stream inner, MonitoringCollectionOptions? options = null)`; `CanWrite` true until completed, `CanRead`/`CanSeek` false; `Write(ReadOnlySpan<byte>)`, `Write(byte[], int, int)`, `WriteAsync(ReadOnlyMemory<byte>, …)` forward and add to `BytesWritten`; `Flush`/`FlushAsync` forward; `Complete`/`CompleteAsync(Exception?)` flush, dispose the inner stream unless `LeaveOpen`, then `protected virtual OnCompleted(Exception?)`; `Dispose`/`DisposeAsync` complete with null if not completed; `IsFull` per data-model.md (2026-11-26: `Dispose`/`DisposeAsync` complete without an exception; a write after completion throws `InvalidOperationException` with `CollectionCompleted`)
- [X] T043 [US3] `MonitoringCollectionPipeWriter` in `Forestry.Serialization/src/Writing/MonitoringCollectionPipeWriter.cs` (R51): public, not sealed; constructor `(PipeWriter inner, MonitoringCollectionOptions? options = null)`; static `Create(Stream utf8, MonitoringCollectionOptions? options = null)` using `PipeWriter.Create(utf8, new StreamPipeWriterOptions(options.Pool, options.MinimumBufferSize, options.LeaveOpen))`; forwards `GetSpan`, `GetMemory`, `FlushAsync`, `CancelPendingFlush`, `CanGetUnflushedBytes`, `UnflushedBytes`; `Advance` counts; `Complete`/`CompleteAsync` complete the inner writer unless `LeaveOpen` (flushing first), then `OnCompleted` (2026-11-26: a writer made by `Create` owns its inner writer and always completes it, leaving the stream open as `LeaveOpen` says; left open, `CompleteAsync` flushes the caller's writer so every byte reaches it)
- [X] T044 [US3] Sequence at telemetry boundaries (R52) in `Serialization/TelemetrySerialization.Write.cs` and its `.Write.Stream.cs` and `.Write.Pipe.cs`: after each whole telemetry has reached the target, call `OnTelemetryWritten()` when the target is an `ICollectionWriter`; for a `Stream` target that is a collection writer, write the pooled buffer after every telemetry (not at the 16 KiB threshold) so `BytesWritten` is exact at each boundary (2026-11-26: `TelemetryWritten` in `Write.cs`; the many-telemetries `Stream` overloads write each telemetry to a collection stream as it ends)
- [X] T045 [US3] `SerializeOptions.DefaultCollectionOptions` typed `MonitoringCollectionOptions` (default `MonitoringCollectionOptions.Default`), setter throws once the options are read-only, in `Serialization/SerializeOptions.cs` (FR-026)
- [X] T046 [US3] Run `dotnet test` on all three test projects: T032-T038 and T020 pass, every earlier test green; then the performance gate (depends on T040-T045) (2026-11-26: passed on net10.0: core 115 (99 + 16 for US3), XML 188, StanForD 46 + 1 skipped, 0 build warnings; performance gate: XML 5 of 5; T020 now counts through `MonitoringCollectionStream.Sequence` over `Stream.Null`, replacing its temporary line-counting stream)

**Checkpoint**: collections can be counted, limited and completed; monitoring (#141) can build on them.

---

## Phase 3b: Object converter - a type definition of a type writes itself (US2 follow-on, Priority: P1) (#145)

**Goal** (developer, 2026-11-26): as System.Text.Json's `JsonTypeInfo<T>` with `ObjectDefaultConverter<T>`,
each telemetry type gets a `TypeDefinition<T>` whose resolved object converter writes a telemetry: the
writer's start, each property's `WriteMember` (getter `Func<Telemetry, T>`, then `WriteNull` or
`WriteName` and the property's resolved converter), the writer's end. `ObjectConverter<T>` derives from
`Converter<T>` and is resolved like any converter (the developer's, the format's, then the default), for a
telemetry type. The walk moves out of `TelemetrySerialization`. The read side of an object converter is
deferred: its `Read` throws (R49).

**Independent Test**: the existing serializing tests pass unchanged through the default object converter;
an `ObjectConverter<Sample>` added to the options replaces the walk for `Sample`.

### Test shells for the object converter ⚠️

- [X] T052 [P] [US2] Fixtures in `Forestry.Serialization/test/Fixtures/ObjectConverters.cs`: `MarkingSampleConverter : ObjectConverter<Sample>` writing `sample:{Id}\n` through the writer; `PlainSampleConverter : Converter<Sample>`, a converter for a telemetry type that is not an object converter
- [X] T053 [US2] Test shell, R49: every added telemetry type has a `TypeDefinition<T>` whose `Converter` is an `ObjectConverter<T>` (the default when none is registered), in `Forestry.Serialization/test/ObjectConverterTests.cs`
- [X] T054 [US2] Test shell, R49 / FR-004: `MarkingSampleConverter` added to `TestingSerializeOptions.Converters` is the resolved object converter, and serializing a `Sample` writes `sample:1\n` instead of the default walk, in `ObjectConverterTests.cs`
- [X] T055 [US2] Test shell, R49: an object converter's `Read` throws `NotSupportedException` (read side deferred); a `Converter<Sample>` that is not an object converter fails when the options lock, naming the type, in `ObjectConverterTests.cs`
- [X] T056 [US2] **Test review gate**: the developer reviews T052-T055 on the board and closes the review task before T057 (C4) (developer, 2026-11-26: passed with no changes)

### Implementation for the object converter

- [X] T057 [US2] `TypeDefinition` becomes an abstract base with `TypeDefinition<T> : TypeDefinition where T : Telemetry` (created by reflection, as `PropertyDefinition<T>` is), in `Forestry.Serialization/src/Definitions/`; `ObjectConverter<T> : Converter<T> where T : Telemetry` (public, `Read` sealed and throwing `NotSupportedException`) and the internal `ObjectDefaultConverter<T>` (the walk) in `Forestry.Serialization/src/Converters/`; `TypeDefinition<T>.Converter` resolved when the definition is built: the options' converter for `T` when it is an `ObjectConverter<T>` (otherwise a failure naming the type), else the default; `TelemetrySerialization.Write.cs` calls the type definition's converter instead of its own walk (2026-11-26: `TypeDefinition.OfType.cs`, `ObjectConverter.OfType.cs`, `ObjectDefaultConverter.OfType.cs`; the default walks properties with an index loop, so no enumerator is allocated per telemetry; messages `TelemetryTypeConverterNotObjectConverter`, `ObjectConverterCannotRead`)
- [X] T058 [US2] Run `dotnet test` on all three test projects: T053-T055 pass, every earlier test green; then the performance gate (depends on T057) (2026-11-26: passed on net10.0: core 119 (115 + 4), XML 188, StanForD 46 + 1 skipped, 0 build warnings; performance gate: XML 5 of 5)

**Checkpoint**: a telemetry type's definition writes itself through its object converter; a format or developer can replace it per type.

---

## Phase 4: Polish & Cross-Cutting Concerns

- [X] T047 [P] README (FR-029, D0, SC-008): a "Serializing" section in `Forestry.Serialization/README.md`: targets (`Stream` sync/async, `PipeWriter` async), the caller owns the target, the `MemoryStream` example reading the string back, converters' `Read`/`Write`, with `DeserializeOptions` reading (a converter's `Read`) and `SerializeOptions` writing (its `Write`) and an example adding one converter to `readOptions.Converters` and `writeOptions.Converters` (R44), the `TelemetryWriter` for format authors and developers (customizing strings, booleans and custom values), and collection writers with `MonitoringCollectionOptions` (2026-11-26: "Serializing telemetries": a small format, writing to a stream and reading a string back, converters in both directions, object converters, collections; status, packages and tests updated; "chunks" now "collections")
- [X] T048 [P] XML docs on every new public member (D0); the direction made clear (R44): `Converters` and `FormatConverters` say a converter is used to read in `DeserializeOptions` and to write in `SerializeOptions`, the class docs of `DeserializeOptions` (reading) and `SerializeOptions` (writing) say so, and `Converter<T>.Read` and `Write` say which options call them; `ValueTask` rules (await once, never concurrently) on the per-telemetry `SerializeAsync` overloads (2026-11-26: the build generates documentation with warnings as errors, 0 warnings; `Converter<T>` links `SerializeOptions` now that it exists)
- [X] T049 [P] Changelogs: under Breaking Changes in `Forestry.Serialization/CHANGELOG.md`, `Forestry.Serialization.Xml/CHANGELOG.md` and `../stanford/Forestry.StanForD/CHANGELOG.md`: `Convert` renamed `Read`; `DeserializeOptions` derives from `TelemetrySerializationOptions`; under Features Added in `Forestry.Serialization/CHANGELOG.md`: converters' `Write`, `SerializeOptions`, `TelemetrySerialization.Serialize`/`SerializeAsync`, `TelemetryWriter`, collection writers (2026-11-26: also `TypeDefinition` abstract, the options base, object converters, collection writers; the XML and StanForD converters' `Read`)
- [X] T050 Run every quickstart.md scenario (1-9); record the result here (2026-11-26: all pass: scenario 1 `ConverterWriteTests` 25, 2-4 `SerializeTests` 16, 5-8 `CollectionWriterTests` 16, 9 `SerializeMemoryTests` 1 (performance), 10 `ObjectConverterTests` 4 (added for the object converter); suites: core 119, XML 188, StanForD 46 + 1 skipped)
- [X] T051 Mark `spec.md` **Status** Implemented with the date and story issues; bring the board in sync (story issues' checklists and Architecture match this file; #137 notes the implementation; #138 and #141 reminded of what they can now build on) (2026-11-26: spec Implemented; #144-#146 match this file; notes on #137, #138 and #141)

---

## Dependencies & Execution Order

### Phase Dependencies

- **US1 (Phase 1)**: shells, review gate, then implementation; T007 (rename) first, alone in its
  commit
- **US2 (Phase 2)**: after US1 (needs `Write`); its shells can be written while US1 is implemented.
  T020 needs US3's `MonitoringCollectionStream` to pass, or a temporary counting `Stream`
- **US3 (Phase 3)**: after US2 (needs `TelemetrySerialization`); shells can be written during US2
- **Polish (Phase 4)**: after the stories it records

### Within Each Story

- Test shells, then the **test review gate**, then implementation (C4)
- US1: rename, then `Write`, then overrides
- US2: options base, getters and the write check, then the serializer's walk and `SerializeOptions`,
  then the buffer, then the three target paths
- US3: options and interface, then the two writers, then sequence bumping and the default options

### Parallel Opportunities

- US1: T001; after the gate T009 alongside T010
- US2: T012 and T020; after the gate T025 and T027 alongside T023-T024
- US3: after the gate T040 and T041 together
- Polish: T047, T048, T049

## Parallel Example: User Story 2

```text
# Shells, in parallel:
T012 string test format and fixtures (Forestry.Serialization/test/Fixtures/)
T020 foreach memory test (Forestry.Serialization.Xml/test/)

# After the review gate (T021), in parallel with the definitions work:
T025 the serializer's walk      T027 PooledBufferWriter
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. US1 shells, review gate, implementation (T001-T011)
2. **Stop and validate**: quickstart 1; every earlier test green
3. Gives #138 its converters' `Write`

### Incremental Delivery

1. US1 → `Read`/`Write` converters, rename landed everywhere
2. US2 → `TelemetrySerialization.Serialize`/`SerializeAsync` to `Stream` and `PipeWriter`
   (unblocks #138's JSON Lines writer)
3. US3 → collection writers (unblocks #141's collecting directive)

## Notes

- [P] tasks touch different files and depend on no incomplete task
- Each review gate is a board task the developer closes (C4, C5); story issues on the board carry
  scope and design decisions in their Architecture section and are kept in sync with this file
- Commit after each task or logical group; a story's shells reach main only with its
  implementation (R56)
