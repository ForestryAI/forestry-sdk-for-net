# Implementation Plan: Harvesting rows

**Branch**: `stanford-001-us2` | **Date**: 2026-11-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-harvesting-rows/spec.md`

## Summary

Turn StanForD Harvesting documents into flat Stem and Log rows that are globally identified,
timestamped and filled by conditional mapping. The package adds Stem and Log row types and many
internal **context rows** (root, header, machine, definitions, `LogVolume`, `LogMeasurement`,
`LogDiameter`, ...) that are read but never enumerated; their policies hold them in transient
memory and set members on the open Stem or Log. Identification and timestamps are member-level
`Set` policies on key members; conditional mapping happens when a context row is enumerated. All
of it is configured in one call. Forestry.Deserialize needs two capabilities first (research R1):
nested rows of several types (D1), and attributes and a row's own content as members (D2).

## Technical Context

**Language/Version**: C# on the SDK's supported target frameworks (net10.0, engineering constitution)

**Primary Dependencies**: Forestry.Deserialize, Forestry.Deserialize.Xml (project references, A1); no third-party packages

**Storage**: N/A (rows are handed to the caller; the final format is the user's)

**Testing**: xUnit through Forestry.Deserialize.Xml with short handwritten Harvesting fragments; real documents from a local folder (`STANFORD_HPR_FOLDER`) for end-to-end tests, skipped when unset; `Category=Performance` left out by `Forestry.StanForD.Tests.runsettings`

**Target Platform**: .NET library (NuGet package, shipping library)

**Project Type**: library

**Performance Goals**: deferred to the performance spec; this spec keeps policy state bounded (C5)

**Constraints**: single values per member (C0); policy state never grows with stems or logs (C5); no XML reading in this package (C4); rows never carry operator contact details

**Scale/Scope**: real documents of about 1 MB, 66 stems and 278 logs (Verified, one 2026-01-14 document); production volumes are big data

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Rule | Check | Status |
|---|---|---|
| C0 Big data format | Stem and Log members are single values (`Guid`, `long`, `int`, `decimal?`, `DateTimeOffset`); no nested objects (data-model.md) | Pass |
| C1 Every row identified | Identification policy sets machine, stem and log keys (R3) | Pass |
| C2 Every row timestamped | Object start date, definition modification dates and header creation date, each with a named source (R4) | Pass |
| C3 Members serve query groups | Initial members traced; the rest wait on US6 and are traced at US7 (data-model.md) | Pass, completed at US7 |
| C4 Interpretation as policies | Context rows and their policies; no reader use; gaps closed in Forestry.Deserialize (R1) | Pass, depends on D1-D2 |
| C5 Size affects only time | Policy state bounded by definitions and one stem (data-model.md) | Pass |
| C6 Test-first | Tasks put test shells and a review gate before code in every code story | Pass (tasks) |
| C7 Design ownership | Every research decision is Proposed | Pass |
| D0 Documentation | XML docs, README and ARCHITECTURE updated per story | Pass (tasks) |
| S0 No silently wrong values | Strict by default; unmatched and duplicate occurrences fail (R5) | Pass |
| A1 References | Forestry.Deserialize and Forestry.Deserialize.Xml only | Pass |
| A2 Schemas | `schemas/` keeps V4p1 only, for the POC (developer, 2026-11-13); real documents are 3.5, read within FR-019's window by the version constraint policy (R6); types are checked against the real documents | Pass (schema files decided; the document window is US5's T011) |
| A4 Tests | Fragments in tests; real documents end-to-end only, not committed (R8) | Pass |

**Post-design re-check**: no violations. Answered by the developer (2026-11-11): an `Id` string
member (R3), `LogMeasurement` as a one-member context row (R5), a window of the two latest schema
versions (R6), extensions as a Forestry.Biometria add-on, out of scope (R7). Still to confirm in
US5: which Harvesting versions are the two latest, since the real documents are 3.5.

## Project Structure

### Documentation (this feature)

```text
specs/001-harvesting-rows/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/public-api.md
└── tasks.md             # /speckit-tasks
```

### Source Code

```text
sdk/stanford/Forestry.StanForD/
├── src/
│   ├── Forestry.StanForD.csproj          # + references to Forestry.Deserialize(.Xml)
│   └── Harvesting/
│       ├── Stem.cs, Log.cs                # enumerated rows
│       ├── Context/                       # internal rows: root, header, machine, definitions, LogVolume, ...
│       ├── Policies/                      # identification, timestamps, conditional mapping, schema version
│       └── HarvestingOptions.cs           # AddHarvesting, UnmatchedOccurrence
├── test/
│   ├── Fragments/                         # handwritten Harvesting fragments
│   └── *Tests.cs                          # one file per story
└── docs: README.md, ARCHITECTURE.md

sdk/deserialize/                           # D1-D2 land here, under its own constitution and spec
```

**Structure Decision**: one package, Harvesting under `Forestry.StanForD.Harvesting`. The existing
`MachineType`, `DiameterUnitType` and `ValidationExtensions` stay until US7 decides (R9).

## Story order and dependencies

| Story | Needs | Can start |
|---|---|---|
| US2 Identification | D1 | after D1 in Forestry.Deserialize |
| US4 Conditional mapping | D1, D2 | after D2 |
| US3 Timestamps | US2; US5 answer | after US2 |
| US5 Investigation | real documents, schema 3.5 | now |
| US6 Query groups | none | now |
| US7 Row types | US2-US4, US6 | last |

US5 and US6 need no code and can run while D1-D2 are built.

## Complexity Tracking

No constitution violations. The cross-silo dependency on Forestry.Deserialize (D1-D2) is the main
risk: it is tracked in research R1 and needs its own Forestry.Deserialize spec.
