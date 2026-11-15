---

description: "Task list for Harvesting rows (spec 001)"
---

# Tasks: Harvesting rows

**Input**: Design documents from `specs/001-harvesting-rows/` (plan.md, spec.md, research.md,
data-model.md, contracts/public-api.md, quickstart.md) and `.specify/memory/constitution.md` 1.0.0.

**Prerequisites**: research decisions marked Proposed are confirmed by the developer before the
story that uses them starts (C7).

**Tests**: Required (constitution C6). In every code story phase: test shells first, one per
acceptance scenario or quickstart row; then a **test review gate** (the developer reviews the shells
on the board before any code); then implementation. No code is written before a story's shells, not
even declarations, so a story's shells may not compile until its implementation.

- **Tests follow A4**: steps labelled `// Arrange`, `// Act`, `// Assert`; each test reads its own
  short Harvesting fragment, one element per line, in `test/Fragments/` or inline. Real documents
  are read only by end-to-end tests, from the folder named by `STANFORD_HPR_FOLDER`, skipped when it
  is unset, never committed (they hold personal data).
- **Tests run through `Forestry.Deserialize.Xml`** with options built by `AddHarvesting()`
  (contracts/public-api.md).

**Organization**: Phases by user story in dependency order: US1 (done), US5 and US6 (documents,
next), then Setup right before the first code story, then US2, US4, US3, US7. The cross-silo
prerequisites D1-D2 are met (Forestry.Deserialize spec 002, #104, 2026-11-13).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: The user story the task belongs to (US1-US7)
- Paths are relative to `sdk/stanford/Forestry.StanForD/` unless they start with `sdk/`

## External prerequisites

- **Forestry.Deserialize D1 and D2** (research R1): **met** by Forestry.Deserialize spec 002, the
  deserialization context (#99, merged via #104 on 2026-11-13). What the code stories should know:
  - policies get a `DeserializationContext`; `Complete` (not `Enumerate`) decides whether a row comes
    out and how long it stays in the context (`Enumerate(false)`, `Keep(Lifetime)`); context rows
    such as `Machine` or the definitions are rows kept and not enumerated;
  - members are direct children of their row's element; a value inside a wrapper (e.g.
    `LogMeasurement > LogDiameter`) is reached by adding the wrapper as a row type that does not come
    out, whose policy sets the parent;
  - attributes and element text are declared with `Forestry.Deserialize.Xml`'s
    `[XmlAttributeValue("name")]` and `[XmlTextValue]`, not by name matching;
  - at most `MaxContextRows` (default 100) rows are kept.

  As first specified here, under Forestry.Deserialize's own constitution and spec:
  - **D1**: rows of several types open at the same time, nested (`Machine` > `Stem` > `Log` >
    `LogVolume`), each completed at its own end tag, in one deserialization. Blocks US2, US3, US4,
    US7.
  - **D2**: a row's XML attributes and its own element content as members (e.g. `LogVolume` with
    `LogVolumeCategory`, `LogMeasurementCategory` and `Value`). Blocks US4 and the schema version
    policy.
  Shells for US2-US4 follow the shipped API above.

## Not in this spec

- Performance gate (a later spec); extension elements (FR-020, a Forestry.Biometria add-on);
  writing rows to storage; other StanForD document families.

---

## Phase 1: User Story 1 - C# engineering (Priority: P1) ✅ Done

**Goal**: The package builds, tests and ships like the SDK's other packages (#91).

