---

description: "Task list for JSON Lines (spec 005)"
---

# Tasks: JSON Lines

**Input**: Design documents from `specs/005-json-lines/` (plan.md, spec.md, research.md R58-R67,
data-model.md, contracts/public-api.md, quickstart.md) and `.specify/memory/constitution.md` 4.0.1.

**Prerequisites**: research items R58-R67 confirmed by the developer (C5). On confirmation of R59,
the spec's edge case "an undefined value … written as its number" is rewritten to "refused" (T001).

**Board**: feature #138; story issues #149 (US1), #150 (US2), #151 (US3), #152 (US4), #153 (US5),
each with its review gate.

**Tests**: Required (constitution C4), as in specs 001-004. In every story phase: test shells
first, one per acceptance scenario, quickstart row or edge case; then a **test review gate** (the
developer reviews the shells on the board before any code); then implementation. A story's shells
may not compile until its implementation declares the surface they call (accepted in 001-004).

- **Where tests live** (C3, R67): core writing and converters in `Forestry.Serialization/test`
  through the string test format; XML reading in `Forestry.Serialization.Xml/test`; JSON Lines in
  its own new test project; StanForD enums and the round trip in `../stanford/Forestry.StanForD/test`.
- **Every test** uses `// Arrange`, `// Act`, `// Assert` comments (A4); a test reading a document
  uses its own local constant, one element per line.
- **Parsing a line**: `using JsonDocument line = JsonDocument.Parse(text)`; lines from a
  `MemoryStream` via `memory.Position = 0;` then `new StreamReader(memory, leaveOpen: true)` and
  `ReadLine()` until null.
- **Performance gate at the end of US1-US4**: `dotnet test -s Performance.runsettings` on both
  serialization test projects (no writing performance work: C6).
- **Every package builds and its tests pass at every commit.**

**Organization**: Phases by user story in priority order: US1, US2, US3, US5 (P1), then US4 (P2).
No Setup phase: the existing projects are there, and the JSON Lines package is created inside US3.
US2, US3 and US4 each need only US1; US5 needs US2 and US3.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: The user story the task belongs to (US1-US5)
- Paths are relative to `sdk/serialization/`: the core is `Forestry.Serialization/`, the XML format
  package is `Forestry.Serialization.Xml/`, the JSON Lines package is
  `Forestry.Serialization.JsonLines/`, and Forestry.StanForD is `../stanford/Forestry.StanForD/`

## Not in this slice

- Reading JSON Lines; JSON Lines options (naming, omitting nulls, number or date formats, gzip);
  overriding values a format cannot represent ("overflow").
