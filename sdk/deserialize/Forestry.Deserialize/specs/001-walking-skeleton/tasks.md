---

description: "Task list for the walking skeleton (spec 001)"
---

# Tasks: Walking skeleton

**Input**: Design documents from `specs/001-walking-skeleton/` (plan.md, spec.md, research.md,
data-model.md, contracts/public-api.md, quickstart.md) and `.specify/memory/constitution.md` 2.0.0.

**Prerequisites**: every research item is Confirmed, Rejected or Deferred (research.md header).

**Tests**: Required (constitution C4). In every story phase: test shells first, one per acceptance
scenario or quickstart row; then a **test review gate** (the developer reviews the shells on the
board before any code); then implementation. Shells compile against the Phase 2 skeletons and fail
until implemented.

**Organization**: Phases by user story in priority order: US1 (P1), US2 (P2), US4 (P2), US3 (P3).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: The user story the task belongs to (US1, US2, US3, US4)
- Paths are relative to `sdk/deserialize/`: the core is `Forestry.Deserialize/`, the media package
  is `Forestry.Deserialize.Xml/` (plan.md, Project Structure)

## External prerequisites

- **XML reader element and text reading** (`Forestry.Deserialize.Xml/BLOCKING.md`, phase 2): every
  XML acceptance test (`Forestry.Deserialize.Xml/test/WalkingSkeletonTests.*.cs`, T020-T029,
  T050, T055-T057, T067-T068, T075) and the XML reading tasks (T046, T073) need it before they can
  pass. Their shells can be written and reviewed now. Core-only tests (`Forestry.Deserialize/test/`)
  do not depend on it.