- [x] T001 [US1] Add the solution, source and test projects with the build hook to `eng/` in `Forestry.StanForD.slnx`, `src/Forestry.StanForD.csproj`, `test/Forestry.StanForD.Tests.csproj` (#91, merged via #92)
- [x] T002 [US1] Leave performance tests out of the default run in `test/Forestry.StanForD.Tests.runsettings` and point `ci.yml` at the solution (#91)
- [x] T003 [US1] Add the change log in `CHANGELOG.md` (#91)
- [x] T004 [US1] Mark the package as a shipping library (`IsShippingLibrary`) in `Directory.Build.props` (committed f1e34a8, 2026-11-11)

---

## Phase 2: Setup (Shared Infrastructure)

**Purpose**: Wiring every code story needs; adds no behavior a test could assert. Done right before
the first code story (US2), not before the document stories (developer, 2026-11-13): T005 for any
code, T006 for the first end-to-end test. The fragment (T007) moved to the start of US2 (2026-11-13).

- [ ] T005 Add project references to `sdk/deserialize/Forestry.Deserialize/src/Forestry.Deserialize.csproj` and `sdk/deserialize/Forestry.Deserialize.Xml/src/Forestry.Deserialize.Xml.csproj` in `src/Forestry.StanForD.csproj` (A1, R9)
- [X] T006 [P] Add the test helper `RealDocuments` that yields files from `STANFORD_HPR_FOLDER` and skips when unset in `test/RealDocuments.cs` (R8) (2026-11-14: `test/RealDocuments.cs`, reading `*.hpr`, `*.hpr.xml` and `*.hpr.gz`; `[RealDocumentFact]` skips a test when the folder is not set, since xUnit 2 has no runtime skip)

**Checkpoint**: the package references the deserialization packages and tests can read real documents.

---

## Phase 3: User Story 5 - When Harvesting values are updated (Priority: P2) 📄 Document ✅ Done

**Goal**: Know which Harvesting values change while the machine works, starting with the contract
start date (#96). **Independent Test**: findings written with sources and reviewed by the developer.

- [X] T008 [P] [US5] Add the Harvesting schemas from Skogforsk to `schemas/` and list which versions exist in `schemas/README.md` (FR-019, R6) (2026-11-13: the developer added the StanForD 2010 schemas at the latest version, V4p1, `HarvestedProduction` included, and removed the older common definitions; "latest only" applies to the schema files, not the documents read; listed in `schemas/README.md`)
- [X] T009 [US5] Compare `ObjectDefinition/StartDate` and `ObjectModificationDate` across the real documents of one contract over several days, and record whether the start date changes when cutting restarts, with sources, in `specs/001-harvesting-rows/findings-us5.md` (2026-11-13: done in `findings-us5.md` §1-2, 12 documents over four days: the start date is set once per object; documents are incremental and repeat the previous last stem)
- [X] T010 [US5] Fill the timestamps-per-definition table (created once or modifiable, element names, seen in real documents) from the schemas and documents in `specs/001-harvesting-rows/findings-us5.md` and update research.md R4 (2026-11-13: done in `findings-us5.md` §3; the developer chose `HarvestingCreationTime`, the header `CreationDate` in UTC, then `ContractNumber` as the partition values; spec FR-007 to FR-009 and research R4 updated)
- [X] T011 [US5] Confirm which two Harvesting schema versions are the latest and whether the real documents (3.5) fall inside them; update spec.md Assumptions and research.md R6 (2026-11-13: the developer chose the latest schema version only, V4p1, with no version check for the POC; spec FR-019, research R6, `findings-us5.md` §4)
- [X] T012 [US5] **Review gate**: the developer reviews `findings-us5.md` on the board (#96) (2026-11-13: passed by the developer on the documentation alone; the fragment moved to US2, so no test review in US5)

---

## Phase 4: User Story 6 - Collect and group queries (Priority: P2, ongoing) 📄 Document ✅ First grouping done

**Goal**: A first grouping of likely queries on log and stem data (#97). **Independent Test**: every
group names its actor and lists the filters, groupings and summaries it needs.

- [X] T013 [P] [US6] Create the extendable query collection (query in plain language, who asks it, group, members it filters/groups/summarizes by) in `specs/001-harvesting-rows/queries.md` (2026-11-14: `queries.md`, groups with query, who asks, filters, groupings, summaries and source)
- [X] T014 [US6] Collect and group queries covering at least product value, contract progress over time, seasonal measurement quality, operator comparisons and machine comparisons (SC-005) in `specs/001-harvesting-rows/queries.md` (2026-11-14: seven groups from what the industry is offered today (#97); each marked hpr, Biometria or MOM, only hpr in the POC; seasonal quality in G6, operator and machine comparisons in G3)
- [X] T015 [US6] For each group, list the Stem and Log members it needs and mark which are already in `data-model.md` in `specs/001-harvesting-rows/queries.md` (2026-11-14: `queries.md`, "Members the hpr groups need": on the rows today, missing for US7, and the User Ids for joining Biometria later)
- [X] T016 [US6] **Review gate**: the developer reviews `queries.md` on the board (#97); collecting continues after the story closes (2026-11-14: passed, "Looks good"; collecting continues in `queries.md`)

---

## Phase 5: User Story 2 - Identification (Priority: P1) 🎯 MVP ✅ Done

**Goal**: Every Stem and Log row is globally identified (#93). **Independent Test**: two fragments
from two machines with equal stem and log keys give rows whose identifications never repeat.
**Needs**: D1 (met, #104); its first task writes the fragment (T007).

**Design (2026-11-13, developer)**: only Stem and Log rows come out; whatever is standard in the
common definitions becomes Stem and Log dimensions. Identification is set when a row completes
(spec 002's `Complete`): the `Machine` is found as an ancestor (it stays open around every stem),
a log's stem as an ancestor, and the common definitions are kept in the deserialization context
until the document ends (`Keep(Lifetime.UntilEnd)`). This replaces the earlier design of `Set`
policies on key members and a policy holding its own memory.

### Test shells for User Story 2 ⚠️

- [X] T007 [US2] Write the minimal Harvesting fragments from `findings-us5.md`, one element per line (A4): `test/Fragments/MinimalHarvesting.xml` (header, machine, operator, species group, product and object definitions, stem 619254 with logs 1-2, stem 619255 with log 1) and `test/Fragments/MinimalHarvestingNext.xml` (the same machine's next document: the definitions again, stem 619255 repeated, stem 619256); made-up values, root `version="3.5"`; read through `test/Fragments/Fragments.cs`, copied to the output by the test project (2026-11-13)
- [X] T017 [P] [US2] Test shell, quickstart 1 / US2-1 / FR-001: a Stem row carries `MachineKey`, `StemKey` and `Id` = `{machine key "N"}-{stem key}` in `test/IdentificationTests.cs` (2026-11-13)
- [X] T018 [P] [US2] Test shell, quickstart 1 / US2-2 / FR-002: a Log row carries `MachineKey`, `StemKey`, `LogKey` and `Id` = `{machine key "N"}-{stem key}-{log key}` in `test/IdentificationTests.cs` (2026-11-13)
- [X] T019 [P] [US2] Test shell, quickstart 2 / US2-4 / SC-001: the fragment and a copy with another machine key, equal stem and log keys: no `Id` occurs twice in `test/IdentificationTests.cs` (2026-11-13)
- [X] T020 [P] [US2] Test shell, edge case "log keys restart per stem": log key 1 under two stems gives two different log `Id`s in `test/IdentificationTests.cs` (2026-11-13)
- [X] T021 [P] [US2] Test shell, quickstart 3 / FR-005: a `Stem` in a `Machine` with no `MachineKey` throws `InvalidDataException` naming the stem key and `MachineKey` in `test/IdentificationTests.cs` (2026-11-13)
- [X] T022 [P] [US2] Test shell, quickstart 4 / US2-3 / FR-003: a theory over `ObjectKey`, `SpeciesGroupKey`, `OperatorKey` (stem) and `ProductKey` (log) referring to a definition not in the document throws `InvalidDataException` naming the stem, the key and its value; defined references pass in T017-T018 in `test/IdentificationTests.cs` (2026-11-13)
- [X] T023 [P] [US2] Test shell, FR-004: the same fragment deserialized twice gives the same `Id`s in `test/IdentificationTests.cs` (2026-11-13)
- [X] T069 [P] [US2] Test shell, FR-004 / findings §2: the stem repeated in the next document (619255 and its log) has the same `Id`s in both documents, so duplicates can be removed downstream, in `test/IdentificationTests.cs` (2026-11-13)
- [X] T024 [P] [US2] Test shell, R2: only Stem and Log rows come out (2 and 3); `Machine` and the definitions never do, in `test/IdentificationTests.cs` (2026-11-13)

The shells assume `AddHarvesting()` (contracts/public-api.md); the test project does not compile
until T030 declares it. That is the only build error (2026-11-13).

### Review gate

- [X] T025 [US2] **Test review gate**: the developer reviews T007, T017-T024 and T069 on the board before any code (C6) (2026-11-13: passed, "Shells look good")

### Implementation for User Story 2 (after the gate)

- [X] T026 [P] [US2] Add the shared identification format, `{machine key "N"}-{key}[-{key}]`, in `src/Identification.cs` (2026-11-13: `Identification.Format` in `src/Identification.cs`)
- [X] T027 [P] [US2] Implement the common-definition policy for `Machine` (never comes out; found as an ancestor) and the four definitions (never come out, `Keep(Lifetime.UntilEnd)`, `MachineKey` and `Id` set from the `Machine` ancestor at `Complete`) in `src/Policies/` (2026-11-13: `MachinePolicy` in `src/Policies/IdentificationPolicies.cs`; `CommonDefinitionPolicy<TDefinition>` in `src/Policies/CommonDefinitionPolicy.cs`, failing when a definition has no key)
- [X] T028 [US2] Implement the Stem and Log identification policies at `Complete`: `MachineKey` from the `Machine` ancestor, a log's `StemKey` from its `Stem` ancestor, `Id` via T026; no machine key throws `InvalidDataException` (FR-005); a reference key with no kept definition of that key throws `InvalidDataException` naming the stem, key and value (FR-003), in `src/Policies/` (2026-11-13: `StemIdentificationPolicy` and `LogIdentificationPolicy` in `src/Policies/IdentificationPolicies.cs`, lookups in `src/Policies/Harvesting.cs`; a reference key the document does not set is not checked)
- [X] T029 [US2] Add converters for the StanForD enums the rows use (`ProcessingCategory`, `CoordinateReferenceSystem`, `CuttingReason`, StanForD texts with spaces), since Forestry.Deserialize has none for enums and the options would fail when they lock, in `src/Converters/` (2026-11-13: `StanForDEnumConverter<TEnum>` in `src/Converters/`, matching the text without spaces, ignoring case; unknown text throws `FormatException`)
- [X] T030 [US2] Implement `AddHarvesting()` adding `Stem` and `Log`, `Machine` and the definitions, the converters and the policies, per contracts/public-api.md, in `src/HarvestingOptions.cs` (2026-11-13: `HarvestingOptionsExtensions.AddHarvesting()` in `src/HarvestingOptions.cs`; no `configure` argument until US4 needs `HarvestingOptions`)
- [X] T031 [US2] Run `dotnet test`: T017-T024 and T069 pass (2026-11-13: 12 passed on net10.0, the four T022 cases included)

**Checkpoint**: Stem and Log rows are identified globally (MVP).

---

## Phase 6: User Story 4 - Conditional mapping (Priority: P1) ✅ Done

**Goal**: Repeated elements land in the right Log member by their categories (#95). **Independent
Test**: a log with three volumes and eight diameters fills eleven members; unmatched fails by
default. **Needs**: D1, D2 (met, #104).

**Design (2026-11-13)**: `LogVolume`, `LogMeasurement` and `LogDiameter` are row types that never
come out, read with spec 002's XML attribute and text members. When one completes, its policy
matches its categories (a diameter's measurement category is its enclosing `LogMeasurement`'s) and
sets the matching member of the open `Log`; `LogMeasurement` sets `MachineLength`. All conditions
need measurement category "Machine". A member no occurrence sets stays 0, "not measured"
(developer, 2026-11-13). `AddHarvesting` takes `Action<HarvestingOptions>`, with `Unmatched`
`Fail` (default) or `Ignore` (FR-012).

### Test shells for User Story 4 ⚠️

- [X] T073 [US4] Extend `test/Fragments/MinimalHarvesting.xml`: stem 619254's first log carries three volumes, a cutting category, top sawing, find-butt-end, eight diameters and the length, as a real log (2026-11-13; the 18 earlier tests still pass)
- [X] T032 [P] [US4] Test shell, quickstart 6 / US4-1: three `LogVolume`s ("m3sob", "m3sub", "m3 (price)", measurement "Machine") fill `MachineVolumeOverBark`, `MachineVolumeUnderBark`, `MachineVolumePrice` in `test/ConditionalMappingTests.cs` (2026-11-13)
- [X] T034 [P] [US4] Test shell, quickstart 7 / FR-010, FR-011: eight `LogDiameter`s under a "Machine" `LogMeasurement` fill the eight `MachineDiameter…` members, and `MachineLength` is set, in `test/ConditionalMappingTests.cs` (2026-11-13)
- [X] T074 [P] [US4] Test shell: a log measuring only some categories keeps 0 for the others in `test/ConditionalMappingTests.cs` (2026-11-13)
- [X] T033 [P] [US4] Test shell, US4-2 / FR-010: a volume matching the volume category but not the measurement category sets nothing (with `Unmatched = Ignore`) in `test/ConditionalMappingTests.cs` (2026-11-13)
- [X] T035 [P] [US4] Test shell, quickstart 8 / US4-4 / FR-012: an unknown volume category fails by default, naming `LogVolume`, the category and the stem in `test/ConditionalMappingTests.cs` (2026-11-13)
- [X] T075 [P] [US4] Test shell, FR-012: an unknown diameter category fails by default, naming `LogDiameter` and the category in `test/ConditionalMappingTests.cs` (2026-11-13)
- [X] T076 [P] [US4] Test shell, FR-010, FR-012: diameters under a measurement that is not "Machine" fail by default, naming the measurement category in `test/ConditionalMappingTests.cs` (2026-11-13)
- [X] T036 [P] [US4] Test shell, quickstart 9 / US4-3: with `Unmatched = Ignore`, an unknown category is ignored and the other members are set in `test/ConditionalMappingTests.cs` (2026-11-13)
- [X] T037 [P] [US4] Test shell, quickstart 10 / FR-013: two volumes with the same categories in one log fail, naming `LogVolume`, the category and the stem in `test/ConditionalMappingTests.cs` (2026-11-13)
- [X] T038 [P] [US4] Test shell, R2: volumes, measurements and diameters never come out; only the 5 stems and logs do, in `test/ConditionalMappingTests.cs` (2026-11-13)

The shells assume `HarvestingOptions` and `UnmatchedOccurrence` (contracts/public-api.md); the test
project does not compile until T043 declares them (2026-11-13).

### Review gate

- [X] T039 [US4] **Test review gate**: the developer reviews T073, T032-T038 and T074-T076 on the board before any code (C6) (2026-11-13: passed, "Shells look good")

### Implementation for User Story 4 (after the gate)

- [X] T040 [P] [US4] Declare the row types `LogVolume` (`[XmlAttributeValue]` `logVolumeCategory` and `logMeasurementCategory`, `[XmlTextValue]` `decimal` value), `LogMeasurement` (`logMeasurementCategory`, `LogLength`) and `LogDiameter` (`logDiameterCategory`, `int` value) in `src/` (2026-11-13: `src/LogMeasurements.cs`)
- [X] T042 [US4] Implement the conditional mapping policies at `Complete`: keep the row out of the enumeration, match its categories against the conditions, set the open `Log`'s member; a member already set fails (FR-013); no match fails or is ignored by `Unmatched` (FR-012), in `src/Policies/` (2026-11-13: `LogVolumePolicy`, `LogMeasurementPolicy`, `LogDiameterPolicy` and the shared `ConditionalMapping` in `src/Policies/ConditionalMappingPolicies.cs`)
- [X] T043 [US4] Add `HarvestingOptions` (`Unmatched`), `UnmatchedOccurrence` and `AddHarvesting(Action<HarvestingOptions>?)`, adding the three row types and their policies, in `src/HarvestingOptions.cs` (2026-11-13: `src/HarvestingOptions.cs`)
- [X] T044 [US4] Run `dotnet test`: T032-T038 and T074-T076 pass, US2 and US3 still green (2026-11-13: 28 passed on net10.0)

---

## Phase 7: User Story 3 - Partitioning (Priority: P2) ✅ Done

**Goal**: Every Stem and Log row carries its partition values, with named sources (#94):
`HarvestingCreationTime` (the header's `CreationDate`, in UTC), then `ContractNumber` (US5
decision, 2026-11-13). **Independent Test**: the fragment gives every row the creation time in UTC
and the contract number of its object. **Needs**: US2 (done), US5 (done).

**Design (2026-11-13)**: `HarvestedProductionHeader` is a row type that never comes out and is kept
until the document ends, like the common definitions. When a stem or log completes, a partition
policy, added after the identification policy, sets the creation time in UTC, and `ContractNumber`
and `ObjectName` from the `ObjectDefinition` the stem refers to (a log through its stem). A stem
with no `ObjectKey` fails (developer, 2026-11-13).

### Test shells for User Story 3 ⚠️

- [X] T045 [P] [US3] Test shell, US3-1 / FR-007: every stem and log carries `HarvestingCreationTime` = the header's creation date in UTC (offset zero) in `test/PartitioningTests.cs` (2026-11-13)
- [X] T046 [P] [US3] Test shell, US3-2 / FR-009: every stem and log carries `ContractNumber` and `ObjectName` from its stem's object in `test/PartitioningTests.cs` (2026-11-13)
- [X] T070 [P] [US3] Test shell, findings §2: the stem repeated in the next document keeps its `Id` and carries the later creation time in `test/PartitioningTests.cs` (2026-11-13)
- [X] T071 [P] [US3] Test shell: a creation date with seven fraction digits, as in the real documents, keeps them in `test/PartitioningTests.cs` (2026-11-13)
- [X] T047 [P] [US3] Test shell, FR-006: a header with no `CreationDate` throws `InvalidDataException` naming the stem and `CreationDate` in `test/PartitioningTests.cs` (2026-11-13)
- [X] T072 [P] [US3] Test shell, FR-009: a stem with no `ObjectKey` throws `InvalidDataException` naming the stem and `ObjectKey` in `test/PartitioningTests.cs` (2026-11-13)

The shells compile against the rows' existing members and fail until the implementation (6 failing,
the 12 US2 tests still passing, 2026-11-13). The fragment edit helpers moved to
`test/Fragments/Fragments.cs`, shared with the US2 tests.

### Review gate

- [X] T048 [US3] **Test review gate**: the developer reviews T045-T047 and T070-T072 on the board before any code (C6) (2026-11-13: passed, "Shells look good")

### Implementation for User Story 3 (after the gate)

- [X] T049 [P] [US3] Declare the row type `HarvestedProductionHeader` (`CreationDate`, `DateTimeOffset`) in `src/HarvestedProductionHeader.cs`, and a policy keeping it out of the enumeration and in the context until the end, in `src/Policies/` (2026-11-13: `src/HarvestedProductionHeader.cs`; `HeaderPolicy` in `src/Policies/PartitionPolicies.cs`)
- [X] T051 [US3] Implement the partition policy for Stem and Log at `Complete`: `HarvestingCreationTime` from the kept header, converted to UTC; `ContractNumber` and `ObjectName` from the kept `ObjectDefinition` with the stem's `ObjectKey`; no `CreationDate` or no `ObjectKey` throws `InvalidDataException`, in `src/Policies/` (2026-11-13: `StemPartitionPolicy` and `LogPartitionPolicy` in `src/Policies/PartitionPolicies.cs`, lookups in `src/Policies/Harvesting.cs`)
- [X] T050 [US3] Add the header row and the partition policy to `AddHarvesting()` in `src/HarvestingOptions.cs`, the partition policy after the identification policies (2026-11-13)
- [X] T052 [US3] Record the partition values' sources in `specs/001-harvesting-rows/data-model.md` (FR-006, FR-009) (2026-11-13: data-model.md, Partitioning sources)
- [X] T053 [US3] Run `dotnet test`: T045-T047 and T070-T072 pass, US2 still green (2026-11-13: 18 passed on net10.0, seven fraction digits included)

---

## Phase 8: Schema version constraint (FR-019) ⏸ After the POC

*Deferred 2026-11-13 (developer): the POC supports the latest schema version only, with no version check (spec FR-019).*

- [ ] T054 [P] [US7] Test shell, FR-019: a root `version` outside the two latest versions fails by default, is read with the option, and one inside passes, in `test/SchemaVersionTests.cs`
- [ ] T055 [US7] **Test review gate**: the developer reviews T054 on the board (C6)
- [ ] T056 [US7] Declare the root context row `HarvestedProduction` (`Version` attribute) and implement the schema version policy with a configurable number of versions in `src/Harvesting/Context/HarvestedProduction.cs` and `src/Harvesting/Policies/SchemaVersionPolicy.cs` (R6)

---

## Phase 9: User Story 7 - Log and Stem dimensions (Priority: P3) ✅ Done

**Goal**: Stem and Log rows carry every dimension the hpr query groups filter or group on, copied
onto each row (#98; spec FR-021 to FR-023). **Independent Test**: the fragment gives every stem and
log its object, sub-object, species group, operator, machine and (on logs) product and destination
dimensions; a real document gives one row per stem and per log, every member traced. **Needs**:
US2-US4 (done), US6 (first grouping done).

**Design (2026-11-14, developer)**: dimensions are duplicated onto every row instead of joined, a
stem's onto its logs too; products and species groups keep Name, Info, UserID and Version; members
are named after their StanForD element, prefixed by their owner where needed
(`LoggingContractorBusinessID`). The members are listed in data-model.md, "Dimensions (US7)". Stem
metrics are US8 (Phase 9b).

### Test shells for User Story 7 ⚠️

- [X] T077 [US7] Extend both fragments with the dimension elements, as in the real documents: `StemNumber` and `SubObjectKey` on each stem; `ObjectUserID` and a `SubObject` (`SubObjectUserID`, `SubObjectName`, `SubObjectKey`) in the object definition; `SpeciesGroupUserID`, `SpeciesGroupInfo` and `SpeciesGroupVersion`; `OperatorUserID`; `MachineUserID` and `LoggingContractor/BusinessID`; `ProductUserID` and a `ProductDestination` (`BusinessName`, `BusinessID`) in the classified product definition; made-up values, in `test/Fragments/` (2026-11-14: both fragments; every existing test still passes)
- [X] T078 [P] [US7] Test shell, US7-1 / FR-021, FR-022: every stem carries `StemNumber`, its object's key, name and user id, sub-object key, name and user id, species group key, name, info, user id and version, and operator key, business name and user id, in `test/DimensionTests.cs` (2026-11-14)
- [X] T079 [P] [US7] Test shell, US7-1 / FR-021: every log carries the same stem dimensions as its stem (no join needed), in `test/DimensionTests.cs` (2026-11-14)
- [X] T080 [P] [US7] Test shell, US7-2 / FR-022: every log carries its product's key, name, info, user id and version, and its destination's business name and id, in `test/DimensionTests.cs` (2026-11-14)
- [X] T081 [P] [US7] Test shell, US7-3 / FR-023: every stem and log carries `MachineUserID` and `LoggingContractorBusinessID`, in `test/DimensionTests.cs` (2026-11-14)
- [X] T057 [P] [US7] Test shell, quickstart 11 / US7-4 / FR-018: a real document (`STANFORD_HPR_FOLDER`, read through T006's helper) gives one Stem row per `Stem` and one Log row per `Log`, with no failure, in `test/EndToEndTests.cs` (2026-11-14: counts `<Stem>` and `<Log>` in each document against the rows, default options, so an unknown category fails here)
- [X] T058 [P] [US7] Test shell, quickstart 12 / US7-5 / C0 / FR-016: every public member of `Stem` and `Log` is a single value (primitive, single-value struct or `string`) in `test/RowShapeTests.cs` (2026-11-14: primitives, enums, `string`, `decimal`, `Guid`, `DateTime`, `DateTimeOffset`, or nullable ones)
- [X] T059 [P] [US7] Test shell, C3 / FR-017: every member of `Stem` and `Log` is named in `data-model.md` in `test/RowShapeTests.cs` (2026-11-14: each member name in backticks in `data-model.md`; fails today for members the data model names only as a group, e.g. the eight diameters)

### Review gate

The dimension shells do not compile until T062 declares the new members (only missing-member
errors, 2026-11-14), so the whole test project waits on T062.

- [X] T060 [US7] **Test review gate**: the developer reviews T077-T081 and T057-T059 on the board before any code (C6) (2026-11-14: passed, "Shells look good")

### Implementation for User Story 7 (after the gate)

- [X] T061 [US7] Choose the remaining Stem and Log members from `queries.md` and record them in `specs/001-harvesting-rows/data-model.md` (2026-11-14: data-model.md "Dimensions (US7)", queries.md "Missing, for US7"; `ProductNamespace` renamed `ProductInfo`)
- [X] T062 [US7] Add the dimension members to `Stem`, `Log` and the definitions; context rows for what sits below them (`SubObject`, `ContactInformation`, `ClassifiedProductDefinition`, `ProductDestination`, `LoggingContractor`); and policies copying the definitions' dimensions onto stems and logs, and a stem's onto its logs, in `src/` and `src/Policies/`; name every Stem and Log member in data-model.md (T059) (2026-11-14: members on `Stem`, `Log` and the definitions; rows `SubObject`, `ContactInformation`, `ClassifiedProductDefinition`, `ProductDestination`, `LoggingContractor` in `src/DefinitionParts.cs`; their policies in `src/Policies/DefinitionPartPolicies.cs`; `StemDimensionPolicy` and `LogDimensionPolicy` in `src/Policies/DimensionPolicies.cs`; every member named in data-model.md. A product with two destinations fails (S0). The test project copies data-model.md next to the tests, since the build output is outside the package folder)
- [X] T063 [US7] Decide with the developer whether `MachineType`, `DiameterUnitType` and `ValidationExtensions` stay; remove them if not, in `src/` (R9) (2026-11-13: removed with the row classes, commit 3400c45)
- [X] T086 [US7] Ignore measurements with measurement category "Operator" (M2) deliberately, in `src/Policies/ConditionalMappingPolicies.cs`, with a test that an operator's volume beside the machine's neither fails nor changes the machine's members in `test/ConditionalMappingTests.cs`; T033 and T076 now use "Other" for a measurement category that still counts as unmatched (2026-11-14, found by T057; the M2 investigation is #109)
- [X] T064 [US7] Run `dotnet test` with `STANFORD_HPR_FOLDER` set: T077-T081 and T057-T059 pass, all earlier stories green (2026-11-14: 36 passed with the fragments. With the 12 real documents, the end-to-end test fails on 10 logs carrying a fourth volume, `m3sob` measured by "Operator", which matches no Log member; with `Unmatched = Ignore` every document passes, one row per stem and log. Developer, 2026-11-14: operator (M2) measurements are ignored for now (T086); then all 38 tests pass with the 12 real documents)

---

## Phase 9b: User Story 8 - Stem metrics (Priority: P4) ✅ Done

**Goal**: The stem's values in wrapper elements: signed coordinates, altitude, coordinate reference
system, boom positions by category and DBH (spec FR-024). **Independent Test**: the fragment's stem
carries them. **Needs**: US7. Only the detail query (G4) uses them (developer, 2026-11-14).

- [X] T087 [US8] Extend both fragments with two `BoomPositioning`s per stem ("Felling", "Last log cut", made-up angles and extensions) and `SingleTreeProcessedStem/DBH`, in real-document order, in `test/Fragments/` (2026-11-14; the 37 earlier tests still pass)
- [X] T082 [P] [US8] Test shells: latitude and longitude signed by `latitudeCategory` / `longitudeCategory`; altitude and `coordinateReferenceSystem`; `BoomAngle` and `BoomExtension` per `boomPositioningCategory` ("Felling", "Last log cut"); `DBH`; in `test/StemMetricTests.cs` (2026-11-14: 8 tests; also an unknown boom positioning or latitude category failing by default, ignored with `Unmatched = Ignore`, and no metric rows coming out. Only build error: `Stem.DBH` does not exist yet)
- [X] T083 [US8] **Test review gate**: the developer reviews T082 on the board before any code (C6) (2026-11-14: passed, "Shells look good")
- [X] T084 [US8] Context rows `StemCoordinates`, `Latitude`, `Longitude`, `BoomPositioning` and `SingleTreeProcessedStem`, and policies setting the stem's metric members, in `src/` and `src/Policies/`; `DBH` added to `Stem`; a category matching no member fails or is ignored by `Unmatched` (FR-012) (2026-11-14: `src/StemMetricParts.cs` and `src/Policies/StemMetricPolicies.cs`; `Stem.DBH`)
- [X] T088 [US8] Ignore a `Latitude` or `Longitude` outside `StemCoordinates`: real documents carry a latitude in species groups' bark functions (`Skogforsk2004ScotsPine`), which name association matched as a row; test in `test/StemMetricTests.cs` (2026-11-14, found by T057)
- [X] T085 [US8] Run `dotnet test`: T082 passes, all earlier stories green (2026-11-14: 46 passed with the fragments, 47 with the 12 real documents)

---

## Phase 10: Polish & Cross-Cutting Concerns ✅ Done

- [X] T065 [P] XML doc comments on every public member (D0) (2026-11-15: met by the build, which warns on any public member without a doc comment and reports none; Harvesting-specific documentation moves to a future Forestry.StanForD.Harvesting package, developer, 2026-11-15)
- [X] T066 [P] Update the usage, rows and policies sections in `README.md` and `ARCHITECTURE.md` (D0) (2026-11-15: README shows `AddHarvesting()`, what a row gives, choices, failures, duplicates, scope and tests; ARCHITECTURE describes what was built: rows, policies, options, findings and POC debt #109, #110)
- [X] T067 [P] Add the release notes in `CHANGELOG.md` (2026-11-15: 1.0.0-beta.1, unreleased)
- [X] T068 Run every quickstart row and record the result in `specs/001-harvesting-rows/quickstart.md` (2026-11-15: 33 scenarios, each mapped to its test; 47 passed with the 12 real documents; scenarios brought up to date with what was built)

---

## Dependencies & Execution Order

### Phase Dependencies

- **US1 (Phase 1)**: done.
- **Setup (Phase 2)**: right before the first code story (US2).
- **US5 (Phase 3)**: done 2026-11-13 (`findings-us5.md`). **US6 (Phase 4)**: no dependencies; next.
- **US2 (Phase 5)**: Setup; D1 met; starts with the fragment (T007).
- **US4 (Phase 6)**: Setup; D1 and D2 met. Independent of US2 except the shared `Log` file.
- **US3 (Phase 7)**: US2 (reference keys) + US5 (T009-T010).
- **Schema version (Phase 8)**: after the POC (spec FR-019, 2026-11-13).
- **US7 (Phase 9)**: US2, US3, US4, US6 (all done); dimensions only.
- **US8 (Phase 9b)**: US7; the stem metrics in wrapper elements.
- **Polish**: last.

### Within Each Story

Shells → review gate → declarations → policies → options → `dotnet test`.

### Parallel Opportunities

- US5 and US6 together.
- All shells marked [P] within a story (same file, written together, reviewed together).
- US2 and US4 shells can be written in parallel; their implementations touch `Log.cs` both, so
  they run one after the other.

## Parallel Example: User Story 2

```text
Task: "T017-T024 test shells in test/IdentificationTests.cs"
Task: "T026 Stem and Log declarations" and "T027 context row declarations" (after T025)
```

## Implementation Strategy

### MVP First (User Story 2)

1. US5 findings (done), then Setup (T005-T006) and the fragment (T007).
2. US2 shells, gate, implementation: identified Stem and Log rows.
3. Stop and validate with a real document.

### Incremental Delivery

US5 → US2 → US4 → US3 → US6 → US7 (dimensions) → US8 (stem metrics); schema version after the POC. Each story ends with
`dotnet test` green on everything before it.

## Notes

- Board stories: #91 (US1), #93 (US2), #94 (US3), #95 (US4), #96 (US5), #97 (US6), #98 (US7), #108 (US8); keep
  each issue's Requirements checklist in sync with this file.
- D1-D2 were Forestry.Deserialize spec 002 (#99; stories #100-#102), merged via #104.
