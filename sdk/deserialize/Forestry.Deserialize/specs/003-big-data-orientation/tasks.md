---

description: "Task list for big data orientation (spec 003)"
---

# Tasks: Big data orientation

**Input**: Design documents from `specs/003-big-data-orientation/` (plan.md, spec.md, research.md
R32-R43, data-model.md, contracts/public-api.md, quickstart.md) and `.specify/memory/constitution.md`
3.1.0.

**Prerequisites**: every research item is Confirmed (research.md, 2026-11-19; R34 as a rejection).

**Board**: feature #125; story issues #126 (US1), #127 (US2), #128 (US3), each with its review gate.

**Tests**: Required (constitution C4), as in specs 001 and 002. In every story phase: test shells
first, one per acceptance scenario, quickstart row or edge case; then a **test review gate** (the
developer reviews the shells on the board before any code); then implementation. A story's shells
may not compile until its implementation declares the surface they call (accepted in 001 and 002).

- **Core tests need no document** (C3): the flat check and the public surface are tested in
  `Forestry.Deserialize/test`, locking the options as `DefinitionValidationTests` does today.
- **XML shells are theories over both entry points** (`[InlineData(EntryPoint.Sync)]`,
  `[InlineData(EntryPoint.Async)]`) through `Forestry.Deserialize.Xml/test/EntryPoints.cs`.
- **Each test reads its own local document**, one element per line, with `// Arrange`, `// Act`,
  `// Assert` comments (A4).
- **Performance gate at the end of every story**: `dotnet test -s Performance.runsettings` on both
  test projects.
- **Every package builds on main** (R40): a story's shells and implementation reach main together;
  Forestry.StanForD changes in the same story as the attributes it uses.

**Organization**: Phases by user story in priority order: US1 (P1), US2 (P1), US3 (P2). No Setup or
Foundational phase: the projects exist, and C4 puts every story's shells before any of its code.
The stories run in order, since all three touch `PropertyDefinition*.cs`, `DeserializeOptions*.cs`
and the XML package.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: The user story the task belongs to (US1, US2, US3)
- Paths are relative to `sdk/deserialize/`: the core is `Forestry.Deserialize/`, the XML format
  package is `Forestry.Deserialize.Xml/`, and Forestry.StanForD is `../stanford/Forestry.StanForD/`

## Not in this slice

- A converter attribute (spec Clarifications; it would live in Forestry.Deserialize without a
  prefix).
- Reading information known when the options lock (R36, after the POC).
- Whether `DeserializerKind` stays (spec Assumptions); only its documentation's words change.
- Forestry.StanForD's documentation and constitution wording (#118), and the content of the
  Forestry.Deserialize README beyond words and the row definition (#116).
- S1 limits other than the context's (depth, value length), carried from 001 (plan Complexity
  Tracking).

---

## Phase 1: User Story 1 - Rows are flat, and the package says so (Priority: P1) 🎯 MVP (#126)

**Goal**: Locking the options fails, once per row type, for every included member that is not a
single value or has no converter. Each property definition checks its member and resolves its own
converter (`ResolvedConverter`); the type definition gathers the failures (R32, R33).

**Independent Test**: A row type with a `List<int>` member fails to lock, naming the type and the
member; a row type with `int`, `string`, `Guid`, `DateTimeOffset` and nullable members locks and
gives its rows (quickstart 1, 2).

### Test shells for User Story 1 ⚠️