- Collecting settings and the serialization directive (#141); partitions (#147, client work).
- Writing performance work and a JSON Lines performance matrix entry (C6: after the round trip).

---

## Phase 0: Confirmation

- [x] T001 Developer confirms R58-R67 (C5); on R59's confirmation, rewrite the edge case in `Forestry.Serialization/specs/005-json-lines/spec.md` ("An enum value that is not a defined member … written as its number") to "refused when written in name form, with an exception naming the value and the enum", and sync #149-#153's Architecture sections with any change (2026-11-27: confirmed with no changes; edge case rewritten)

---

## Phase 1: User Story 1 - Every flat member type has a built-in converter (Priority: P1) 🎯 MVP (#149)

**Goal**: built-in converters for enums, `DateOnly`, `TimeOnly` and `TimeSpan`; the base
`EnumConverter<TEnum>` with `EnumForm`; `[EnumMemberName]`; converters chosen per member; the new
`TelemetryWriter` methods; `GetSpan`/`GetMemory`/`Advance` virtual (R58-R62, R66).

**Independent Test**: each new kind reads from its UTF-8 text, writes through the string test format
and reads back the same; a test writer overriding one new method changes only that kind
(quickstart 1-4).

### Test shells for User Story 1 ⚠️

- [x] T002 [P] [US1] Fixtures in `Forestry.Serialization/test/Fixtures/`: `TimedTelemetry.cs` (a telemetry with `DateOnly`, `TimeOnly`, `TimeSpan`, `DateOnly?`, `TimeSpan?` members), `EnumTelemetry.cs` (enums `Shade { Light, Dark }`, `Quality { [EnumMemberName("Quality A")] A, [EnumMemberName("Quality B")] B }`, `[Flags] Parts { None = 0, Top = 1, Butt = 2 }`, `Wide : ulong { Max = ulong.MaxValue }`, `Clash { [EnumMemberName("x")] One, [EnumMemberName("X")] Two }`; a telemetry with `Shade`, `Shade?`, `Quality`, `Parts` members and two `Shade` members), and `MemberAwareEnumFactory.cs` (a test `ConverterFactory` with `UsesMember = true` that creates a number-form converter for members named `*Number`, name form otherwise) (2026-11-27: the declared-names enum is `Quality { [EnumMemberName("Quality A")] A, [EnumMemberName("Quality B")] B }`, since spec 003's `FlatTelemetry.cs` already has a `Grade`; `ClashTelemetry` holds `Clash`; `TestEnumConverter<TEnum>(EnumForm)` sits with the factory; developer's review, 2026-11-27: `TestingSerializeOptions` gains `Delimiter`, `;` by default, settable per test, with a theory in `SerializeTests.cs`)
- [x] T003 [P] [US1] Test shell, US1-1 / SC-001 (quickstart 1): `TimedTelemetry` and `EnumTelemetry` build definitions for `TestingSerializeOptions` and `TestingDeserializeOptions` with no converter registered, each member's `ResolvedConverter` not null, in `Forestry.Serialization/test/BuiltInConverterTests.cs`
- [x] T004 [P] [US1] Test shell, US1-2 / SC-002: theories writing then reading `DateOnly` (`MinValue`, `MaxValue`, 2026-11-27), `TimeOnly` (`MinValue`, `MaxValue`, 13:20:00.1234567), `TimeSpan` (`Zero`, `MinValue`, `MaxValue`, -1.02:03:04.5) through `ConverterRoundTrip`, same value back; and reading `13:20:00` and `13:20:00.5` (R61), in `BuiltInConverterTests.cs`
- [x] T005 [P] [US1] Test shell, US1-3 / FR-004 (quickstart 2): `TypedTelemetryWriter` gains the new overloads and `WriteEnumName`, recording calls; serializing `TimedTelemetry` and `EnumTelemetry` calls each kind's method once per member; a writer overriding only `WriteValue(TimeSpan)` changes only `TimeSpan` members, in `BuiltInConverterTests.cs` (fixture `Forestry.Serialization/test/Fixtures/TypedTelemetryWriter.cs`) (2026-11-27: through the built-in converters and `TypedTelemetryWriter.Over` directly, as spec 004's typed-overload test does, and a `MinutesTelemetryWriter` overriding only `WriteValue(TimeSpan)`)
- [x] T006 [P] [US1] Test shell, US1-4: `DateOnly?` and `TimeSpan?` with a value write their value; without, the string test format's null, in `BuiltInConverterTests.cs`
- [x] T007 [P] [US1] Test shell, US1-5 / FR-005: a converter registered for `Shade` is resolved for `Shade` members instead of the built-in enum converter, in `Forestry.Serialization/test/EnumConverterTests.cs`
- [x] T008 [P] [US1] Test shell, US1-6 / FR-002: `Shade.Dark` written by the built-in converter is `Dark` and reads back; `dark` reads to `Dark`; `1` fails with `FormatException`; a test subclass of `EnumConverter<Shade>` created with `EnumForm.Number` writes `1` and reads `1`, and `Dark` fails, in `EnumConverterTests.cs`
- [x] T009 [P] [US1] Test shell, FR-002c / R62 (quickstart 3): `Quality.A` writes `Quality A` and reads from `quality a`; `Clash` makes the options fail at lock with a message naming `Clash` and `X`, in `EnumConverterTests.cs`
- [x] T010 [P] [US1] Test shell, R59 (quickstart 3): `(Shade)7` in name form throws when written, naming `7` and `Shade`; `Parts.Top | Parts.Butt` writes `Top, Butt` and reads back; `Wide.Max` in number form writes `18446744073709551615` and reads back, in `EnumConverterTests.cs`
- [x] T011 [P] [US1] Test shell, R58 (quickstart 4): with `MemberAwareEnumFactory` registered, `EnumTelemetry`'s two `Shade` members get different converters (one `Number`, one `Name`); a `Shade?` member named `*Number` gets a number-form inner converter through the nullable factory; a converter registered for `Shade` still wins over the factory, in `Forestry.Serialization/test/ConverterResolutionTests.cs` (2026-11-27: `TestingDeserializeOptions` gains `Format`, an init list returned as its `FormatConverters`, to hold the factory as a format package would)
- [x] T012 [P] [US1] Test shell, R66: a test writer overriding `GetSpan` to throw still writes `int`, `string`, `Guid` and raw bytes through the base defaults (they use `Output`), in `ConverterWriteTests.cs`