- **XML reader multi-segment support** (`BLOCKING.md` "not part of this pass", issue #24): needed by
  quickstart row 7 (T068) and T073, buffers split at arbitrary byte positions.

## Not in this slice

Deferred in research.md, so no tasks here:

- **Spec 002/003**: constructor-parameter fail-fast cases (R5); parameter resolution from open or
  retained rows, retention of completed rows, the fail-fast policy (R5, R7); the full policy
  catalogue and slot order (R7); R7's edge cases; the XML package's association approach beyond
  name only (R13 B/C); `DeserializerKind` / `TypeDefinitionKind` (R10).
- **Implementation (names, confirmed in code review)**: attribute names (R5, R8), `Pipeline(Type)`
  (R6), registration member names (R12), the converter name "Converter" or "Deserializer" (R14),
  dimensions delimiters and builder API (R15). The names used below are the research examples.
- **Spec 002/003 (also)**: adding constructor parameter types automatically and transitively, and
  rejecting a parameter type not derived from `Row` (R5, R12).
- **Key-references slice (spec 002-005)**: references by key (R5): the store slot at completion,
  the resolve slot in the member pipeline, the reference attribute (R7).
- **Later slice**: the S1 fail-fast limits (FR-014).

**Note (R9)**: documentation option B is set up in T001. Switching to option A (missing XML docs
fail the build) is not a task here: it becomes mandatory with the first real StanForD document
deserialized end to end (C6), in the slice that delivers it.

## Flow coverage

The flows deserialization has to get right, where each is tested, and which slice owns it
(developer, 2026-10-30):

| Flow | Slice | Test shells |
|---|---|---|
| 1. Type definitions created from a specific options instance (per options, never global) | 001 | T030-T034 (FR-008), T035 (isolation), T028 (`[Row]` name) |
| 2. Property definitions per type definition (properties and fields, names) | 001 | T026 (field member), T027 (`[Field]` name), T031 (nested property rejected) |
| 3. Effective converter per property definition | 001 | T024 (built-in failure), T029 (XML default), T036 (options over built-in) |
| 4. Object construction with parameters, only of row types | 002/003 | Deferred |
| 5. Parameter types added automatically as type definitions | 002/003 | Deferred |
| 6. Key references: attributes or options establish the referenced type definition, its keyed store and its lookup | key-references slice | Deferred |
| 7. Policies per slot, and execution order | 001 (minimal order) | T055 (defaults), T056 (attribute policy), T057 (order across slots), T058 (precedence), T059 (read-only options) |

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Documentation build setting and removal of the old design (R0, R10). The build is
expected to be red from T002 until T017.

- [ ] T001 Enable XML documentation, option B (R9): set `GenerateDocumentationFile` to `true` and keep CS1591 a warning (`WarningsNotAsErrors` includes `CS1591`) in `Forestry.Deserialize/src/Forestry.Deserialize.csproj`
- [ ] T002 [P] Delete the old Value model (R10, Confirmed): `Forestry.Deserialize/src/Value.cs`, `Value.OfType.cs`, `NullableValue.OfType.cs`, `DeserializedValue.OfType.cs`, `Dimension.cs`, `Dimension.Defaults.cs`, `Dimensions.cs`
- [ ] T003 [P] Delete the old reading layer (R10, Confirmed): `Forestry.Deserialize/src/ValueAsyncEnumerable.cs`, `ValueAsyncEnumerator.cs`, `ReadErrorHandling.cs`, `Reading/IBuffering.cs`, `Reading/ReadingStatus.cs`, `Reading/IReaderState.cs`, `Reading/ReaderPath.cs`, `Reading/ReaderPosition.cs`
- [ ] T004 [P] Delete the XML vocabulary from the core (C0, R10): `Forestry.Deserialize/src/Attributes/ElementAttribute.cs`, `Forestry.Deserialize/src/Attributes/CollectionAttribute.cs`
- [ ] T005 [P] Delete the old core tests and fixtures, which test the rejected design (R0, R11): `Forestry.Deserialize/test/DeserializationPipeTests.cs`, `TypeDefinitionReflectionTests.cs`, `TestingDeserializeOptions.cs`, `TestingDeserializerProvider.cs`, `TestingMachine.cs`, `TestingObjectDeserializer.cs`, `TestingPropertyDefinition.cs`, `TestingTypeDefinition.cs`, `TestingValue.cs`, `TestingValueDeserializer.cs`
- [ ] T006 [P] Delete the XML package's old deserialization layer (R0): `Forestry.Deserialize.Xml/src/Deserializers/` (`ObjectDeserializer.cs`, `XmlDeserializer.cs`, `Value/`) and `Forestry.Deserialize.Xml/src/Deserialization.Property.cs`; the reader under `Forestry.Deserialize.Xml/src/Reading/` stays
- [ ] T007 Rename `Forestry.Deserialize/src/ReflextionExtensions.cs` to `ReflectionExtensions.cs`, including the class name and its callers (R10)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The public surface of `contracts/public-api.md` as compile-only skeletons (members throw
`NotImplementedException`), so every story's test shells compile. Behavior is written in the story
phases, after each test review gate (C4).

**⚠️ CRITICAL**: No story's test shells can compile until this phase is complete.

- [ ] T008 [P] Declare `Row` (abstract; `Type`, `AsType<T>()`, `Is<T>()`) in `Forestry.Deserialize/src/Rows/Row.cs` and `Field` (metadata about one member: member info, media name, C# type, converter; never a value, R4) in `Forestry.Deserialize/src/Rows/Field.cs`
- [ ] T009 [P] Declare `Converter<T>` with `T Convert(ReadOnlySpan<byte> utf8)` in `Forestry.Deserialize/src/Converters/Converter.OfType.cs` and a converter factory in `Forestry.Deserialize/src/Converters/ConverterFactory.cs` (R14); delete `Forestry.Deserialize/src/Deserializers/` (`Deserializer.cs`, `Deserializer.OfType.cs`, `DeserializerFactory.cs`, `IDeserializerProvider.cs`), whose role the converters take over (R10)
- [ ] T010 [P] Declare `RowAttribute` (class; optional name), `FieldAttribute` (property or field; optional name) and `RowConstructorAttribute` (constructor; `AllowMultiple = false`) in `Forestry.Deserialize/src/Attributes/RowAttribute.cs`, `FieldAttribute.cs`, `RowConstructorAttribute.cs` (R5, R8)
- [ ] T011 [P] Declare the reading types in `Forestry.Deserialize/src/Reading/`: `RowState.cs` (`None`, `Partial`, `Complete`), `FieldContext.cs` (`readonly ref struct` with `int Depth`, `ReadOnlySpan<byte> Name`, `ReadOnlySpan<byte> Path`, `ReadOnlySpan<byte> Dimensions`), `FieldContextExtensions.cs` (UTF-8 span to `string`, dimensions to dictionary), `DimensionsBuilder.cs` (rented bytes), `DeserializationState.cs` (R2, R15)
- [ ] T012 Declare the base enumerators in `Forestry.Deserialize/src/Reading/RowEnumerator.cs` (`IEnumerator<Row>`) and `Forestry.Deserialize/src/Reading/RowAsyncEnumerator.cs` (`IAsyncEnumerator<Row>`), each with `protected abstract RowState Read(ReadOnlySequence<byte> bytes, bool isFinalBlock, out Row? row, out long consumed)` and the protected pipeline calls `Pipeline<T>() where T : Row`, `Pipeline(PropertyInfo property, ReadOnlySpan<byte> utf8, in FieldContext context)`, `Pipeline(FieldInfo field, ReadOnlySpan<byte> utf8, in FieldContext context)` (two overloads, one internal path), `Pipeline()` (R2, R6)
- [ ] T013 Reshape `Forestry.Deserialize/src/DeserializeOptions.cs` and `DeserializeOptions.Caching.cs` to the new surface: abstract; `Rows.Add<T>() where T : Row` (chainable) and `Rows.Add(params Type[] types)`; converter and policy registration using `ObservableList<T>`; abstract creation of the media's `RowEnumerator` (over `ReadOnlyMemory<byte>`) and `RowAsyncEnumerator` (over `PipeReader`) (R6, R12)
- [ ] T014 Declare the entry points in `Forestry.Deserialize/src/Deserialization.cs`, `Deserialization.String.cs` (`Deserialize(string, DeserializeOptions)`), `Deserialization.Memory.cs` (replaces `Deserialization.Span.cs`; `Deserialize(ReadOnlyMemory<byte>, DeserializeOptions)`) and `Deserialization.Pipe.cs` (`DeserializeAsync(PipeReader, …, CancellationToken)`, `DeserializeAsync(Stream, …, CancellationToken)`) (R3)
- [ ] T015 Declare the policy surface in `Forestry.Deserialize/src/Policies/`: a policy per minimal slot of the type pipeline (construct; hand out) and the member pipeline (convert, set, Field metadata); push, complete and pop are fixed stack mechanics, not slots, plus a policy attribute base in `Forestry.Deserialize/src/Attributes/PolicyAttribute.cs` (R7)
- [ ] T016 [P] Declare the XML side: reshape `Forestry.Deserialize.Xml/src/DeserializeXmlOptions.cs` to the new `DeserializeOptions` surface (the documents' `XmlDeserializeOptions`; name Deferred), and add skeletons `Forestry.Deserialize.Xml/src/XmlRowReading.cs`, `XmlRowEnumerator.cs` (`: RowEnumerator`), `XmlRowAsyncEnumerator.cs` (`: RowAsyncEnumerator`)
- [ ] T017 Build both packages green (`dotnet build` of `Forestry.Deserialize/Forestry.Deserialize.slnx` and `Forestry.Deserialize.Xml/Forestry.Deserialize.Xml.slnx`); `DeserializerKind` and `TypeDefinitionKind` stay as they are (Deferred), except removing references that no longer compile

**Checkpoint**: The surface compiles; test shells can now be written.

---

## Phase 3: User Story 1 - Shred a flat class into rows (Priority: P1) 🎯 MVP

**Goal**: A document with `Log` occurrences becomes one `Log` row per occurrence, in order, through
the synchronous entry point.

**Independent Test**: Small hand-written XML documents and `Log : Row { int LogKey; decimal LogVolume }`
registered on the XML options; check the rows (quickstart rows 1-3, 12-14).

### Test shells for User Story 1 ⚠️ (XML shells need BLOCKING.md phase 2 to pass)

- [ ] T018 [P] [US1] Add the fixture `Log : Row` with `int LogKey` and `decimal LogVolume` in `Forestry.Deserialize.Xml/test/Fixtures/Log.cs`
- [ ] T019 [P] [US1] Add core fixtures in `Forestry.Deserialize/test/Fixtures/`: `Log.cs` (as T018), `LogWithNestedProperty.cs` (a property of a nested class), `LogWithParameters.cs` (only a public constructor with a parameter), `LogWithTwoPublicConstructors.cs` (two public constructors, none marked), `LogWithTwoMarkedConstructors.cs` (two constructors marked `[RowConstructor]`)
- [ ] T020 [US1] Test shell, quickstart 1 / US1-1 / FR-003: `<Root><Log><LogKey>1</LogKey><LogVolume>0.59</LogVolume></Log><Log><LogKey>2</LogKey><LogVolume>0.50</LogVolume></Log></Root>` gives two rows in order, members as given, in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [ ] T021 [US1] Test shell, quickstart 2 / US1-2 / FR-005: document 1 plus elements outside any `Log` and an `<Unknown>` element inside one gives the same two rows, in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [ ] T022 [US1] Test shell, quickstart 3 / US1-3: `<Root />` gives no rows and no error, in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [ ] T023 [US1] Test shell, spec edge case: an empty element for a member (`<LogVolume />`) leaves the member null, or its type's default where null is not allowed, in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [ ] T024 [US1] Test shell, quickstart 12 / FR-007: `<LogKey>abc</LogKey>` throws, naming the member, its place in the document and `Log`, in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [ ] T025 [US1] Test shell, quickstart 14 / spec edge case: a document cut off inside an open `Log` throws (from the media's reader) and the open row is never handed out, in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [ ] T026 [US1] Test shell, flow 2 (property definitions): a public field member is set like a property: fixture `LogWithFieldMember : Row` with public field `decimal LogVolume` in `Forestry.Deserialize.Xml/test/Fixtures/LogWithFieldMember.cs`; document 1 gives the field values (compiled-expression setter, R14), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [ ] T027 [US1] Test shell, flow 2: `[Field("Volume")]` on `LogVolume` in fixture `Forestry.Deserialize.Xml/test/Fixtures/LogWithRenamedField.cs`: `<Volume>0.59</Volume>` sets `LogVolume` and `<LogVolume>` is ignored (R8), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [ ] T028 [US1] Test shell, flow 1/2: `[Row("Tree")]` on fixture `Forestry.Deserialize.Xml/test/Fixtures/TreeLog.cs`: `<Tree>` occurrences give rows and `<Log>` occurrences are ignored (R8), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [ ] T029 [US1] Test shell, flow 3 (media default converter): fixture `Forestry.Deserialize.Xml/test/Fixtures/LogWithFlag.cs` with `bool TopSawing`: `<TopSawing>1</TopSawing>` gives `true` and `<TopSawing>0</TopSawing>` gives `false` (XML default converter, R14), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [ ] T030 [P] [US1] Core test shell, quickstart 13 / FR-008: no row type registered throws before any byte is read, in `Forestry.Deserialize/test/DefinitionValidationTests.cs`
- [ ] T031 [US1] Core test shell, FR-008: `LogWithNestedProperty` throws at the call (before any byte is read), in `Forestry.Deserialize/test/DefinitionValidationTests.cs`
- [ ] T032 [US1] Core test shell, FR-008: `LogWithParameters` throws (this slice: chosen constructor has parameters), in `Forestry.Deserialize/test/DefinitionValidationTests.cs`
- [ ] T033 [US1] Core test shell, FR-008: `LogWithTwoPublicConstructors` throws (no constructor can be chosen), in `Forestry.Deserialize/test/DefinitionValidationTests.cs`
- [ ] T034 [US1] Core test shell, FR-008: `LogWithTwoMarkedConstructors` throws (more than one marked), in `Forestry.Deserialize/test/DefinitionValidationTests.cs`
- [ ] T035 [US1] Core test shell, flow 1 (options isolation, S0, R5): `Log` added to options A is in A's row type lookup and not in options B's, built from a second options instance, in `Forestry.Deserialize/test/OptionsTests.cs`
- [ ] T036 [US1] Core test shell, flow 3 (effective converter): a `Converter<int>` registered in the options becomes the effective converter of `Log.LogKey` instead of the built-in (precedence: options, attributes, media defaults, built-ins; R14), with the test converter in `Forestry.Deserialize/test/Fixtures/MarkingInt32Converter.cs`, in `Forestry.Deserialize/test/ConverterResolutionTests.cs`
- [ ] T037 [US1] **Test review gate**: the developer reviews shells T018-T036 (`Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`, `Forestry.Deserialize/test/DefinitionValidationTests.cs`, `Forestry.Deserialize/test/OptionsTests.cs`, `Forestry.Deserialize/test/ConverterResolutionTests.cs`, fixtures) on the board and closes the review task before T038 starts (C4)

### Implementation for User Story 1

- [ ] T038 [P] [US1] Implement the built-in converters in `Forestry.Deserialize/src/Converters/BuiltIn/`: one generic converter for `T : IUtf8SpanParsable<T>` with invariant culture (integers, `decimal`, `double`, `DateTime`, …), plus `StringConverter` and `BooleanConverter`; a failed conversion throws naming the member, its place (from the `FieldContext` path) and the class (FR-007, R14)
- [ ] T039 [P] [US1] Implement registration and locking in `Forestry.Deserialize/src/DeserializeOptions.cs` and `DeserializeOptions.Caching.cs`: `Rows.Add<T>()` returning the row collection for chaining, and `Rows.Add(params Type[] types)` throwing for a type not extending `Row`; mark these types as explicitly added (only they are handed out, R12); no assembly scan; on first use, lock the options and build the per-options frozen row type lookup (`Type` to type definition), never global (R5, R6, R12, S0)
- [ ] T040 [US1] Implement type definitions in `Forestry.Deserialize/src/Definitions/TypeDefinition.cs`, `TypeDefinition.OfType.cs`, `TypeDefinition.Properties.cs`: choose the constructor ("exactly one marked: use it, whatever its accessibility; none marked and exactly one public: use it; otherwise fail fast; more than one marked: fail fast"), build its delegate once; one property definition per public property or field of a built-in type ("text, boolean, integer numbers, decimal numbers and date/time"); reject per FR-008 before any byte is read (depends on T039)
- [ ] T041 [US1] Implement `PropertyDefinition<T>` in `Forestry.Deserialize/src/Definitions/PropertyDefinition.cs` and `PropertyDefinition.OfType.cs`: converter resolved once by `T` ("options, attributes, media defaults, built-ins"); setter `Action<Row, T>` built once (setter delegate for a property, compiled expression for a field); non-generic `ReadAndSet(Row row, ReadOnlySpan<byte> utf8)` dispatching into `T`, no boxing; per-definition `Field` metadata (R14) (depends on T038, T040)
- [ ] T042 [US1] Implement the pipelines in their minimal slot order in `Forestry.Deserialize/src/Reading/DeserializationState.cs` and `Forestry.Deserialize/src/Reading/RowPipelines.cs`: `Pipeline<T>()` constructs (no parameters in this slice) and pushes on the open-row stack; `Pipeline(member, utf8, context)` converts and sets on the top open row; `Pipeline()` completes the top row, hands it out, pops it and keeps no reference; a type not in the lookup throws (S0), a member not of the top row's type throws, `Pipeline()` with no open row throws (R2, R6, R7)
- [ ] T043 [US1] Implement `RowEnumerator` in `Forestry.Deserialize/src/Reading/RowEnumerator.cs`: `MoveNext` wraps the memory in a single-segment `ReadOnlySequence<byte>` (no copy), calls `Read` with the final-block flag set, sets `Current` on `Complete`, returns `false` on `None`; `Reset()` throws `NotSupportedException`; `Dispose` clears the `DeserializationState` and hands out no row (R2)
- [ ] T044 [US1] Implement the synchronous entry points in `Forestry.Deserialize/src/Deserialization.String.cs` and `Deserialization.Memory.cs`: build definitions and lock the options before returning (FR-008 errors throw at the call); a `string` is encoded to UTF-8; return an `IEnumerable<Row>` over the media's `RowEnumerator` that can be enumerated once (a second `GetEnumerator()` throws) (R2, R3)
- [ ] T045 [US1] Implement association in `Forestry.Deserialize.Xml/src/DeserializeXmlOptions.cs`, built once per options (reflection only then): an element named like the class (or its `[Row]` name) maps to a delegate calling `Pipeline<T>()`; a child element named like a member (or its `[Field]` name) maps to its `PropertyInfo` / `FieldInfo`; registers the XML default converters (`bool` accepts `true`, `false`, `1`, `0`, as XML Schema does); supplies `XmlRowEnumerator` (R6, R8, R13 A, R14)
- [ ] T046 [US1] Implement `Forestry.Deserialize.Xml/src/XmlRowReading.cs` (needs BLOCKING.md phase 2): rebuild `Utf8XmlReader` from its saved state each call; the start of an associated element calls `Pipeline<T>()`; the text of an associated member calls `Pipeline(member, utf8, context)` with depth, name and path, resolving escapes and entities into a scratch buffer (`stackalloc` for short values, rented `ArrayPool<byte>` for long ones); the end element of an open row calls `Pipeline()` and returns `Complete`; unassociated data is skipped; the closed root returns `None`; an incomplete grammar at the final block throws; bytes consumed are reported even when 0 (R2)
- [ ] T047 [US1] Implement `Forestry.Deserialize.Xml/src/XmlRowEnumerator.cs` delegating `Read` to `XmlRowReading` (R2)
- [ ] T048 [US1] Run `dotnet test` on `Forestry.Deserialize/test/Forestry.Deserialize.Tests.csproj` and `Forestry.Deserialize.Xml/test/Forestry.Deserialize.Xml.Tests.csproj`: T020-T036 pass

**Checkpoint**: User Story 1 works end to end with the synchronous entry point (MVP).

---

## Phase 4: User Story 2 - Work with rows without knowing their class (Priority: P2)

**Goal**: Rows come out as `Row`; the developer asks the type and casts to it.

**Independent Test**: Rows from Story 1: `Type`, `AsType<Log>()`, `AsType<Other>()`, a missing member
(quickstart rows 4-5).

### Test shells for User Story 2 ⚠️

- [ ] T049 [P] [US2] Core test shell, quickstart 4 / US2-1..3 / FR-006: a `Log` row built directly reports `Type` `Log`; `AsType<Log>()` gives 1 / 0.59; `AsType<Other>()` throws naming both classes; `Is<Log>()` is true, in `Forestry.Deserialize/test/RowTests.cs`
- [ ] T050 [P] [US2] Test shell, quickstart 5 / US2-4: a `Log` without `<LogVolume>` leaves `LogVolume` at its default, in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US2.cs`
- [ ] T051 [US2] **Test review gate**: the developer reviews T049-T050 (`Forestry.Deserialize/test/RowTests.cs`, `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US2.cs`) on the board before T052 (C4)

### Implementation for User Story 2

- [ ] T052 [US2] Implement `Row` in `Forestry.Deserialize/src/Rows/Row.cs`: `Type` returns `GetType()`; `AsType<T>()` is a checked cast throwing `InvalidCastException` naming both classes; `Is<T>()` (R4)
- [ ] T053 [US2] Run `dotnet test` on both test projects: T049-T050 pass, US1 still green

**Checkpoint**: User Stories 1 and 2 work.

---

## Phase 5: User Story 4 - Change behavior with policies (Priority: P2)

**Goal**: Pipeline slots are policy points; policies attach by attribute and through the options,
options win; options are read-only once used.

**Independent Test**: A recording test policy attached by attribute, by options, and by both
(quickstart rows 8-11).

### Test shells for User Story 4 ⚠️

- [ ] T054 [P] [US4] Add a recording test policy (records each call of its slot, in order) in `Forestry.Deserialize/test/Fixtures/RecordingPolicy.cs` and `Forestry.Deserialize.Xml/test/Fixtures/RecordingPolicy.cs`
- [ ] T055 [US4] Test shell, quickstart 8 / US4-1 / FR-011: without policies, document 1 gives the defaults: construct and push at start, convert and set members, hand out and pop at the media's end token, no reference kept, in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US4.cs`
- [ ] T056 [US4] Test shell, quickstart 9 / US4-2 / FR-012: the recording policy attached to `Log` by attribute acts once per row, in document order, in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US4.cs`
- [ ] T057 [US4] Test shell, flow 7 (order across slots): a recording policy in every slot of both pipelines sees, per row, construct, then convert and set for each member, then hand out, in that order (R7 minimal order), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US4.cs`
- [ ] T058 [P] [US4] Core test shell, quickstart 10 / US4-3 / FR-012: the same slot attached by attribute and by options: only the options' policy acts (precedence "option for that property or parameter, option for that type, attribute on the property or parameter, attribute on the type, options-wide default, built-in default"), in `Forestry.Deserialize/test/PolicyPrecedenceTests.cs`
- [ ] T059 [P] [US4] Core test shell, quickstart 11 / US4-4 / FR-013: changing options (a policy, a row type, a converter) after first use throws, in `Forestry.Deserialize/test/OptionsTests.cs`
- [ ] T060 [US4] **Test review gate**: the developer reviews T054-T059 (`Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US4.cs`, `Forestry.Deserialize/test/PolicyPrecedenceTests.cs`, `Forestry.Deserialize/test/OptionsTests.cs`, `RecordingPolicy.cs` fixtures) on the board before T061 (C4)

### Implementation for User Story 4

- [ ] T061 [US4] Implement the slots and built-in defaults in `Forestry.Deserialize/src/Policies/` (type pipeline slots: construct; hand out, around the fixed push, complete and pop. Member pipeline slots: convert, set; Field metadata slot off by default) and wire `RowPipelines` to call the resolved policy per slot (R7)
- [ ] T062 [US4] Implement precedence resolution once per definition when definitions are built, in `Forestry.Deserialize/src/Policies/PolicyResolver.cs`, with the six levels quoted in T058 (R7)
- [ ] T063 [P] [US4] Implement attachment by attribute through `Forestry.Deserialize/src/Attributes/PolicyAttribute.cs` and by options through the policy registration in `Forestry.Deserialize/src/DeserializeOptions.cs` (R7)
- [ ] T064 [US4] Implement read-only options in `Forestry.Deserialize/src/DeserializeOptions.cs` and `Forestry.Deserialize/src/ObservableList.OfType.cs`: once locked (T039), any change throws (FR-013, R6)
- [ ] T065 [US4] Fold `Forestry.Deserialize/src/NamingPolicy.cs`, `IIgnorePropertyPolicy.cs`, `IIncludeFieldPolicy.cs` into the policy model (R10: Reshape)
- [ ] T066 [US4] Run `dotnet test` on both test projects: T055-T059 pass, US1 and US2 still green

**Checkpoint**: User Stories 1, 2 and 4 work.

---

## Phase 6: User Story 3 - Synchronous or asynchronous input (Priority: P3)

**Goal**: The same rows from a stream or pipe, asynchronously, however the bytes are split.

**Independent Test**: Document 1 through the pipe entry point and split at every byte position
(quickstart rows 6-7).

### Test shells for User Story 3 ⚠️ (T068 also needs XML multi-segment support, #24)

- [ ] T067 [US3] Test shell, quickstart 6 / US3-1, US3-2 / FR-009, FR-010: document 1 through `DeserializeAsync(PipeReader, …)` gives identical rows, and the first row is handed out at its end token, before the second `Log` is read, in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US3.cs`
- [ ] T068 [US3] Test shell, quickstart 7 / US3-3 / FR-010: document 1 written to a pipe split at every byte position gives identical rows, in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US3.cs`
- [ ] T069 [US3] **Test review gate**: the developer reviews T067-T068 (`Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US3.cs`) on the board before T070 (C4)

### Implementation for User Story 3

- [ ] T070 [US3] Implement `RowAsyncEnumerator` in `Forestry.Deserialize/src/Reading/RowAsyncEnumerator.cs` with `Forestry.Deserialize/src/Reading/PipeReaderBuffering.cs` (kept, internal): `MoveNextAsync` refills, calls `Read` on the unconsumed bytes with the final-block flag from `IsCompleted`, advances the pipe by the bytes consumed (even 0), refills again on `Partial`, returns `false` on `None`; cancellation; `DisposeAsync` clears the state and releases the pipe's buffer, handing out no row (R2)
- [ ] T071 [US3] Implement the asynchronous entry points in `Forestry.Deserialize/src/Deserialization.Pipe.cs`: `DeserializeAsync(PipeReader, …)` and `DeserializeAsync(Stream, …)` (via `PipeReader.Create`); validate and lock before returning; one enumeration only (R2, R3)
- [ ] T072 [US3] Implement `Forestry.Deserialize.Xml/src/XmlRowAsyncEnumerator.cs` delegating `Read` to `XmlRowReading`, and supply it from `Forestry.Deserialize.Xml/src/DeserializeXmlOptions.cs` (R2)
- [ ] T073 [US3] Extend `Forestry.Deserialize.Xml/src/XmlRowReading.cs` for multi-segment buffers (needs #24): a value split across segments is joined into the scratch buffer; an incomplete value returns `Partial` without consuming it (R2)
- [ ] T074 [US3] Run `dotnet test` on both test projects: T067-T068 pass, all earlier stories still green

**Checkpoint**: All user stories work.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T075 Test shell, quickstart 15 / SC-003 / C1: generated documents with 1,000 vs 1,000,000 `Log` rows, rows discarded: memory does not grow with the count, in `Forestry.Deserialize.Xml/test/MemoryTests.cs`
- [ ] T076 **Test review gate**: the developer reviews T075 (`Forestry.Deserialize.Xml/test/MemoryTests.cs`) on the board before T077 (C4)
- [ ] T077 Make T075 pass: no reference kept to handed-out rows in `Forestry.Deserialize/src/Reading/DeserializationState.cs`, scratch buffers returned to the pool in `Forestry.Deserialize.Xml/src/XmlRowReading.cs`
- [ ] T078 [P] Add XML doc comments to every public member of `Forestry.Deserialize/src/` until no CS1591 warning is left (D0, R9 option B)
- [ ] T079 [P] Rewrite `Forestry.Deserialize/README.md` (replacing the superseded banner) as the how-to page for this slice: register a row class, deserialize XML sync and async, `AsType<T>()`, attach a policy, with the example from `contracts/public-api.md` (D0)
- [ ] T080 Run every scenario in `specs/001-walking-skeleton/quickstart.md` (rows 1-15) and record the result

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies; the build is red from T002 until T017
- **Foundational (Phase 2)**: depends on Setup; blocks every story's shells
- **US1 (Phase 3)**: depends on Foundational; XML shells pass only after BLOCKING.md phase 2
- **US2 (Phase 4)**: depends on Foundational; T050 uses US1's XML path (T045-T047)
- **US4 (Phase 5)**: depends on Foundational; T055-T057 use US1's XML path; T064 builds on T039
- **US3 (Phase 6)**: depends on US1 (T042-T046); T068 and T073 need #24
- **Polish (Phase 7)**: depends on the stories it measures (T075 needs US1)

### Within Each Story

- Test shells, then the **test review gate**, then implementation (C4)
- Shells fail until their implementation tasks are done
- Definitions (T039-T041) before pipelines (T042) before enumerators (T043) before entry points (T044)
- Core before XML (T045-T047 use the core)

### Parallel Opportunities

- Setup: T002-T006 together
- Foundational: T008-T011 and T016 together
- US1: fixtures T018 and T019 together; T030 alongside the XML shells; T038 and T039 together
- US2: T049 and T050 together
- US4: T054, T058, T059 together; T063 alongside T062
- Polish: T078 and T079 together

---

## Parallel Example: User Story 1

```text
Task: "Add the fixture Log : Row in Forestry.Deserialize.Xml/test/Fixtures/Log.cs"            (T018)
Task: "Add core fixtures in Forestry.Deserialize/test/Fixtures/"                              (T019)

# After the review gate (T037):
Task: "Implement the built-in converters in Forestry.Deserialize/src/Converters/BuiltIn/"     (T038)
Task: "Implement registration and locking in Forestry.Deserialize/src/DeserializeOptions.cs"  (T039)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1: Setup
2. Phase 2: Foundational (surface compiles)
3. Phase 3: US1 shells, review gate, implementation
4. **STOP and VALIDATE**: core tests pass now; XML tests pass once BLOCKING.md phase 2 is done
5. Demo: document 1 to two `Log` rows

### Incremental Delivery

1. Setup + Foundational: surface ready
2. US1: rows from a document, sync (MVP)
3. US2: `Type` / `AsType<T>()`
4. US4: policies, precedence, read-only options
5. US3: async input and split buffers (with #24)
6. Polish: memory test, XML docs, how-to page, quickstart run

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- Each review gate is a board task the developer closes (C4, C5); names marked Deferred
  (implementation) are confirmed in code review
- Commit after each task or logical group, against the board item
- The work for this slice was larger than first estimated; the reasons are in research.md (R0-R15)