- [X] T001 [P] [US1] Fixtures in `Forestry.Deserialize/test/Fixtures/` (2026-11-19):
  - `FlatRow.cs`: `FlatRow`, one member of each single-value kind with a converter (R32): `bool`, `char`, `int`, `long`, `double`, `string`, `decimal`, `Guid`, `DateTimeOffset`, `int?`, `Guid?`, and `Measure`, a test struct implementing `IUtf8SpanFormattable` and `ISpanParsable<Measure>` (one `decimal` inside) with `MeasureConverter` added in the test; `SingleValuesWithoutConverters` with an enum `Grade`, `DateOnly` and `TimeSpan` (single values with no built-in converter today, found while writing the shells)
  - `NonFlatRows.cs`, one row type per kind in SC-001 (with `LogWithNestedProperty`, a `Measurement` class member, from spec 001): `LogWithRowMember` (a `Stem` member), `LogWithArray` (`int[]`), `LogWithList` (`List<int>`), `LogWithDictionary` (`Dictionary<string, int>`), `LogWithCompositeStruct` (`Coordinate`, two `int` fields and no text form), `LogWithConvertedComposite` (a `Measurement` member, plus `MeasurementConverter`, a `Converter<Measurement>` added in the test)
  - in `NonFlatRows.cs`: `LogWithCharsOnlyStruct` (`CharsOnlyMeasure`, implementing `ISpanFormattable` and `ISpanParsable<T>` but not `IUtf8SpanFormattable`); `LogWithTwoFailingMembers` (a `List<int>` member and `UnconvertedMeasure`, a struct implementing both interfaces for which no converter exists); `LogWithExcludedList` (a `List<int>` member named `ExcludedDiameters`, left out by spec 001's `ExcludingPolicy`, which excludes members whose name starts with `Excluded`)
- [X] T002 [US1] Test shell, US1-1 / quickstart 1: `FlatRow` locks (`MakeReadOnly`), and each of its 12 members has a property definition with a `ResolvedConverter` that `CanConvert` its type; and `SingleValuesWithoutConverters` fails only for lack of a converter, all three members in one message, never as a nested structure (R32, R33), in `Forestry.Deserialize/test/DefinitionValidationTests.cs`
- [X] T003 [US1] Test shell, US1-2 / US1-3 / US1-4 / SC-001 (quickstart 2, 3): a theory over `LogWithNestedProperty`, `LogWithRowMember`, `LogWithArray`, `LogWithList`, `LogWithDictionary` and `LogWithCompositeStruct`; locking throws `InvalidOperationException` whose message contains the row type, the member, the member's type and the flat-row rule, in `DefinitionValidationTests.cs`
- [X] T004 [US1] Test shell, US1-5 / FR-003 / SC-001 (quickstart 4): `LogWithConvertedComposite` with `MeasurementConverter` added to the options still fails to lock with `MemberTypeIsNestedStructure`'s message, in `DefinitionValidationTests.cs`
- [X] T005 [US1] Test shell, R32 / R33: `LogWithCharsOnlyStruct` fails to lock with `MemberTypeNotUtf8Formattable`'s message (it names the member and `IUtf8SpanFormattable`, not the flat-row rule), in `DefinitionValidationTests.cs`
- [X] T006 [US1] Test shell, R33 "one message per type": `LogWithTwoFailingMembers` throws once, and the one message names both members, the list as a nested structure and `UnconvertedMeasure` as having no converter, in `DefinitionValidationTests.cs`
- [X] T007 [US1] Test shell, R33 "ignored members" (quickstart 5): `LogWithExcludedList` with `ExcludingPolicy` locks, and its type definition has no property definition for the list, in `DefinitionValidationTests.cs`
- [X] T008 [US1] Change the existing test for `LogWithNestedProperty` (asserting "converter") to assert `MemberTypeIsNestedStructure`'s message, in `DefinitionValidationTests.cs` (R33 "existing tests affected"); renamed `Deserialize_ForANestedClassMember_ItShould_ThrowBeforeAnyByteIsRead`
- [X] T009 [P] [US1] Change `logKey.Converter` to `logKey.ResolvedConverter` in `Forestry.Deserialize/test/ConverterResolutionTests.cs` (R33)
- [X] T010 [US1] **Test review gate**: the developer reviews T001-T009 on the board and closes the review task before T011 (C4) (developer, 2026-11-19: passed with no changes; the missing built-in converters for enums, `DateOnly`, `TimeOnly` and `TimeSpan` noted as next after the READMEs)

### Implementation for User Story 1

- [X] T011 [P] [US1] Messages in `Forestry.Deserialize/src/Formatting.Resource.cs` (R33): `MemberTypeIsNestedStructure` (member, its type, the flat-row rule, and the two fixes: "copy the values onto the row as single values, or make it its own row type"), for a class, another row type, an array, a collection or a struct with no text form; `MemberTypeNotUtf8Formattable` (member, its type, and the fix: "implement `IUtf8SpanFormattable`, so rows can be written without a string per value"), for a value type implementing `ISpanFormattable` without `IUtf8SpanFormattable`; and a message for one row type's failures that lists each member's reason; and a `Throwing` method in `Forestry.Deserialize/src/Throwing.cs` that throws one `InvalidOperationException` for a row type and its list of failures (R33)
- [X] T012 [US1] Single-value check in `Forestry.Deserialize/src/Definitions/PropertyDefinition.cs`: a static check on a `Type` that, after unwrapping `Nullable<T>`, accepts exactly "a primitive (`Type.IsPrimitive`)", "`string`", "an enum", or "a value type that formats to UTF-8: implements `IUtf8SpanFormattable` (required), and parses from text: implements `IUtf8SpanParsable<TSelf>` or `ISpanParsable<TSelf>` (either)" (R32). It runs before the generic definition is created, since some member types cannot be a generic argument (R33 step 1)
- [X] T013 [US1] Configure and resolve in `PropertyDefinition.cs` and `Forestry.Deserialize/src/Definitions/PropertyDefinition.OfType.cs` (R33 steps 2-3):
  - `PropertyDefinition<T>` is created from the member alone and builds its setter as today
  - the base's `Configure(options)` calls `private protected abstract ResolveConverter(options)`; `PropertyDefinition<T>` implements it, resolving its typed `Converter<T>` through `DeserializeOptions.ResolveConverter(Type)` with no cast handed in from outside
  - the public `Converter` property becomes `ResolvedConverter`
  - creation reports a failure instead of throwing: "not a single value" from T012, or "no converter" from resolution
  - `Field` (removed in US2) is built from the resolved converter until then
- [X] T014 [US1] Gather failures in `Forestry.Deserialize/src/Definitions/TypeDefinition.cs`: `Create` decides inclusion through the definition policies, asks each included member's property definition to be created and configured, collects the failures, and throws once per row type through T011's `Throwing` method; it no longer resolves converters (R33 step 4)
- [X] T015 [US1] `Row` documentation (FR-004): the XML doc comment of `Row` in `Forestry.Deserialize/src/Rows/Row.cs` says a row is the unit enumerated, every member is a single value (C7), and it has nothing to do with row-based vs columnar formats
- [X] T016 [US1] Run `dotnet test` on `Forestry.Deserialize/test`, `Forestry.Deserialize.Xml/test` and `../stanford/Forestry.StanForD/test`: T002-T009 pass, every earlier test still green; then the performance gate on both deserialization test projects (depends on T011-T015) (2026-11-19: passed on net10.0: core 56, XML 180, StanForD 46 + 1 skipped (real documents folder not set), 0 build warnings; performance gate: XML 4 of 4, the core project has no performance tests. One spec 001 test changed with FR-003: `DeserializeOptionsTests.MakeReadOnly_AfterAFailedBuildAndAddingTheMissingConverter_ItShould_Lock` now adds the missing converter for `FlatRow`'s `Measure`, since a converter for the nested `Measurement` no longer helps)

**Checkpoint**: flat rows are enforced; nothing else has changed name.

---

## Phase 2: User Story 2 - Document names belong to the format (Priority: P1) (#127)

**Goal**: `[Row]` and `[Field]` leave the core for the XML package's `XmlElementName`;
`XmlAttributeValue` and `XmlTextValue` become `XmlAttributeName` and `XmlElementText`, with one
source per member; `Field` is removed; `FieldContext` becomes `ReadingInformation` (R35-R37, R43).

**Independent Test**: A document whose element names differ from the class and member names gives
the same rows with names declared through `XmlElementName`; the core has no `RowAttribute`,
`FieldAttribute` or `Field` (quickstart 6-9).

### Test shells for User Story 2 ⚠️

- [X] T017 [P] [US2] Move existing XML fixtures to the new attributes, in `Forestry.Deserialize.Xml/test/Fixtures/`:
  - `[Row("…")]` → `[XmlElementName("…")]` in `LogWithFlag.cs`, `LogWithMarkedConstructor.cs`, `RecordedLog.cs`, `TreeLog.cs`, `MeasurementByAttribute.cs`, `PriceWithDuplicateName.cs`, `PriceWithTextOption.cs`, `PriceWithTwoTextValues.cs`
  - `[XmlAttributeValue("…")]` → `[XmlAttributeName("…")]` and `[XmlTextValue]` → `[XmlElementText]` in `Price.cs`, `MeasurementByAttribute.cs`, `PriceWithDuplicateName.cs`, `PriceWithTextOption.cs`, `PriceWithTwoTextValues.cs`, `Reference.cs`
  - rename `LogWithRenamedField.cs` → `LogWithRenamedMember.cs` and `LogWithDuplicateField.cs` → `LogWithDuplicateMember.cs` (classes too), with `[Field("…")]` → `[XmlElementName("…")]` (R42)
- [X] T018 [US2] Update tests using the moved fixtures and names, in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs` (renamed fixtures; T017/T018 comments say `XmlElementName`; the two tests named after "Field" and "Media" renamed `Deserialize_ForARenamedMember_ItShould_SetItFromTheElementNameOnly` and `Deserialize_ForARenamedRow_ItShould_ReadTheElementNameOnly`) and `Forestry.Deserialize.Xml/test/DeserializationContextTests.US3.cs` (`TextValue<PriceWithTextOption>` → `ElementText<PriceWithTextOption>`; the summary's list names `XmlAttributeName`, `XmlElementText` and `XmlElementName`)
- [X] T019 [US2] Test shell, US2-4 / FR-007 (quickstart 9): the existing duplicate-name test for `LogWithDuplicateMember` also asserts the message contains `XmlElementName`, in `WalkingSkeletonTests.US1.cs`
- [X] T020 [P] [US2] Fixtures for exclusive sources in `Forestry.Deserialize.Xml/test/Fixtures/`, all three in one file, `PricesWithTwoSources.cs`: `PriceWithElementAndText.cs` (one member with `XmlElementName` and `XmlElementText`), `PriceWithAttributeAndElement.cs` (one member with `XmlAttributeName` and `XmlElementName`), `PriceWithAttributeAndTextOption.cs` (one member with `XmlAttributeName`, set as element text by `ElementText<T>` in the test)
- [X] T021 [US2] Test shells, US2-2b / FR-006a (quickstart 8): each T020 fixture fails when the options lock, the message naming the member and both sources (for the option case, `XmlAttributeName` and `ElementText`), in `Forestry.Deserialize.Xml/test/DeserializationContextTests.US3.cs`
- [X] T022 [US2] Test shell, R37 "the name is required": `new XmlElementNameAttribute("")` and `new XmlElementNameAttribute(null!)` throw an `ArgumentException` (`ArgumentNullException` for null), in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`
- [X] T023 [P] [US2] Test shell, US2-6 / FR-009: `new ReadingInformation(2, "Log"u8, "/Root/Log"u8)` from `Forestry.Deserialize` reports `Depth` 2, and `Name.ToUtf8String()` and `Path.ToUtf8String()` (from `Utf8Extensions`) give `"Log"` and `"/Root/Log"`, in a new `Forestry.Deserialize/test/ReadingInformationTests.cs`
- [X] T024 [US2] **Test review gate**: the developer reviews T017-T023 on the board before T025 (C4) (developer, 2026-11-19: passed with no changes)

### Implementation for User Story 2

- [X] T025 [US2] Remove `Field` (R35): delete `Forestry.Deserialize/src/Rows/Field.cs`, and the `Field` property and its construction in `PropertyDefinition.cs` and `PropertyDefinition.OfType.cs`
- [X] T026 [US2] `ReadingInformation` (R36): `git mv` `Forestry.Deserialize/src/Reading/FieldContext.cs` → `Forestry.Deserialize/src/ReadingInformation.cs` and `Reading/FieldContextExtensions.cs` → `src/Utf8Extensions.cs`; rename the types, set namespace `Forestry.Deserialize`, keep `Depth`, `Name`, `Path` and `ToUtf8String`; rename parameters of this type from `context` to `information` in `Forestry.Deserialize/src/Reading/RowBuilder.cs` (both `Set` overloads and `SetMember`), `PropertyDefinition.cs` and `PropertyDefinition.OfType.cs` (`ReadAndSet`), `Forestry.Deserialize.Xml/src/XmlAssociation.cs` (`Set`) and `Forestry.Deserialize.Xml/src/XmlRowReading.cs`; fix `using` lines
- [X] T027 [US2] XML attributes in a new `Forestry.Deserialize.Xml/src/Attributes/` folder, namespace `Forestry.Deserialize.Xml` (R37, R43):
  - new `XmlElementNameAttribute(string name)`: `AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field`, `AllowMultiple = false`, `Inherited = false`; `ArgumentException.ThrowIfNullOrEmpty(name)`; doc comments for its meaning on a row type (the element its occurrences have) and on a member (the child element its value comes from)
  - `git mv` `src/XmlAttributeValueAttribute.cs` → `src/Attributes/XmlAttributeNameAttribute.cs`, class renamed, behavior unchanged
  - `git mv` `src/XmlTextValueAttribute.cs` → `src/Attributes/XmlElementTextAttribute.cs`, class renamed; its doc says "the text of the row's own element, never a child's"
- [X] T028 [P] [US2] Converters folder (R43): `git mv` `Forestry.Deserialize.Xml/src/XmlBooleanConverter.cs` → `src/Converters/XmlBooleanConverter.cs`, namespace `Forestry.Deserialize.Xml.Converters`; add the `using` in `DeserializeXmlOptions.cs`
- [X] T029 [US2] XML association and options (R37):
  - `Forestry.Deserialize.Xml/src/XmlAssociation.cs` reads `XmlElementNameAttribute` where it reads `RowAttribute` (row names) and `FieldAttribute` (member names) today; defaults unchanged ("without an attribute, a row type is the element named like its class, and a member is the child element named like the member")
  - a member with two or more of `XmlElementName`, `XmlAttributeName`, `XmlElementText`, or `ElementText<TRow>` with `XmlElementName` or `XmlAttributeName`, fails when the options lock, naming the member and the sources
  - `DeserializeXmlOptions.TextValue<TRow>(memberName)` → `ElementText<TRow>(memberName)` in `Forestry.Deserialize.Xml/src/DeserializeXmlOptions.cs`
  - messages in `Forestry.Deserialize.Xml/src/Formatting.Resource.cs` and `Throwing.cs` name the new attributes (e.g. "give one a different [XmlElementName] name"); add the exclusive-source message
- [X] T030 [US2] Remove the core attributes (FR-005): delete `Forestry.Deserialize/src/Attributes/RowAttribute.cs` and `FieldAttribute.cs`; remove `<see cref>`s to them (e.g. `XmlAssociation.cs` remarks)
- [X] T031 [US2] Forestry.StanForD (R40): `[XmlAttributeValue("…")]` → `[XmlAttributeName("…")]` and `[XmlTextValue]` → `[XmlElementText]` in `../stanford/Forestry.StanForD/src/LogMeasurements.cs` and `../stanford/Forestry.StanForD/src/StemMetricParts.cs` (12 places); nothing else changes
- [X] T032 [US2] Run `dotnet test` on the three test projects: T017-T023 pass, every earlier test green (StanForD's unchanged); then the performance gate on both deserialization test projects (depends on T025-T031) (2026-11-19: passed on net10.0: core 57, XML 188, StanForD 46 + 1 skipped (real documents folder not set), 0 build warnings; performance gate: XML 4 of 4. Also renamed beyond the task text: the parameters of `MediaRowEnumerator`'s and `MediaRowAsyncEnumerator`'s protected `Set` overloads; the XML options' internal `TextValues` and its messages and throw helpers (`ElementTexts`, `MoreThanOneElementText`, `ElementTextTypeNotAdded`, `ElementTextMemberNotFound`); new `MemberWithMoreThanOneSource`)

**Checkpoint**: document names live in the XML package; `Field` and `FieldContext` are gone.

---

## Phase 3: User Story 3 - One vocabulary: format and flatten (Priority: P2) (#128)

**Goal**: `Media*` becomes `Format*`, `Row` moves to the top of `src/`, and source documentation,
messages and the docs say "format" and "flatten" (R38, R39, R41).

**Independent Test**: The SC-003 search finds no "media" or "shred" outside the excluded history,
and every existing test passes (quickstart 10, 11, search check).

### Test shells for User Story 3 ⚠️

- [X] T033 [P] [US3] Test shell, SC-002 (quickstart 10): reflect over `typeof(Row).Assembly`'s exported types and their public and protected members; assert no type named `Field`, `RowAttribute` or `FieldAttribute`, and no type or member name containing `Media`, in a new `Forestry.Deserialize/test/PublicSurfaceTests.cs` (2026-11-19: fails today only for `MediaConverters`, `MediaRowEnumerator` and `MediaRowAsyncEnumerator`; the US2 removals already pass)
- [X] T034 [US3] **Test review gate**: the developer reviews T033 on the board before T035 (C4) (developer, 2026-11-19: passed with no changes)

### Implementation for User Story 3

- [X] T035 [US3] Enumerators (R38): `git mv` `Forestry.Deserialize/src/Reading/MediaRowEnumerator.cs` → `Reading/FormatRowEnumerator.cs` and `MediaRowAsyncEnumerator.cs` → `FormatRowAsyncEnumerator.cs` (namespace `Forestry.Deserialize.Reading` unchanged); update their uses in `Forestry.Deserialize/src/DeserializeOptions.cs` (`CreateRowEnumerator`, `CreateRowAsyncEnumerator`), the core's other callers, `Forestry.Deserialize.Xml/src/XmlRowEnumerator.cs`, `XmlRowAsyncEnumerator.cs`, `DeserializeXmlOptions.cs` and `Forestry.Deserialize/test/Fixtures/TestingDeserializeOptions.cs`
- [X] T036 [US3] Converters and region (R38): `MediaConverters` → `FormatConverters` in `Forestry.Deserialize/src/DeserializeOptions.cs` and `DeserializeOptions.Caching.cs` (`ResolveConverter`), and in `Forestry.Deserialize.Xml/src/DeserializeXmlOptions.cs` with `s_mediaConverters` → `s_formatConverters`; `#region Media` → `#region Format`
- [X] T037 [US3] Move `Row` (R39): `git mv` `Forestry.Deserialize/src/Rows/Row.cs` → `src/Row.cs`; delete the then empty `src/Rows/`
- [X] T038 [US3] Source wording (FR-011, R41): "media" → "format" and "shred"/"shredding" → "flatten"/"flattening" in XML doc comments and resource messages of `Forestry.Deserialize/src/` (`DeserializeOptions.cs`, `DeserializeOptions.Caching.cs`, `Deserialization.cs`, `Deserialization.Pipe.cs`, `Deserialization.Memory.cs`, `Deserialization.String.cs`, `Reading/RowBuilder.cs`, `Reading/ReadStatus.cs`, `Policies/DefinitionPolicy.cs`, `Formatting.Resource.cs`, `DeserializerKind.cs`, `Converters/Converter.cs`, `Converters/Primitive/BooleanConverter.cs`, `Attributes/RowConstructorAttribute.cs`, `Row.cs`) and `Forestry.Deserialize.Xml/src/` (`DeserializeXmlOptions.cs`, `XmlRowEnumerator.cs`, `XmlRowAsyncEnumerator.cs`, `XmlRowReading.cs`, the attributes), and in test comments (`Forestry.Deserialize/test/ConverterResolutionTests.cs`, `Fixtures/TestingDeserializeOptions.cs`, `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US1.cs`, `WalkingSkeletonTests.US3.cs`, `DeserializationContextTests.US3.cs`)
- [X] T039 [P] [US3] Package docs (FR-004, FR-011, R41): `Forestry.Deserialize/README.md` (words, and a row defined as the unit enumerated with single-value members, unrelated to row-based vs columnar formats) and `Forestry.Deserialize/ARCHITECTURE.md`; `Forestry.Deserialize.Xml/README.md` and `ARCHITECTURE.md` (words, and the attribute family from R37) (2026-11-19: both `ARCHITECTURE.md` are superseded design notes, so their words stay as history; each banner now says so, and R41 and the search exclude them)
- [X] T040 [P] [US3] SDK README (R41): "through a media" in Big data clients and "XML media" in the Packages table become "format" in `../../README.md`
- [X] T041 [US3] Run the SC-003 search from quickstart.md ("Search check"): no output apart from this spec's own documents; fix what it finds (depends on T038-T040) (2026-11-19: only `test/PublicSurfaceTests.cs`, which names "Media" on purpose; the search now matches from a word's start, so "immediately" is not a hit)
- [X] T042 [US3] Run `dotnet test` on the three test projects: T033 passes, every earlier test green; then the performance gate on both deserialization test projects (depends on T035-T041) (2026-11-19: passed on net10.0: core 58, XML 188, StanForD 46 + 1 skipped (real documents folder not set), 0 build warnings; performance gate: XML 4 of 4. `DefinitionPolicy`'s remark that names would "move into the core" was rewritten: names belong to the format package)

**Checkpoint**: one vocabulary across code, docs and constitution.

---

## Phase 4: Polish & Cross-Cutting Concerns

- [X] T043 [P] Changelogs (FR-014): list every breaking change from `contracts/public-api.md` under Breaking Changes in the Unreleased `1.0.0-beta.1` section of `Forestry.Deserialize/CHANGELOG.md` and `Forestry.Deserialize.Xml/CHANGELOG.md`, and add the flat check under Features Added in `Forestry.Deserialize/CHANGELOG.md`; reword "Shred" in its existing entry to "Flatten" (2026-11-19: also an Other Changes entry in Forestry.StanForD's changelog for its attribute renames)
- [X] T044 Run every quickstart.md scenario (1-11) and the search check; record the result here (2026-11-19: all pass: core 58, XML 188, StanForD 46 + 1 skipped; the scenario tests alone: core 24, XML 32; the search finds only `PublicSurfaceTests.cs`. Scenario 1 corrected to kinds with converters, and 1a added for the enum, `DateOnly` and `TimeSpan`)
- [X] T045 Mark `spec.md` **Status** Implemented with the date and story issues, and bring the board in sync: story issues' checklists and Architecture match this file, #125 notes the implementation, and #118 is reminded of StanForD's wording follow-on (2026-11-19: spec Implemented; #126-#128 match this file; implementation note on #125 and reminder on #118)

---

## Dependencies & Execution Order

### Phase Dependencies

- **US1 (Phase 1)**: shells, review gate, then implementation; no dependency
- **US2 (Phase 2)**: after US1 (both reshape `PropertyDefinition*.cs`); its shells can be written
  while US1 is implemented. T031 (StanForD) lands with T027 so StanForD keeps building
- **US3 (Phase 3)**: after US2 (both touch `DeserializeOptions*.cs`, the XML options and the
  enumerators)
- **Polish (Phase 4)**: after the stories it records

### Within Each Story

- Test shells, then the **test review gate**, then implementation (C4)
- Messages and checks before the type definition (US1); core removals and renames before the XML
  package, the XML package before StanForD (US2); renames before wording, wording before the
  search check (US3); then the test run and performance gate

### Parallel Opportunities

- US1: T001 and T009; after the gate T011 alongside T012
- US2: T017, T020 and T023 (different files); after the gate T028 alongside T025-T026
- US3: T033; after the gate T039 and T040 alongside T035-T038
- Polish: T043

## Parallel Example: User Story 2

```text
# Shells, in parallel:
T017 fixture moves (Forestry.Deserialize.Xml/test/Fixtures/)
T020 exclusive-source fixtures (Forestry.Deserialize.Xml/test/Fixtures/)
T023 ReadingInformationTests (Forestry.Deserialize/test/)

# After the review gate (T024), in parallel:
T025 remove Field      T028 Converters folder
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. US1 shells, review gate, implementation (T001-T016)
2. **Stop and validate**: quickstart 1-5; every earlier test green
3. Gives spec 004 (writing rows) its guarantee: every member formats to UTF-8 as one column

### Incremental Delivery

1. US1 → flat rows enforced, `ResolvedConverter`
2. US2 → XML attribute family, `ReadingInformation`, `Field` gone, StanForD renamed
3. US3 → `Format*`, `Row` at the top, docs in one vocabulary (unblocks #116)

## Notes

- [P] tasks touch different files and depend on no incomplete task
- Each review gate is a board task the developer closes (C4, C5); story issues on the board carry
  scope and design decisions in their Architecture section and are kept in sync with this file
- Commit after each task or logical group; a story's shells reach main only with its
  implementation (R40)