### Review gate

- [x] T013 [US1] **Test review gate**: the developer reviews T002-T012 on the board (#149) and closes the review before T014 (C4) (developer, 2026-11-27: passed, with the string test format's delimiter made a setting)

### Implementation for User Story 1

- [x] T014 [US1] `TelemetryWriter` in `Forestry.Serialization/src/Writing/TelemetryWriter.cs` (R60, R66): `public virtual void WriteValue(DateOnly value)` and `WriteValue(TimeOnly value)` (`WriteFormatted(value, "O")`), `WriteValue(TimeSpan value)` (`"c"`), `public virtual void WriteEnumName(ReadOnlySpan<byte> utf8Name)` (default: the bytes); `GetSpan`, `GetMemory`, `Advance` become `virtual`; `WriteValue(ReadOnlySpan<char>)`, `WriteRaw` and `WriteFormatted` write through `Output`, not `this`; XML docs on each; class remarks list the new kinds (2026-11-27: done; the base defaults take `Output` into a local and write through it)
- [x] T015 [US1] `TestingSerializeOptions`' writer in `Forestry.Serialization/test/Fixtures/TestingSerializeOptions.cs` keeps the base defaults for the new methods (nothing to add unless a shell needs it) (2026-11-27: no change needed: the base defaults serve the string test format)
- [x] T016 [P] [US1] `DateOnlyConverter`, `TimeOnlyConverter`, `TimeSpanConverter` in `Forestry.Serialization/src/Converters/Primitive/` (R61): `Read` decodes UTF-8 into a `stackalloc char[64]` (a `FormatException` when longer) and calls `TryParse(chars, CultureInfo.InvariantCulture, out value)`, else `FormatException` through `Formatting.NotParsable`; `Write` calls the writer's overload (2026-11-27: the three converters and a shared internal `ShortText.Decode` (stack buffer, 64 chars) in one file, `Converters/Primitive/DateOnlyTimeOnlyTimeSpanConverters.cs`, as `DateTimeConverter.cs` holds two)
- [x] T017 [US1] `EnumMemberNameAttribute` in `Forestry.Serialization/src/Attributes/EnumMemberNameAttribute.cs` (R62): `[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]`, constructor `ArgumentException.ThrowIfNullOrEmpty(name)`, `Name` (2026-11-27: done)
- [x] T018 [US1] `EnumForm`, `EnumMember<TEnum>` and the internal `EnumFields<TEnum>` cache in `Forestry.Serialization/src/Converters/EnumFields.OfType.cs` (R59, R62): per member value, C# name, name (`[EnumMemberName]` or the C# name), UTF-8 name; `Dictionary<string, TEnum>` with `StringComparer.OrdinalIgnoreCase` and its `GetAlternateLookup<ReadOnlySpan<char>>()`; value → UTF-8 name; a duplicate name ignoring case throws `InvalidOperationException` naming the enum and the name (new `Throwing`/`Formatting` entries); `IsFlags` (2026-11-27: `EnumForm` and `EnumMember<TEnum>` live with the cache in `Converters/EnumFields.OfType.cs`; the cache is a lazily built instance, not a static constructor, so a clash throws `InvalidOperationException` every time rather than `TypeInitializationException`)
- [x] T019 [US1] `EnumConverter<TEnum>` in `Forestry.Serialization/src/Converters/EnumConverter.OfType.cs` (R59): `protected EnumConverter(EnumForm form)`, `Form`; `Read` (name: decode to stack chars, lookup; flags: split on `,`, trim, OR the values; number: parse the underlying type from UTF-8 invariant) else `FormatException` naming the text and enum; `Write` (name: `WriteEnumName`, flags joined `", "` into a stack buffer, undefined value → `NotSupportedException` naming value and enum; number: `WriteValue(long)`, `ulong` above `long.MaxValue` → `WriteValue(decimal)`); protected `Members`, `TryGetValue`, `TryGetUtf8Name`; XML docs (2026-11-27: done; flags read by splitting on `,` and trimming; flags written by taking members largest value first, then joined smallest first (`Top, Butt`))
- [x] T020 [US1] Converters chosen per member (R58): `ConverterFactory` in `Forestry.Serialization/src/Converters/ConverterFactory.cs` gains `public virtual bool UsesMember => false` and `public virtual Converter CreateConverter(Type type, MemberInfo member, TelemetrySerializationOptions options) => CreateConverter(type, options)`; `TelemetrySerializationOptions.ResolveConverter(Type type, MemberInfo? member)` in `Forestry.Serialization/src/Serialization/TelemetrySerializationOptions.Caching.cs` keeps the precedence and caches by type unless the found factory `UsesMember` and a member is given; `PropertyDefinition<T>.ResolveConverter` passes `Member`; `TypeDefinition<T>` passes null (2026-11-27: `ResolveConverter(Type, MemberInfo? member = null)`; a `UsesMember` factory is asked per member and its result is never cached by type)
- [x] T021 [US1] `NullableConverterFactory` in `Forestry.Serialization/src/Converters/Primitive/NullableConverter.OfType.cs` becomes member-aware (`UsesMember => true`): the inner converter resolves with the member (2026-11-27: done)
- [x] T022 [US1] `EnumConverterFactory` and internal sealed `EnumDefaultConverter<TEnum>` (name form) in `Forestry.Serialization/src/Converters/Primitive/EnumConverterFactory.cs`; add it and T016's converters to `s_primitiveConverters` before `NullableConverterFactory` and `Utf8SpanParsableConverterFactory` (2026-11-27: `EnumConverterFactory` unwraps the `TargetInvocationException` from `Activator`, so a name clash fails as itself)
- [x] T023 [US1] Update `PublicSurfaceTests` in `Forestry.Serialization/test/PublicSurfaceTests.cs` for the new public types and members (contracts/public-api.md) (2026-11-27: the existing test only checks removed names; a theory now checks the contract's new public names)
- [x] T024 [US1] Run `dotnet test` on `Forestry.Serialization/test`, `Forestry.Serialization.Xml/test` and `../stanford/Forestry.StanForD/test`: T003-T012 pass, every earlier test green (StanForD still registers its own enum converter); then the performance gate (2026-11-27: core 166 (155 + 11 new rows), XML 188, StanForD 46 + 1 skipped, 0 warnings; performance gate XML 5 of 5, the core project has no performance tests. Spec 003's `SingleValuesWithoutConverters` (enum, `DateOnly`, `TimeSpan` "without a converter") now holds `UnconvertedMeasure` and `Species`, single values that still have none; the unused `Grade` enum in `FlatTelemetry.cs` is removed)

**Checkpoint**: every flat member type converts both ways; formats can choose converters per member.

---

## Phase 2: User Story 2 - StanForD's enums can be written (Priority: P1) (#150)

**Goal**: StanForD's enums declare their texts; `StanForDEnumConverter` builds on the base, reads
tolerantly and writes the texts; `AddHarvesting` for serialize options (R63).

**Independent Test**: each member of each StanForD enum is read from its text, written, and
compared (quickstart 5).

### Test shells for User Story 2 ⚠️

- [ ] T025 [P] [US2] Test shell, US2-1 / SC-003: a theory over every member of `CuttingReason`, `ProcessingCategory` and `CoordinateReferenceSystem`: written text equals the StanForD text (`OtherManual` → `Other manual`, `GradeBreak` → `Grade break`, `SingleTreeProcessing` → `SingleTreeProcessing`, `WGS84` → `WGS84`) and reads back, in `../stanford/Forestry.StanForD/test/StanForDEnumTests.cs`
- [ ] T026 [P] [US2] Test shell, US2-2 / FR-007: `TestingSerializeOptions`-like options in the StanForD tests (a minimal `SerializeOptions` with a recording writer, `Fixtures/RecordingSerializeOptions.cs`) with `AddHarvesting()` lock without failure and contain `Stem` and `Log`, in `StanForDEnumTests.cs`
- [ ] T027 [P] [US2] Test shell, US2-3: `OtherManual`, `other manual` and `Other manual` read to `CuttingReason.OtherManual`; `Unknown` fails with `FormatException`, in `StanForDEnumTests.cs`
- [ ] T028 [P] [US2] Test shell, US2-4 / FR-002e: a test telemetry with a `CuttingReason` member and a test factory forcing number form, added to options with `AddHarvesting()`: the member is still written `Other manual`, in `StanForDEnumTests.cs`

### Review gate

- [ ] T029 [US2] **Test review gate**: the developer reviews T025-T028 on the board (#150) before T030 (C4)

### Implementation for User Story 2

- [ ] T030 [US2] `[EnumMemberName]` on `CuttingReason.GradeBreak` ("Grade break"), `PulpwoodGrade` ("Pulpwood grade"), `TopBreak` ("Top break"), `OtherManual` ("Other manual") in `../stanford/Forestry.StanForD/src/CuttingReason.cs`
- [ ] T031 [US2] `StanForDEnumConverter<TEnum>` derives from `EnumConverter<TEnum>` with `EnumForm.Name` in `../stanford/Forestry.StanForD/src/Converters/StanForDEnumConverter.cs`: `Read` tries the base, then the text without spaces against `Members`' C# names ignoring case, else `FormatException` as today; no `Write` override; remove the "no enum converter yet" remark
- [ ] T032 [US2] `AddHarvesting<TOptions>(this TOptions options) where TOptions : SerializeOptions` in `../stanford/Forestry.StanForD/src/HarvestingOptions.cs`: adds `Stem` and `Log` and the three StanForD enum converters; XML docs
- [ ] T033 [US2] Run `dotnet test` on `../stanford/Forestry.StanForD/test` (and the core and XML suites): T025-T028 pass, every earlier test green

**Checkpoint**: `Stem` and `Log` can be serialized in any format.

---

## Phase 3: User Story 3 - Telemetries written as JSON Lines (Priority: P1) (#151)

**Goal**: the new `Forestry.Serialization.JsonLines` package writes one telemetry per line over
`Utf8JsonWriter` (R65, R66).

**Independent Test**: telemetries with every member kind serialized to a `MemoryStream`, split
into lines and parsed (quickstart 7-9).

### Package setup

- [ ] T034 [US3] Create `Forestry.Serialization.JsonLines/` like `Forestry.Serialization.Xml/`: `Forestry.Serialization.JsonLines.slnx` (src and test projects), `Directory.Build.props` (`IsShippingLibrary`), `src/Forestry.Serialization.JsonLines.csproj` (`$(RequiredTargetFrameworks)`, `1.0.0-beta.1`, description and tags, project reference to `../../Forestry.Serialization/src/Forestry.Serialization.csproj`, `InternalsVisibleTo` the tests), `test/Forestry.Serialization.JsonLines.Tests.csproj` (xUnit like the XML tests, runsettings), empty `README.md`, `CHANGELOG.md`, `ARCHITECTURE.md`; `dotnet build` the solution
- [ ] T035 [US3] Add `Forestry.Serialization.JsonLines` to the `build-test` and `pack` matrices in `../../.github/workflows/ci.yml` and to `../../.github/workflows/release.yml`'s package list and folder switch (R67)

### Test shells for User Story 3 ⚠️

- [ ] T036 [P] [US3] Fixtures in `Forestry.Serialization.JsonLines/test/Fixtures/`: `EveryKindTelemetry.cs` (`string`, `bool`, `int`, `long`, `double`, `decimal`, `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, an enum, `int?`, `string?`, `DateOnly?`, and a second enum member with `[JsonLinesEnumForm(EnumForm.Number)]`) and `JsonLines.cs` (serialize to a `MemoryStream`, return the lines and the bytes)
- [ ] T037 [P] [US3] Test shell, US3-1 / FR-009: three telemetries → three lines, each ending `\n`, each `JsonDocument.Parse` succeeds with a root object, members in definition order, in `Forestry.Serialization.JsonLines/test/JsonLinesWriterTests.cs`
- [ ] T038 [P] [US3] Test shell, US3-2 / FR-011: `EveryKindTelemetry` with values and with nulls: each property's `ValueKind` (string, number, true/false, null) and value equal the member's (`decimal` via `GetDecimal`, dates via the core converters reading the string), in `JsonLinesWriterTests.cs`
- [ ] T039 [P] [US3] Test shell, US3-3 / SC-005: text with `"`, `\`, `\u0001`, `\n`, `\r\n`, `åäö` and an emoji: one line, the bytes contain `åäö` as UTF-8 (no `\u00`), parses back to the exact text, in `JsonLinesWriterTests.cs`
- [ ] T040 [P] [US3] Test shell, US3-4 / C8: the same telemetries through sync `Stream`, async `Stream` and a `PipeWriter` over a `Pipe`: identical bytes; the stream still open, the `PipeWriter` not completed, in `JsonLinesWriterTests.cs`
- [ ] T041 [P] [US3] Test shell, US3-5 / FR-013: three telemetries into a `MonitoringCollectionStream` over a `MemoryStream`: `Sequence` 3, `BytesWritten` the stream length, in `JsonLinesWriterTests.cs`
- [ ] T042 [P] [US3] Test shell, US3-6 / FR-002d: the plain enum member is a JSON string with its name; the `[JsonLinesEnumForm(Number)]` member of the same enum type a JSON number, in `JsonLinesWriterTests.cs`
- [ ] T043 [P] [US3] Test shell, US3-7 / FR-011a: `double.NaN`, `PositiveInfinity`, `NegativeInfinity` throw `NotSupportedException` whose message names the member and the value, in `JsonLinesWriterTests.cs`
- [ ] T044 [P] [US3] Test shell, US3-8 / FR-011b: a test converter calling `writer.GetSpan()` throws `NotSupportedException` saying to use the value methods; `WriteRaw("{\"a\":1}"u8)` writes the value and `WriteRaw("{"u8)` throws, in `JsonLinesWriterTests.cs`

### Review gate

- [ ] T045 [US3] **Test review gate**: the developer reviews T036-T044 on the board (#151) before T046 (C4)

### Implementation for User Story 3

- [ ] T046 [P] [US3] `JsonLinesEnumFormAttribute` in `Forestry.Serialization.JsonLines/src/Attributes/JsonLinesEnumFormAttribute.cs`: `[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]`, `Form`
- [ ] T047 [P] [US3] `JsonLinesEnumConverterFactory` (`UsesMember => true`, reads the member's `JsonLinesEnumFormAttribute`, default `Name`, caches one converter per enum type and form) and internal sealed `JsonLinesEnumConverter<TEnum> : EnumConverter<TEnum>` in `Forestry.Serialization.JsonLines/src/Converters/JsonLinesEnumConverter.cs`
- [ ] T048 [US3] `SerializeJsonLinesOptions` in `Forestry.Serialization.JsonLines/src/SerializeJsonLinesOptions.cs`: sealed; `FormatConverters` = the factory; `OnMakingReadOnly` builds a `FrozenDictionary<TypeDefinition, JsonEncodedText[]>` of member names by `PropertyDefinition.Index` (encoded with `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`); `CreateWriter` returns a new `JsonLinesTelemetryWriter(this)`; XML docs saying names are the default enum form and NaN is refused
- [ ] T049 [US3] `JsonLinesTelemetryWriter` in `Forestry.Serialization.JsonLines/src/JsonLinesTelemetryWriter.cs` (R65): internal sealed; one `Utf8JsonWriter` created on the first `OnReset` over `Output` with `new JsonWriterOptions { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, SkipValidation = false }`, else `Reset(Output)`; framing and every value method as in R65's mapping; `double.IsFinite` check before `WriteNumberValue`; `WriteEndTelemetry` → `WriteEndObject`, `Flush`, `Output.GetSpan(1)[0] = (byte)'\n'`, `Output.Advance(1)`, `Reset(Output)`; `GetSpan`, `GetMemory`, `Advance` overridden to throw `NotSupportedException` (R66); messages in `Forestry.Serialization.JsonLines/src/Formatting.Resource.cs` and `Throwing.cs`
- [ ] T050 [US3] Run `dotnet test Forestry.Serialization.JsonLines/Forestry.Serialization.JsonLines.slnx`: T037-T044 pass; core and XML suites green; then the performance gate on the two existing serialization test projects

**Checkpoint**: any telemetry can be written as JSON Lines.

---

## Phase 4: User Story 5 - A StanForD Harvesting document makes a round trip (Priority: P1) (#153)

**Goal**: the milestone of #137 and #138 (C6, R67).

**Independent Test**: `MinimalHarvesting.xml` → Stem and Log → two JSON Lines outputs → parsed and
compared; the same over real documents (quickstart 10-11).

### Test shells for User Story 5 ⚠️

- [ ] T051 [US5] Add a project reference to `../../serialization/Forestry.Serialization.JsonLines/src/Forestry.Serialization.JsonLines.csproj` in `../stanford/Forestry.StanForD/test/Forestry.StanForD.Tests.csproj` (the StanForD package does not reference it)
- [ ] T052 [P] [US5] Fixture `../stanford/Forestry.StanForD/test/Fixtures/RoundTrip.cs`: deserialize a document with `new DeserializeXmlOptions().AddHarvesting()`, serialize each `Stem` and each `Log` into its own `MemoryStream` with `new SerializeJsonLinesOptions().AddHarvesting()`, and compare each parsed line with its telemetry through the serialize options' type definition (`Properties`, `Name`, the property's value; strings, booleans, numbers by value, `decimal` via `GetDecimal`; dates, times, durations, enums by reading the JSON string with the property's `ResolvedConverter`; `null` for missing values), returning the differences
- [ ] T053 [P] [US5] Test shell, US5-1 / SC-004: `Fragments/MinimalHarvesting.xml`: Stem lines = Stems, Log lines = Logs, no differences, in `../stanford/Forestry.StanForD/test/RoundTripTests.cs`
- [ ] T054 [P] [US5] Test shell, US5-2: `[RealDocumentFact]` over `RealDocuments.Read()`: no differences in any document, in `RoundTripTests.cs`

### Review gate

- [ ] T055 [US5] **Test review gate**: the developer reviews T051-T054 on the board (#153) before T056 (C4)

### Implementation for User Story 5

- [ ] T056 [US5] Make the round trip pass: fix whatever T053 and T054 reveal in the stories' code (no new feature code expected); record each finding in the story it belongs to
- [ ] T057 [US5] Run `dotnet test` on all four solutions (core, XML, JSON Lines, StanForD), and locally with `STANFORD_HPR_FOLDER` set: everything green; SC-006 counts recorded

**Checkpoint**: the milestone works; writing performance work may start (C6).

---

## Phase 5: User Story 4 - XML documents read enums and durations as XML writes them (Priority: P2) (#152)

**Goal**: `[XmlEnumForm]` and the XML enum converter; `xs:duration` (R64).

**Independent Test**: hand-written documents with enum members (no attribute, names, numbers) and
durations (quickstart 6).

### Test shells for User Story 4 ⚠️

- [ ] T058 [P] [US4] Fixture `Forestry.Serialization.Xml/test/Fixtures/EnumAndDurationTelemetry.cs`: a telemetry with a `Shade` member, a `[XmlEnumForm(EnumForm.Number)] Shade` member and a `TimeSpan` member
- [ ] T059 [P] [US4] Test shell, US4-1: a local document with `<Shade>dark</Shade>` reads `Dark`; `<Shade>1</Shade>` fails with `FormatException`, in `Forestry.Serialization.Xml/test/XmlEnumAndDurationTests.cs`
- [ ] T060 [P] [US4] Test shell, US4-2: the number member `<ShadeNumber>1</ShadeNumber>` reads `Dark`; `Dark` fails, in `XmlEnumAndDurationTests.cs`
- [ ] T061 [P] [US4] Test shell, US4-3 / FR-002e: a converter registered for `Shade` wins over the XML enum converter, in `XmlEnumAndDurationTests.cs`
- [ ] T062 [P] [US4] Test shell, US4-4 / FR-003a: `PT1H30M`, `P2DT3H`, `-PT0.5S`, `PT0S` read to the exact `TimeSpan`; `P1M`, `P1Y`, `P`, `PT`, `1H` fail with `FormatException`, in `XmlEnumAndDurationTests.cs`

### Review gate

- [ ] T063 [US4] **Test review gate**: the developer reviews T058-T062 on the board (#152) before T064 (C4)

### Implementation for User Story 4

- [ ] T064 [P] [US4] `XmlEnumFormAttribute` in `Forestry.Serialization.Xml/src/Attributes/XmlEnumFormAttribute.cs`: `[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]`, `Form`
- [ ] T065 [P] [US4] `XmlEnumConverterFactory` (`UsesMember => true`, default `Name`, one converter per enum type and form) and internal sealed `XmlEnumConverter<TEnum> : EnumConverter<TEnum>` in `Forestry.Serialization.Xml/src/Converters/XmlEnumConverter.cs`
- [ ] T066 [P] [US4] `XmlTimeSpanConverter` in `Forestry.Serialization.Xml/src/Converters/XmlTimeSpanConverter.cs`: a parser over the UTF-8 bytes (optional `-`, `P`, optional `nD`, optional `T` followed by at least one of `nH`, `nM`, `n[.n]S`; at least one part; `Y`, or `M` before `T`, fail), `checked` arithmetic in ticks, `FormatException` with the text; `Write` delegates to the writer
- [ ] T067 [US4] Register both in `s_formatConverters` in `Forestry.Serialization.Xml/src/DeserializeXmlOptions.cs` beside `XmlBooleanConverter`
- [ ] T068 [US4] Run `dotnet test` on `Forestry.Serialization.Xml/test` and `../stanford/Forestry.StanForD/test`: T059-T062 pass, every earlier test green (StanForD's own converter still wins); then the performance gate

**Checkpoint**: XML documents with enums as numbers and durations as `xs:duration` read.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T069 [P] `Forestry.Serialization.JsonLines/README.md` with a "Writing JSON Lines" section: `SerializeJsonLinesOptions`, serializing into a `MemoryStream`, reading the lines back; enum forms, nulls, escaping, NaN; what it does not do (D0, FR-016); `ARCHITECTURE.md` per R65
- [ ] T070 [P] `Forestry.Serialization/README.md`: the new converters, `EnumConverter<TEnum>`, `[EnumMemberName]`, per-member factories, the new writer methods; `Forestry.Serialization.Xml/README.md`: `[XmlEnumForm]`, `xs:duration`; `../stanford/Forestry.StanForD/README.md`: serializing `Stem` and `Log`
- [ ] T071 [P] `CHANGELOG.md` entries in the four packages (1.0.0-beta.1, unreleased)
- [ ] T072 Validate `quickstart.md` end to end; tick the checklists on #149-#153 and #138; reminder to the developer: add `build-test (Forestry.Serialization.JsonLines)` to the main ruleset's required checks before merging
- [ ] T073 Pull request to `main` (the developer merges)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 0 (T001)**: before any shell.
- **US1 (Phase 1)**: first; everything else needs its converters and member-aware resolution.
- **US2 (Phase 2)**, **US3 (Phase 3)**, **US4 (Phase 5)**: each needs only US1; independent of
  each other (different packages).
- **US5 (Phase 4)**: needs US2 and US3.
- **Polish**: after US5 and US4.

### Within Each Story

Shells → review gate → implementation → test run. In US1: T014 (writer) and T016-T019 before T020-T022
(resolution and registration).

### Parallel Opportunities

- US1 shells T002-T012 are all [P] (different test files or independent tests in one file).
- After US1, US2, US3 and US4 can proceed in parallel (StanForD, the new package, XML).
- In US3, T046 and T047 in parallel; T048 then T049.
- In US4, T064-T066 in parallel.

## Parallel Example: after User Story 1

```text
# Three stories, three packages:
US2: T025-T028 shells in ../stanford/Forestry.StanForD/test/StanForDEnumTests.cs
US3: T034-T035 package setup, then T036-T044 shells in Forestry.Serialization.JsonLines/test/
US4: T058-T062 shells in Forestry.Serialization.Xml/test/XmlEnumAndDurationTests.cs
```

## Implementation Strategy

### MVP First (User Story 1 Only)

US1 alone completes the flat member types for every format (and Parquet later).

### Incremental Delivery

US1 → US2 (StanForD writable) → US3 (JSON Lines) → US5 (the milestone) → US4 (XML enums and
durations) → polish and PR. Each story ends green, with its gate closed by the developer.

## Notes

- `tasks.md` is the truth (G0); the story issues' checklists mirror it.
- Design changes found while implementing go back to research.md and the spec first (C5, G0).
