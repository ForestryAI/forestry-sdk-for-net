---

description: "Task list for the walking skeleton (spec 001)"
---

# Tasks: Walking skeleton

**Input**: Design documents from `specs/001-walking-skeleton/` (plan.md, spec.md, research.md,
data-model.md, contracts/public-api.md, quickstart.md) and `.specify/memory/constitution.md` 2.0.0.

**Prerequisites**: every research item is Confirmed, Rejected or Deferred (research.md header).

**Tests**: Required (constitution C4). In every story phase: test shells first, one per acceptance
scenario, quickstart row or flow; then a **test review gate** (the developer reviews the shells on the
board before any code); then implementation. No code is written before a story's shells: not even
declarations. Each story declares the public surface it needs as part of its own implementation,
after its gate, so a story's shells may not compile until then (developer, 2026-10-30). Setup
(Phase 1) changes the build setting and removes the old design before any shell; it adds no
behavior a test could assert, so it is accepted as setup, not code under C4 (developer,
2026-10-30).

- **XML acceptance shells are theories over both entry points** (sync and async), so every
  acceptance scenario checks that sync and async agree (SC-001). They run a document through the
  test helper `EntryPoints` (`Forestry.Deserialize.Xml/test/EntryPoints.cs`), whose async branch
  throws until US3, so no test references `DeserializeAsync` before it exists; the async rows are
  marked skipped (`[InlineData(…, Skip = "US3")]`). Sync alone is enough until then. T066 switches
  the helper's async branch to `DeserializeAsync` and removes the skips (developer, 2026-10-30).
- **A shell that does not compile breaks its whole test project** until its story's implementation
  (e.g. US4's shells in `OptionsTests.cs`); accepted (developer, 2026-10-30).
- **Tests read rows with a plain C# cast** (`(Log)row`); `AsType<T>()` is US2's (developer,
  2026-10-30).

**Organization**: Phases by user story in priority order: US1 (P1), US2 (P2), US4 (P2), US3 (P3).
There is no Foundational phase: C4 puts every story's shells before any of its code.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: The user story the task belongs to (US1, US2, US3, US4)
- Paths are relative to `sdk/deserialize/`: the core is `Forestry.Deserialize/`, the media package
  is `Forestry.Deserialize.Xml/` (plan.md, Project Structure)

## External prerequisites

- **XML reader element and text reading** (`Forestry.Deserialize.Xml/BLOCKING.md`, phase 2): every
  XML acceptance test (`Forestry.Deserialize.Xml/test/WalkingSkeletonTests.*.cs`, T010-T020, T045,
  T050-T052, T062-T063, T070) and the XML reading tasks (T041, T068) need it before they can pass.
  Their shells can be written and reviewed now. Core-only tests (`Forestry.Deserialize/test/`) do not
  depend on it.
