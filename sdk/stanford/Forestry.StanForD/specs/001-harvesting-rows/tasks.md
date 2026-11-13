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
code, T006 for the first end-to-end test. The fragment (T007) moved to the end of US5.

- [ ] T005 Add project references to `sdk/deserialize/Forestry.Deserialize/src/Forestry.Deserialize.csproj` and `sdk/deserialize/Forestry.Deserialize.Xml/src/Forestry.Deserialize.Xml.csproj` in `src/Forestry.StanForD.csproj` (A1, R9)
- [ ] T006 [P] Add the test helper `RealDocuments` that yields files from `STANFORD_HPR_FOLDER` and skips when unset in `test/RealDocuments.cs` (R8)

**Checkpoint**: the package references the deserialization packages and tests can read real documents.

---

## Phase 3: User Story 5 - When Harvesting values are updated (Priority: P2) 📄 Document

**Goal**: Know which Harvesting values change while the machine works, starting with the contract
start date (#96). **Independent Test**: findings written with sources and reviewed by the developer.

- [X] T008 [P] [US5] Add the Harvesting schemas from Skogforsk to `schemas/` and list which versions exist in `schemas/README.md` (FR-019, R6) (2026-11-13: the developer added the StanForD 2010 schemas at the latest version, V4p1, `HarvestedProduction` included, and removed the older common definitions; "latest only" applies to the schema files, not the documents read; listed in `schemas/README.md`)
- [ ] T009 [US5] Compare `ObjectDefinition/StartDate` and `ObjectModificationDate` across the real documents of one contract over several days, and record whether the start date changes when cutting restarts, with sources, in `specs/001-harvesting-rows/findings-us5.md`
- [ ] T010 [US5] Fill the timestamps-per-definition table (created once or modifiable, element names, seen in real documents) from the schemas and documents in `specs/001-harvesting-rows/findings-us5.md` and update research.md R4
- [ ] T011 [US5] Confirm which two Harvesting schema versions are the latest and whether the real documents (3.5) fall inside them; update spec.md Assumptions and research.md R6
- [ ] T007 [US5] Write the minimal Harvesting fragment from the findings (T009-T011), distilled to what ties identification, stems and logs to a contract (developer, 2026-11-13): `Machine` with `MachineKey`; `ObjectDefinition` with `ObjectKey` and `ContractNumber`; one `Stem` with `StemKey`, `ObjectKey`, `SpeciesGroupKey` and `StemCoordinates` (where, and what kind of tree); one `Log` with `LogKey` and its volume, length and diameter (what was produced); plus a `SpeciesGroupDefinition` only if the findings show the key alone is not enough. Root `version` as in the real documents; one element per line (A4), in `test/Fragments/MinimalHarvesting.xml`. Not real rows: the smallest valid input for the code stories' tests
- [ ] T012 [US5] **Review gate**: the developer reviews `findings-us5.md` and the fragment (T007) on the board (#96)

---

## Phase 4: User Story 6 - Collect and group queries (Priority: P2, ongoing) 📄 Document

**Goal**: A first grouping of likely queries on log and stem data (#97). **Independent Test**: every
group names its actor and lists the filters, groupings and summaries it needs.

- [ ] T013 [P] [US6] Create the extendable query collection (query in plain language, who asks it, group, members it filters/groups/summarizes by) in `specs/001-harvesting-rows/queries.md`
- [ ] T014 [US6] Collect and group queries covering at least product value, contract progress over time, seasonal measurement quality, operator comparisons and machine comparisons (SC-005) in `specs/001-harvesting-rows/queries.md`
- [ ] T015 [US6] For each group, list the Stem and Log members it needs and mark which are already in `data-model.md` in `specs/001-harvesting-rows/queries.md`
- [ ] T016 [US6] **Review gate**: the developer reviews `queries.md` on the board (#97); collecting continues after the story closes

---

## Phase 5: User Story 2 - Identification (Priority: P1) 🎯 MVP

**Goal**: Every Stem and Log row is globally identified (#93). **Independent Test**: two fragments
from two machines with equal stem and log keys give rows whose identifications never repeat.
**Needs**: D1 (met, #104) and US5's fragment (T007).

### Test shells for User Story 2 ⚠️

- [ ] T017 [P] [US2] Test shell, quickstart 1 / US2-1 / FR-001: a Stem row carries `MachineKey`, `StemKey` and `Id` = `{machine key}-{stem key}` in `test/IdentificationTests.cs`
- [ ] T018 [P] [US2] Test shell, quickstart 1 / US2-2 / FR-002: a Log row carries `MachineKey`, `StemKey`, `LogKey` and `Id` = `{machine key}-{stem key}-{log key}` in `test/IdentificationTests.cs`
- [ ] T019 [P] [US2] Test shell, quickstart 2 / US2-4 / SC-001: two fragments, two machines, equal stem and log keys: no `Id` occurs twice in `test/IdentificationTests.cs`
- [ ] T020 [P] [US2] Test shell, edge case "log keys restart per stem": two stems each with log key 1 give two different log `Id`s in `test/IdentificationTests.cs`
- [ ] T021 [P] [US2] Test shell, quickstart 3 / FR-005: a `Stem` before any `MachineKey` fails, naming the stem in `test/IdentificationTests.cs`
- [ ] T022 [P] [US2] Test shell, quickstart 4 / US2-3 / FR-003: a stem referencing an `ObjectKey` not defined earlier fails, naming the stem and the key; a defined one passes in `test/IdentificationTests.cs`
- [ ] T023 [P] [US2] Test shell, FR-004: the same fragment deserialized twice gives the same `Id`s in `test/IdentificationTests.cs`
- [ ] T024 [P] [US2] Test shell, context rows (R2): `Machine` and definition rows are never enumerated in `test/IdentificationTests.cs`

### Review gate

- [ ] T025 [US2] **Test review gate**: the developer reviews T017-T024 on the board before any code (C6)

### Implementation for User Story 2 (after the gate)

- [ ] T026 [P] [US2] Declare the `Stem` and `Log` row types with the identification and key members of data-model.md (`Id` `string`, `MachineKey` `Guid`, `StemKey` `long`, `LogKey` `int`, `ObjectKey`, `SubObjectKey`, `SpeciesGroupKey`, `OperatorKey`, `ProductKey` `long`) in `src/Harvesting/Stem.cs` and `src/Harvesting/Log.cs`
- [ ] T027 [P] [US2] Declare the internal context rows `Machine`, `ObjectDefinition`, `SpeciesGroupDefinition`, `ProductDefinition`, `OperatorDefinition` (`OperatorKey` only, never contact details) in `src/Harvesting/Context/`
- [ ] T028 [US2] Implement the context-row policy: holds `Machine` and definitions by key in transient memory, drops them at `Enumerate` (returns `null`), forgets everything at `Clear` in `src/Harvesting/Policies/ContextPolicy.cs` (R2, C5)
- [ ] T029 [US2] Implement the identification policies: member-level `Set` on `Stem.StemKey` (sets `MachineKey`, `Id`; fails with no machine key) and `Log.LogKey` (sets `MachineKey`, `StemKey`, `Id` from the open stem captured at `Construct`); reference keys fail when not defined earlier, in `src/Harvesting/Policies/IdentificationPolicy.cs` (R3)
- [ ] T030 [US2] Implement `AddHarvesting()` adding `Stem`, `Log`, the context rows and the policies, per contracts/public-api.md, in `src/Harvesting/HarvestingOptions.cs`
- [ ] T031 [US2] Run `dotnet test`: T017-T024 pass

**Checkpoint**: Stem and Log rows are identified globally (MVP).

---

## Phase 6: User Story 4 - Conditional mapping (Priority: P1)

**Goal**: Repeated elements land in the right Log member by their attributes (#95). **Independent
Test**: a log with three volumes and eight diameters fills eleven members; unmatched fails by
default. **Needs**: D1, D2 (met, #104).

### Test shells for User Story 4 ⚠️

- [ ] T032 [P] [US4] Test shell, quickstart 6 / US4-1: three `LogVolume`s by `logVolumeCategory` (`m3sob`, `m3sub`, `m3 (price)`, `logMeasurementCategory="Machine"`) fill `VolumeM3sobMachine`, `VolumeM3subMachine`, `VolumeM3PriceMachine` in `test/ConditionalMappingTests.cs`
- [ ] T033 [P] [US4] Test shell, US4-2 / FR-010: a `LogVolume` matching the volume category but not the measurement category sets nothing (with `Unmatched = Ignore`) in `test/ConditionalMappingTests.cs`
- [ ] T034 [P] [US4] Test shell, quickstart 7 / FR-010: eight `LogDiameter`s under `LogMeasurement logMeasurementCategory="Machine"` fill `DiameterButtOb` … `DiameterTopUb`, and `LogLength` is set, in `test/ConditionalMappingTests.cs`
- [ ] T035 [P] [US4] Test shell, quickstart 8 / US4-4 / FR-012: an unknown `logVolumeCategory` with default options fails, naming the element and its attributes in `test/ConditionalMappingTests.cs`
- [ ] T036 [P] [US4] Test shell, quickstart 9 / US4-3: the same with `Unmatched = Ignore` is ignored and the other members are set in `test/ConditionalMappingTests.cs`
- [ ] T037 [P] [US4] Test shell, quickstart 10 / FR-013: two `LogVolume`s with the same categories in one log fail in `test/ConditionalMappingTests.cs`
- [ ] T038 [P] [US4] Test shell, R2: `LogVolume`, `LogMeasurement` and `LogDiameter` rows are never enumerated in `test/ConditionalMappingTests.cs`

### Review gate

- [ ] T039 [US4] **Test review gate**: the developer reviews T032-T038 on the board before any code (C6)

### Implementation for User Story 4 (after the gate)

- [ ] T040 [P] [US4] Declare the context rows `LogVolume` (`LogVolumeCategory`, `LogMeasurementCategory` attributes, `Value` content), `LogMeasurement` (`LogMeasurementCategory`, its only member) and `LogDiameter` (`LogDiameterCategory`, `Value`) in `src/Harvesting/Context/`
- [ ] T041 [P] [US4] Add the conditional members to `Log` (`decimal?` volumes, `int?` diameters, `int?` `LogLength`) in `src/Harvesting/Log.cs`
- [ ] T042 [US4] Implement the conditional mapping policy: at `Enumerate` of `LogVolume` / `LogDiameter`, match its members (and the open `LogMeasurement`'s category) against the conditions, set the matched member on the open `Log`, drop the context row; `Unmatched` `Fail` (default) or `Ignore`; a second occurrence for the same member fails, in `src/Harvesting/Policies/ConditionalMappingPolicy.cs` (R5)
- [ ] T043 [US4] Add `HarvestingOptions.Unmatched` and `UnmatchedOccurrence` per contracts/public-api.md in `src/Harvesting/HarvestingOptions.cs`
- [ ] T044 [US4] Run `dotnet test`: T032-T038 pass, US2 still green

---

## Phase 7: User Story 3 - Timestamps (Priority: P2)

**Goal**: Every Stem and Log row carries timestamps with named sources (#94). **Independent Test**:
a fragment with dated definitions gives rows with each date. **Needs**: US2, US5 (T009-T010).

### Test shells for User Story 3 ⚠️

- [ ] T045 [P] [US3] Test shell, quickstart 5 / US3-3 / FR-008: a stem referencing an object with `StartDate` carries `ObjectStartDate`, and so do its logs, in `test/TimestampTests.cs`
- [ ] T046 [P] [US3] Test shell, US3-1/US3-2 / FR-007: creation and modification dates of referenced definitions (per the US5 table) are carried and kept apart in `test/TimestampTests.cs`
- [ ] T047 [P] [US3] Test shell, FR-006 / SC-002: every Stem and Log row has `DocumentCreationDate` from the header in `test/TimestampTests.cs`

### Review gate

- [ ] T048 [US3] **Test review gate**: the developer reviews T045-T047 on the board before any code (C6)

### Implementation for User Story 3 (after the gate)

- [ ] T049 [P] [US3] Declare the internal context row `HarvestedProductionHeader` (`CreationDate`) and add the date members of the definitions per the US5 table in `src/Harvesting/Context/`
- [ ] T050 [US3] Add the timestamp members (`DateTimeOffset` / `DateTimeOffset?`) to `Stem` and `Log` per data-model.md in `src/Harvesting/Stem.cs` and `src/Harvesting/Log.cs`
- [ ] T051 [US3] Implement the timestamp policy: when a reference key is set, copy the referenced definition's dates; set `DocumentCreationDate` from the header, in `src/Harvesting/Policies/TimestampPolicy.cs` (R4)
- [ ] T052 [US3] Record each timestamp's source and the partition candidates (FR-006, FR-009) in `specs/001-harvesting-rows/data-model.md`
- [ ] T053 [US3] Run `dotnet test`: T045-T047 pass, US2 and US4 still green

---

## Phase 8: Schema version constraint (FR-019, part of US7's readiness)

- [ ] T054 [P] [US7] Test shell, FR-019: a root `version` outside the two latest versions fails by default, is read with the option, and one inside passes, in `test/SchemaVersionTests.cs`
- [ ] T055 [US7] **Test review gate**: the developer reviews T054 on the board (C6)
- [ ] T056 [US7] Declare the root context row `HarvestedProduction` (`Version` attribute) and implement the schema version policy with a configurable number of versions in `src/Harvesting/Context/HarvestedProduction.cs` and `src/Harvesting/Policies/SchemaVersionPolicy.cs` (R6)

---

## Phase 9: User Story 7 - Log and Stem row types (Priority: P3)

**Goal**: Stem and Log rows with members from the query groups (#98). **Independent Test**: a real
document gives one row per stem and per log, every member traced. **Needs**: US2-US4, US6.

### Test shells for User Story 7 ⚠️

- [ ] T057 [P] [US7] Test shell, quickstart 11 / US7-1 / FR-018: a real document (`STANFORD_HPR_FOLDER`) gives one Stem row per `Stem` and one Log row per `Log` in `test/EndToEndTests.cs`
- [ ] T058 [P] [US7] Test shell, quickstart 12 / C0 / FR-016: every public member of `Stem` and `Log` is a single value (primitive or single-value struct, or `string`) in `test/RowShapeTests.cs`
- [ ] T059 [P] [US7] Test shell, C3 / FR-017: every member of `Stem` and `Log` appears in the trace column of `data-model.md` in `test/RowShapeTests.cs`

### Review gate

- [ ] T060 [US7] **Test review gate**: the developer reviews T057-T059 on the board before any code (C6)

### Implementation for User Story 7 (after the gate)

- [ ] T061 [US7] Choose the remaining Stem and Log members from `queries.md` and record each with its trace in `specs/001-harvesting-rows/data-model.md`
- [ ] T062 [US7] Add those members, with context rows (e.g. `SingleTreeProcessedStem`) where they come from below the row, in `src/Harvesting/`
- [ ] T063 [US7] Decide with the developer whether `MachineType`, `DiameterUnitType` and `ValidationExtensions` stay; remove them if not, in `src/` (R9)
- [ ] T064 [US7] Run `dotnet test` with `STANFORD_HPR_FOLDER` set: T057-T059 pass, all earlier stories green

---

## Phase 10: Polish & Cross-Cutting Concerns

- [ ] T065 [P] XML doc comments on every public member in `src/Harvesting/` (D0)
- [ ] T066 [P] Update the usage, rows and policies sections in `README.md` and `ARCHITECTURE.md` (D0)
- [ ] T067 [P] Add the release notes in `CHANGELOG.md`
- [ ] T068 Run every quickstart row and record the result in `specs/001-harvesting-rows/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **US1 (Phase 1)**: done.
- **Setup (Phase 2)**: right before the first code story (US2).
- **US5, US6 (Phases 3-4)**: no dependencies; next. US5's T007 (the fragment) comes after its findings.
- **US2 (Phase 5)**: Setup + US5's fragment (T007); D1 met.
- **US4 (Phase 6)**: Setup; D1 and D2 met. Independent of US2 except the shared `Log` file.
- **US3 (Phase 7)**: US2 (reference keys) + US5 (T009-T010).
- **Schema version (Phase 8)**: US5 (T011); D2 met.
- **US7 (Phase 9)**: US2, US3, US4, US6.
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

1. US5 (findings and the fragment, T007), then Setup (T005-T006).
2. US2 shells, gate, implementation: identified Stem and Log rows.
3. Stop and validate with a real document.

### Incremental Delivery

US5 → US2 → US4 → US3 → schema version → US7 (needs US6). Each story ends with
`dotnet test` green on everything before it.

## Notes

- Board stories: #91 (US1), #93 (US2), #94 (US3), #95 (US4), #96 (US5), #97 (US6), #98 (US7); keep
  each issue's Requirements checklist in sync with this file.
- D1-D2 were Forestry.Deserialize spec 002 (#99; stories #100-#102), merged via #104.