- **XML reader multi-segment support** (`BLOCKING.md` "not part of this pass", issue #24): needed by
  quickstart row 7 (T063) and T068, buffers split at arbitrary byte positions.

## Not in this slice

Deferred in research.md, so no tasks here:

- **Spec 002/003**: constructor-parameter fail-fast cases (R5); parameter resolution from open or
  retained rows, retention of completed rows, the fail-fast policy (R5, R7); the full policy
  catalogue and slot order (R7); R7's edge cases; the XML package's association approach beyond
  name only (R13 B/C); `DeserializerKind` / `TypeDefinitionKind` (R10); adding constructor parameter
  types automatically and transitively, and rejecting a parameter type not derived from `Row` (R5,
  R12); attaching policies to parameter definitions (FR-012); if the POC needs it, constructor
  parameters bound to a member value of another row type, with property-definition getters (R5).
- **Key-references slice (spec 002-005)**: references by key (R5): the store slot at completion,
  the resolve slot in the member pipeline, the reference attribute (R7).
- **Media dimensions**: the `FieldContext.Dimensions` member and the dimensions builder (R15); the
  XML media sets no dimensions yet (developer, 2026-10-30).
- **Implementation (names, confirmed in code review)**: attribute names (R5, R8), `Pipeline(Type)`
  (R6), member names for adding row types (R12), the converter name "Converter" or "Deserializer"
  (R14). The names used below are the research examples.
- **Complex-members slice (spec 002-005)**: complex (owned) members, nested classes read from a
  structure in the document, and collections (R16).
- **Later slice**: the S1 fail-fast limits (FR-014).

**Note (R9)**: documentation option B is set up in T001. Switching to option A (missing XML docs
fail the build) is not a task here: it becomes mandatory with the first real StanForD document
deserialized end to end (C6), in the slice that delivers it.

## Flow coverage

The flows deserialization has to get right, where each is tested, and which slice owns it
(developer, 2026-10-30):

| Flow | Slice | Test shells |
|---|---|---|
| 1. Type definitions created from a specific options instance (per options, never global) | 001 | T021-T026 (FR-008), T020 (one marked constructor used), T027 (isolation), T018 (`[Row]` name) |
| 2. Property definitions per type definition (properties and fields, names) | 001 | T016 (field member), T017 (`[Field]` name), T022 (member without a converter rejected) |
| 3. Effective converter per property definition | 001 | T014 (built-in failure), T013 (empty element), T019 (XML default), T028 (options over built-in) |
| 4. Object construction with parameters, only of row types | 002/003 | Deferred |
| 5. Parameter types added automatically as type definitions | 002/003 | Deferred |
| 6. Key references: attributes or options establish the referenced type definition, its keyed store and its lookup | key-references slice | Deferred |
| 7. Policies per slot, and execution order | 001 (minimal order) | T050 (defaults), T051 (attribute policy), T052 (order across slots), T053 (precedence), T054 (read-only options) |

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Documentation build setting and removal of the old design (R0, R10). The build is
expected to be red from T002 until US1's implementation (T043).

- [X] T001 Enable XML documentation, option B (R9): set `GenerateDocumentationFile` to `true` and keep CS1591 a warning (`WarningsNotAsErrors` includes `CS1591`) in `Forestry.Deserialize/src/Forestry.Deserialize.csproj`
- [X] T002 [P] Delete the old Value model (R10, Confirmed): `Forestry.Deserialize/src/Value.cs`, `Value.OfType.cs`, `NullableValue.OfType.cs`, `DeserializedValue.OfType.cs`, `Dimension.cs`, `Dimension.Defaults.cs`, `Dimensions.cs`
- [X] T003 [P] Delete the old reading layer (R10, Confirmed): `Forestry.Deserialize/src/ValueAsyncEnumerable.cs`, `ValueAsyncEnumerator.cs`, `ReadErrorHandling.cs`, `Reading/IBuffering.cs`, `Reading/ReadingStatus.cs`, `Reading/IReaderState.cs`, `Reading/ReaderPath.cs`, `Reading/ReaderPosition.cs`
- [X] T004 [P] Delete the XML vocabulary from the core (C0, R10): `Forestry.Deserialize/src/Attributes/ElementAttribute.cs`, `Forestry.Deserialize/src/Attributes/CollectionAttribute.cs`
- [X] T005 [P] Delete the old core tests and fixtures, which test the rejected design (R0, R11): `Forestry.Deserialize/test/DeserializationPipeTests.cs`, `TypeDefinitionReflectionTests.cs`, `TestingDeserializeOptions.cs`, `TestingDeserializerProvider.cs`, `TestingMachine.cs`, `TestingObjectDeserializer.cs`, `TestingPropertyDefinition.cs`, `TestingTypeDefinition.cs`, `TestingValue.cs`, `TestingValueDeserializer.cs`
- [X] T006 [P] Delete the XML package's old deserialization layer (R0): `Forestry.Deserialize.Xml/src/Deserializers/` (`ObjectDeserializer.cs`, `XmlDeserializer.cs`, `Value/`) and `Forestry.Deserialize.Xml/src/Deserialization.Property.cs`; the reader under `Forestry.Deserialize.Xml/src/Reading/` stays
- [X] T007 Rename `Forestry.Deserialize/src/ReflextionExtensions.cs` to `ReflectionExtensions.cs`, including the class name and its callers (R10)

---

## Phase 2: User Story 1 - Shred a flat class into rows (Priority: P1) 🎯 MVP

**Goal**: A document with `Log` occurrences becomes one `Log` row per occurrence, in order.

**Independent Test**: Small hand-written XML documents and `Log : Row { int LogKey; decimal LogVolume }`
added to the XML options; check the rows (quickstart rows 1-3, 12-14).

### Test shells for User Story 1 ⚠️ (XML shells need BLOCKING.md phase 2 to pass)

- [X] T008 [P] [US1] Add the fixture `Log : Row` with `int LogKey` and `decimal LogVolume` in `Forestry.Deserialize.Xml/test/Fixtures/Log.cs`, and the helper `Forestry.Deserialize.Xml/test/EntryPoints.cs` (an entry-point mode, sync or async; sync calls `Deserialization.Deserialize`, async throws `NotSupportedException` until T066)
- [X] T009 [P] [US1] Add core fixtures in `Forestry.Deserialize/test/Fixtures/`: `Log.cs` (as T008), `LogWithNestedProperty.cs` (a property of a nested class with no converter), `LogWithParameters.cs` (only a public constructor, with a row-typed parameter), `Stem.cs` (that parameter's row type), `LogWithTwoPublicConstructors.cs` (two public constructors, none marked), `LogWithTwoMarkedConstructors.cs` (two constructors marked `[RowConstructor]`), `NotARow.cs` (a class not extending `Row`)
- [X] T010 [US1] Test shell, quickstart 1 / US1-1 / FR-003: `<Root><Log><LogKey>1</LogKey><LogVolume>0.59</LogVolume></Log><Log><LogKey>2</LogKey><LogVolume>0.50</LogVolume></Log></Root>` gives two rows in order, members as given (theory over both entry points; rows read by cast), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [X] T011 [US1] Test shell, quickstart 2 / US1-2 / FR-005: document 1 plus elements outside any `Log` and an `<Unknown>` element inside one gives the same two rows (theory over both entry points), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [X] T012 [US1] Test shell, quickstart 3 / US1-3: `<Root />` gives no rows and no error (theory over both entry points), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [X] T013 [US1] Test shell, spec edge case: an empty element for a member (`<LogVolume />`) leaves the member null, or its type's default where null is not allowed (theory over both entry points), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [X] T014 [US1] Test shell, quickstart 12 / FR-007: `<LogKey>abc</LogKey>` throws, naming the member, its place in the document and `Log` (theory over both entry points), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [X] T015 [US1] Test shell, quickstart 14 / spec edge case: a document cut off inside an open `Log` throws (from the media's reader) and the open row is never handed out (theory over both entry points), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [X] T016 [US1] Test shell, flow 2 (property definitions): a public field member is set like a property: fixture `LogWithFieldMember : Row` with public field `decimal LogVolume` in `Forestry.Deserialize.Xml/test/Fixtures/LogWithFieldMember.cs`; document 1 gives the field values (compiled-expression setter, R14), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [X] T017 [US1] Test shell, flow 2: `[Field("Volume")]` on `LogVolume` in fixture `Forestry.Deserialize.Xml/test/Fixtures/LogWithRenamedField.cs`: `<Volume>0.59</Volume>` sets `LogVolume` and `<LogVolume>` is ignored (R8), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [X] T018 [US1] Test shell, flow 1/2: `[Row("Tree")]` on fixture `Forestry.Deserialize.Xml/test/Fixtures/TreeLog.cs`: `<Tree>` occurrences give rows and `<Log>` occurrences are ignored (R8), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [X] T019 [US1] Test shell, flow 3 (media default converter): fixture `Forestry.Deserialize.Xml/test/Fixtures/LogWithFlag.cs` with `bool TopSawing`: `<TopSawing>1</TopSawing>` gives `true` and `<TopSawing>0</TopSawing>` gives `false` (XML default converter, R14), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [X] T020 [US1] Test shell, FR-008 / R5 rule 1 (the valid case): fixture `Forestry.Deserialize.Xml/test/Fixtures/LogWithMarkedConstructor.cs` with an unmarked public constructor taking a parameter and a private parameterless one marked `[RowConstructor]`: document 1 gives rows built by the marked constructor (exactly one marked is used, whatever its accessibility), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [X] T021 [P] [US1] Core test shell, quickstart 13 / FR-008: no row type added throws before any byte is read, in `Forestry.Deserialize/test/DefinitionValidationTests.cs`
- [X] T022 [US1] Core test shell, FR-008: `LogWithNestedProperty` throws at the call (before any byte is read), since no converter handles `Measurement`, in `Forestry.Deserialize/test/DefinitionValidationTests.cs`
- [X] T023 [US1] Core test shell, FR-008: `LogWithParameters(Stem stem)` throws because its chosen constructor has a parameter at all (this slice), not because of the parameter's type; from spec 002/003 row-typed parameters are allowed and this test becomes an "accepted" one (R5), in `Forestry.Deserialize/test/DefinitionValidationTests.cs`
- [X] T024 [US1] Core test shell, FR-008: `LogWithTwoPublicConstructors` throws (no constructor can be chosen), in `Forestry.Deserialize/test/DefinitionValidationTests.cs`
- [X] T025 [US1] Core test shell, FR-008: `LogWithTwoMarkedConstructors` throws because more than one constructor is marked `[RowConstructor]` (a row type uses exactly one), not because of its second constructor's parameter, in `Forestry.Deserialize/test/DefinitionValidationTests.cs`
- [X] T026 [US1] Core test shell, FR-008: `options.Rows.Add(typeof(NotARow))` throws when added, naming the type (R12), in `Forestry.Deserialize/test/DefinitionValidationTests.cs`
- [X] T027 [US1] Core test shell, flow 1 (options isolation, S0, R5): `Log` added to options A is in A's row type lookup and not in options B's, built from a second options instance, in `Forestry.Deserialize/test/OptionsTests.cs`
- [X] T028 [US1] Core test shell, flow 3 (effective converter): a `Converter<int>` added to the options becomes the effective converter of `Log.LogKey` instead of the built-in (precedence: options, attributes, media defaults, built-ins; R14), with the test converter in `Forestry.Deserialize/test/Fixtures/MarkingInt32Converter.cs`, in `Forestry.Deserialize/test/ConverterResolutionTests.cs`
- [X] T029 [US1] **Test review gate**: the developer reviews shells T008-T028 (`Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`, `Forestry.Deserialize/test/DefinitionValidationTests.cs`, `Forestry.Deserialize/test/OptionsTests.cs`, `Forestry.Deserialize/test/ConverterResolutionTests.cs`, fixtures) on the board and closes the review task before T030 starts (C4)

### Implementation for User Story 1 (declares the surface it needs, then implements it)

- [ ] T030 [P] [US1] Declare `RowAttribute` (class; optional name), `FieldAttribute` (property or field; optional name) and `RowConstructorAttribute` (constructor; `AllowMultiple = false`) in `Forestry.Deserialize/src/Attributes/RowAttribute.cs`, `FieldAttribute.cs`, `RowConstructorAttribute.cs` (R5, R8)
- [ ] T031 [P] [US1] Declare the abstract `Row` in `Forestry.Deserialize/src/Rows/Row.cs` (its members `Type`, `AsType<T>()`, `Is<T>()` are US2's) and implement `Field` (metadata about one member: member info, media name, C# type, converter; never a value, R4) in `Forestry.Deserialize/src/Rows/Field.cs`
- [ ] T032 [P] [US1] Implement the reading types in `Forestry.Deserialize/src/Reading/`: `RowState.cs` (`None`, `Partial`, `Complete`), `FieldContext.cs` (`readonly ref struct` with `int Depth`, `ReadOnlySpan<byte> Name`, `ReadOnlySpan<byte> Path`; `Dimensions` is Deferred), `FieldContextExtensions.cs` (UTF-8 span to `string`) (R2, R15)
- [ ] T033 [US1] Delete `Forestry.Deserialize/src/Deserializers/` (`Deserializer.cs`, `Deserializer.OfType.cs`, `DeserializerFactory.cs`, `IDeserializerProvider.cs`; R10) and implement the converters in `Forestry.Deserialize/src/Converters/`: `Converter<T>` with `T Convert(ReadOnlySpan<byte> utf8)`, a converter factory, and the built-ins in `Converters/BuiltIn/` for every primary type of this slice (text, boolean, integer numbers, decimal numbers, date/time): one generic converter for `T : IUtf8SpanParsable<T>` with invariant culture, plus `StringConverter` and `BooleanConverter`; a failed conversion throws naming the member, its place (the `FieldContext` path, via `FieldContextExtensions`) and the class (FR-007, R14) (depends on T032)
- [ ] T034 [US1] Implement the options in `Forestry.Deserialize/src/DeserializeOptions.cs` and `DeserializeOptions.Caching.cs`: abstract; `Rows.Add<T>() where T : Row` returning the row collection for chaining, and `Rows.Add(params Type[] types)` throwing for a type not extending `Row`; these types are marked as explicitly added (only they are handed out, R12); no assembly scan; converter registration (R14): the defaults (every primary converter of T033, and the type definition as the object converter) and the developer's converters, in `ObservableList<T>`; abstract creation of the media's `RowEnumerator` over `ReadOnlyMemory<byte>`; on first use, lock the options and build the per-options frozen row type lookup (`Type` to type definition), never global (R5, R6, R12, S0) (depends on T033)
- [ ] T035 [US1] Implement type definitions in `Forestry.Deserialize/src/Definitions/TypeDefinition.cs`, `TypeDefinition.OfType.cs`, `TypeDefinition.Properties.cs`: choose the constructor ("exactly one marked: use it, whatever its accessibility; none marked and exactly one public: use it; otherwise fail fast; more than one marked: fail fast"), build its delegate once; one property definition per public property or field; a member is accepted when a converter resolves for its type (built-in for "text, boolean, integer numbers, decimal numbers and date/time", media default, or added to the options; R14), otherwise rejected; reject per FR-008 before any byte is read (depends on T030, T034)
- [ ] T036 [US1] Implement `PropertyDefinition<T>` in `Forestry.Deserialize/src/Definitions/PropertyDefinition.cs` and `PropertyDefinition.OfType.cs`: converter resolved once by `T` ("options, attributes, media defaults, built-ins"); setter `Action<Row, T>` built once (setter delegate for a property, compiled expression for a field); non-generic `ReadAndSet(Row row, ReadOnlySpan<byte> utf8)` dispatching into `T`, no boxing; empty text sets `null` where the member allows null, else its type's default, without calling the converter; per-definition `Field` metadata (R14) (depends on T033, T035)
- [ ] T037 [US1] Implement the pipelines in their minimal slot order in `Forestry.Deserialize/src/Reading/DeserializationState.cs` and `Forestry.Deserialize/src/Reading/RowPipelines.cs`: `Pipeline<T>()` constructs (no parameters in this slice) and pushes on the open-row stack; `Pipeline(property or field, utf8, context)` converts and sets on the top open row; `Pipeline()` completes the top row, hands it out, pops it and keeps no reference; a type not in the lookup throws (S0), a member not of the top row's type throws, `Pipeline()` with no open row throws (R2, R6, R7) (depends on T036)
- [ ] T038 [US1] Declare and implement `RowEnumerator` (`IEnumerator<Row>`) in `Forestry.Deserialize/src/Reading/RowEnumerator.cs`: `protected abstract RowState Read(ReadOnlySequence<byte> bytes, bool isFinalBlock, out Row? row, out long consumed)`; protected `Pipeline<T>() where T : Row`, `Pipeline(PropertyInfo property, ReadOnlySpan<byte> utf8, in FieldContext context)`, `Pipeline(FieldInfo field, ReadOnlySpan<byte> utf8, in FieldContext context)` (two overloads, one internal path), `Pipeline()`, delegating to `RowPipelines`; `MoveNext` wraps the memory in a single-segment `ReadOnlySequence<byte>` (no copy), calls `Read` with the final-block flag set, sets `Current` on `Complete`, returns `false` on `None`; `Reset()` throws `NotSupportedException`; `Dispose` clears the `DeserializationState` and hands out no row (R2, R6) (depends on T037)
- [ ] T039 [US1] Implement the synchronous entry points in `Forestry.Deserialize/src/Deserialization.cs`, `Deserialization.String.cs` (`Deserialize(string, DeserializeOptions)`, encoded to UTF-8) and `Deserialization.Memory.cs` (replaces `Deserialization.Span.cs`; `Deserialize(ReadOnlyMemory<byte>, DeserializeOptions)`): build definitions and lock the options before returning (FR-008 errors throw at the call); return an `IEnumerable<Row>` over the media's `RowEnumerator` that can be enumerated once (a second `GetEnumerator()` throws) (R2, R3) (depends on T038)
- [ ] T040 [US1] Reshape `Forestry.Deserialize.Xml/src/DeserializeXmlOptions.cs` to the new `DeserializeOptions` and implement association, built once per options (reflection only then): an element named like the class (or its `[Row]` name) maps to a delegate calling `Pipeline<T>()`; a child element named like a member (or its `[Field]` name) maps to its `PropertyInfo` / `FieldInfo`; registers the XML default converters (`bool` accepts `true`, `false`, `1`, `0`, as XML Schema does); supplies `XmlRowEnumerator` (R6, R8, R13 A, R14)
- [ ] T041 [US1] Implement `Forestry.Deserialize.Xml/src/XmlRowReading.cs` (needs BLOCKING.md phase 2): rebuild `Utf8XmlReader` from its saved state each call; the start of an associated element calls `Pipeline<T>()`; the text of an associated member calls the member `Pipeline` overload with depth, name and path, resolving escapes and entities into a scratch buffer (`stackalloc` for short values, rented `ArrayPool<byte>` for long ones); the end element of an open row calls `Pipeline()` and returns `Complete`; unassociated data is skipped; the closed root returns `None`; an incomplete grammar at the final block throws; bytes consumed are reported even when 0 (R2)
- [ ] T042 [US1] Implement `Forestry.Deserialize.Xml/src/XmlRowEnumerator.cs` (`: RowEnumerator`) delegating `Read` to `XmlRowReading` (R2)
- [ ] T043 [US1] Build both packages green (`dotnet build` of `Forestry.Deserialize/Forestry.Deserialize.slnx` and `Forestry.Deserialize.Xml/Forestry.Deserialize.Xml.slnx`; `DeserializerKind` and `TypeDefinitionKind` stay as they are, Deferred, except removing references that no longer compile) and run `dotnet test` on `Forestry.Deserialize/test/Forestry.Deserialize.Tests.csproj` and `Forestry.Deserialize.Xml/test/Forestry.Deserialize.Xml.Tests.csproj`: T021-T028 pass; the sync cases of T010-T020 pass (async cases skipped until T066)

**Checkpoint**: User Story 1 works end to end with the synchronous entry point (MVP).

---

## Phase 3: User Story 2 - Work with rows without knowing their class (Priority: P2)

**Goal**: Rows come out as `Row`; the developer asks the type and casts to it.

**Independent Test**: Rows from Story 1: `Type`, `AsType<Log>()`, `AsType<Other>()`, a missing member
(quickstart rows 4-5).

### Test shells for User Story 2 ⚠️

- [ ] T044 [P] [US2] Core test shell, quickstart 4 / US2-1..3 / FR-006: a `Log` row built directly reports `Type` `Log`; `AsType<Log>()` gives 1 / 0.59; `AsType<Other>()` throws naming both classes; `Is<Log>()` is true, in `Forestry.Deserialize/test/RowTests.cs`
- [ ] T045 [P] [US2] Test shell, quickstart 5 / US2-4: a `Log` without `<LogVolume>` leaves `LogVolume` at its default (theory over both entry points), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US2.cs`
- [ ] T046 [US2] **Test review gate**: the developer reviews T044-T045 (`Forestry.Deserialize/test/RowTests.cs`, `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US2.cs`) on the board before T047 (C4)

### Implementation for User Story 2

- [ ] T047 [US2] Declare and implement `Row`'s members in `Forestry.Deserialize/src/Rows/Row.cs`: `Type` returns `GetType()`; `AsType<T>()` is a checked cast throwing `InvalidCastException` naming both classes; `Is<T>()` (R4)
- [ ] T048 [US2] Run `dotnet test` on both test projects: T044 and the sync case of T045 pass (async skipped), US1 still green

**Checkpoint**: User Stories 1 and 2 work.

---

## Phase 4: User Story 4 - Change behavior with policies (Priority: P2)

**Goal**: Pipeline slots are policy points; policies attach by attribute and through the options,
options win; options are read-only once used.

**Independent Test**: A recording test policy attached by attribute, by options, and by both
(quickstart rows 8-11).

### Test shells for User Story 4 ⚠️

- [ ] T049 [P] [US4] Add a recording test policy (records each call of its slot, in order) in `Forestry.Deserialize/test/Fixtures/RecordingPolicy.cs` and `Forestry.Deserialize.Xml/test/Fixtures/RecordingPolicy.cs`
- [ ] T050 [US4] Test shell, quickstart 8 / US4-1 / FR-011: without policies, document 1 gives the defaults: construct and push at start, convert and set members, hand out and pop at the media's end token, no reference kept (theory over both entry points), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US4.cs`
- [ ] T051 [US4] Test shell, quickstart 9 / US4-2 / FR-012: the recording policy attached to `Log` by attribute acts once per row, in document order (theory over both entry points), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US4.cs`
- [ ] T052 [US4] Test shell, flow 7 (order across slots): a recording policy in every slot of both pipelines sees, per row, construct, then convert and set for each member, then hand out, in that order (R7 minimal order), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US4.cs`
- [ ] T053 [P] [US4] Core test shell, quickstart 10 / US4-3 / FR-012: the same slot attached by attribute and by options: only the options' policy acts (precedence "option for that property or parameter, option for that type, attribute on the property or parameter, attribute on the type, options-wide default, built-in default"), in `Forestry.Deserialize/test/PolicyPrecedenceTests.cs`
- [ ] T054 [P] [US4] Core test shell, quickstart 11 / US4-4 / FR-013: changing options (a policy, a row type, a converter) after first use throws, in `Forestry.Deserialize/test/OptionsTests.cs`
- [ ] T055 [US4] **Test review gate**: the developer reviews T049-T054 (`Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US4.cs`, `Forestry.Deserialize/test/PolicyPrecedenceTests.cs`, `Forestry.Deserialize/test/OptionsTests.cs`, `RecordingPolicy.cs` fixtures) on the board before T056 (C4)

### Implementation for User Story 4

- [ ] T056 [US4] Declare and implement the policy surface and built-in defaults in `Forestry.Deserialize/src/Policies/` and a policy attribute base in `Forestry.Deserialize/src/Attributes/PolicyAttribute.cs`: type pipeline slots construct and hand out, around the fixed push, complete and pop; member pipeline slots convert and set; the Field metadata slot, off by default; wire `RowPipelines` to call the resolved policy per slot (R7)
- [ ] T057 [US4] Implement precedence resolution once per definition when definitions are built, in `Forestry.Deserialize/src/Policies/PolicyResolver.cs`, with the six levels quoted in T053 (R7)
- [ ] T058 [P] [US4] Implement attachment by attribute through `Forestry.Deserialize/src/Attributes/PolicyAttribute.cs` and by options through policy registration in `Forestry.Deserialize/src/DeserializeOptions.cs`, for type and property definitions (parameter definitions with spec 002/003; R7, FR-012)
- [ ] T059 [US4] Implement read-only options in `Forestry.Deserialize/src/DeserializeOptions.cs` and `Forestry.Deserialize/src/ObservableList.OfType.cs`: once locked (T034), any change throws (FR-013, R6)
- [ ] T060 [US4] Fold `Forestry.Deserialize/src/NamingPolicy.cs`, `IIgnorePropertyPolicy.cs`, `IIncludeFieldPolicy.cs` into the policy model (R10: Reshape)
- [ ] T061 [US4] Run `dotnet test` on both test projects: T053-T054 and the sync cases of T050-T052 pass (async skipped), US1 and US2 still green

**Checkpoint**: User Stories 1, 2 and 4 work.

---

## Phase 5: User Story 3 - Synchronous or asynchronous input (Priority: P3)

**Goal**: The same rows from a stream or pipe, asynchronously, however the bytes are split.

**Independent Test**: Document 1 through the pipe entry point and split at every byte position
(quickstart rows 6-7); the async cases of every earlier theory.

### Test shells for User Story 3 ⚠️ (T063 also needs XML multi-segment support, #24)

- [ ] T062 [US3] Test shell, quickstart 6 / US3-1, US3-2 / FR-009, FR-010: document 1 through `DeserializeAsync(PipeReader, …)` gives identical rows, and the first row is handed out at its end token, before the second `Log` is read, in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US3.cs`
- [ ] T063 [US3] Test shell, quickstart 7 / US3-3 / FR-010: document 1 written to a pipe split at every byte position gives identical rows, in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US3.cs`
- [ ] T064 [US3] **Test review gate**: the developer reviews T062-T063 (`Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US3.cs`) on the board before T065 (C4)

### Implementation for User Story 3

- [ ] T065 [US3] Declare and implement `RowAsyncEnumerator` (`IAsyncEnumerator<Row>`) in `Forestry.Deserialize/src/Reading/RowAsyncEnumerator.cs`, with the same protected `Read` and `Pipeline` overloads as `RowEnumerator`, using `Forestry.Deserialize/src/Reading/PipeReaderBuffering.cs` (kept, internal): `MoveNextAsync` refills, calls `Read` on the unconsumed bytes with the final-block flag from `IsCompleted`, advances the pipe by the bytes consumed (even 0), refills again on `Partial`, returns `false` on `None`; cancellation; `DisposeAsync` clears the state and releases the pipe's buffer, handing out no row; add the abstract creation of the media's `RowAsyncEnumerator` over a `PipeReader` to `Forestry.Deserialize/src/DeserializeOptions.cs` (R2)
- [ ] T066 [US3] Implement the asynchronous entry points in `Forestry.Deserialize/src/Deserialization.Pipe.cs`: `DeserializeAsync(PipeReader, DeserializeOptions, CancellationToken)` and `DeserializeAsync(Stream, DeserializeOptions, CancellationToken)` (via `PipeReader.Create`); validate and lock before returning; one enumeration only; then make the async branch of `Forestry.Deserialize.Xml/test/EntryPoints.cs` call `DeserializeAsync` and remove the `Skip = "US3"` marks from the theories (R2, R3)
- [ ] T067 [US3] Implement `Forestry.Deserialize.Xml/src/XmlRowAsyncEnumerator.cs` (`: RowAsyncEnumerator`) delegating `Read` to `XmlRowReading`, and supply it from `Forestry.Deserialize.Xml/src/DeserializeXmlOptions.cs` (R2)
- [ ] T068 [US3] Extend `Forestry.Deserialize.Xml/src/XmlRowReading.cs` for multi-segment buffers (needs #24): a value split across segments is joined into the scratch buffer; an incomplete value returns `Partial` without consuming it (R2)
- [ ] T069 [US3] Run `dotnet test` on both test projects: T062-T063 and the async cases of every earlier theory pass; all earlier stories still green

**Checkpoint**: All user stories work.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T070 Test shell, quickstart 15 / SC-003 / C1: generated documents with 1,000 vs 1,000,000 `Log` rows, rows discarded: memory does not grow with the count, in `Forestry.Deserialize.Xml/test/MemoryTests.cs`
- [ ] T071 **Test review gate**: the developer reviews T070 (`Forestry.Deserialize.Xml/test/MemoryTests.cs`) on the board before T072 (C4)
- [ ] T072 Make T070 pass: no reference kept to handed-out rows in `Forestry.Deserialize/src/Reading/DeserializationState.cs`, scratch buffers returned to the pool in `Forestry.Deserialize.Xml/src/XmlRowReading.cs`
- [ ] T073 [P] Add XML doc comments to every public member of `Forestry.Deserialize/src/` until no CS1591 warning is left (D0, R9 option B)
- [ ] T074 [P] Rewrite `Forestry.Deserialize/README.md` (replacing the superseded banner) as the how-to page for this slice: add a row class, deserialize XML sync and async, `AsType<T>()`, attach a policy, with the example from `contracts/public-api.md` (D0)
- [ ] T075 Run every scenario in `specs/001-walking-skeleton/quickstart.md` (rows 1-15) and record the result; confirm SC-004 on scenario 1: only creating the options, one `Rows.Add<Log>()`, one `Deserialize` call and one conversion per row

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies; the build is red from T002 until T043
- **US1 (Phase 2)**: depends on Setup; its shells are written before any code and may not compile
  until T030-T042; XML shells pass only after BLOCKING.md phase 2
- **US2 (Phase 3)**: depends on US1 (the `Row` it extends, the XML path for T045)
- **US4 (Phase 4)**: depends on US1 (the pipelines it attaches policies to); T059 builds on T034
- **US3 (Phase 5)**: depends on US1 (T037-T041); T063 and T068 need #24; completes the async cases
  of every earlier theory
- **Polish (Phase 6)**: depends on the stories it measures (T070 needs US1)

### Within Each Story

- Test shells, then the **test review gate**, then implementation (C4); implementation starts by
  declaring the surface the shells use
- Shells fail (or do not compile) until their implementation tasks are done
- US1: attributes, `Row`, reading types (T030-T032) before converters (T033) before options (T034)
  before definitions (T035-T036) before pipelines (T037) before the enumerator (T038) before the
  entry points (T039); core before XML (T040-T042)

### Parallel Opportunities

- Setup: T002-T006 together
- US1: fixtures T008 and T009 together; T021 alongside the XML shells; T030, T031, T032 together
- US2: T044 and T045 together
- US4: T049, T053, T054 together; T058 alongside T057
- Polish: T073 and T074 together

---

## Parallel Example: User Story 1

```text
Task: "Add the fixture Log : Row in Forestry.Deserialize.Xml/test/Fixtures/Log.cs"            (T008)
Task: "Add core fixtures in Forestry.Deserialize/test/Fixtures/"                              (T009)

# After the review gate (T029):
Task: "Declare RowAttribute, FieldAttribute, RowConstructorAttribute"                         (T030)
Task: "Declare Row and implement Field in Forestry.Deserialize/src/Rows/"                     (T031)
Task: "Implement the reading types in Forestry.Deserialize/src/Reading/"                      (T032)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1: Setup
2. Phase 2: US1 shells, review gate, then the surface and its implementation
3. **STOP and VALIDATE**: core tests pass now; XML sync cases pass once BLOCKING.md phase 2 is done
4. Demo: document 1 to two `Log` rows

### Incremental Delivery

1. Setup
2. US1: rows from a document, sync (MVP)
3. US2: `Type` / `AsType<T>()`
4. US4: policies, precedence, read-only options
5. US3: async input and split buffers (with #24); the async cases of every theory
6. Polish: memory test, XML docs, how-to page, quickstart run

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- Each review gate is a board task the developer closes (C4, C5); names marked Deferred
  (implementation) are confirmed in code review
- Commit after each task or logical group, against the board item
- The work for this slice was larger than first estimated; the reasons are in research.md (R0-R15)
- Rewritten after `/speckit-analyze` (2026-10-30): no Foundational phase (C1), theories over both
  entry points (E1), cast instead of `AsType<T>()` in US1 (F3), new shell T026 (E2), U1-U3 folded
  into T033, T034, T036, media dimensions Deferred (U4), SC-004 checked in T075 (E3). Second run:
  async theory rows skipped through `EntryPoints` until T066 (G1, G2), setup accepted (L1), shells
  breaking their project accepted (L2)
