# Research: Harvesting rows

Spec: [spec.md](spec.md) · Plan: [plan.md](plan.md)

Every decision here is **Proposed** until the developer confirms it (constitution C7). Findings
marked **Verified** were checked against code, a schema or a real document on 2026-11-11, not
assumed.

## R0. What Forestry.Deserialize can do today (Verified)

- **Decision**: StanForD spec 001 builds on Forestry.Deserialize as it is after its spec 001
  (walking skeleton), and lists what it still needs from it (R1).
- **Findings** (`Forestry.Deserialize` README, `XmlRowReading.cs`, walking-skeleton spec and
  research R12, R13, R15):
  - Rows extend `Row`; several row types can be added to the options; policies attach by attribute
    or options; `EnumerationPolicy` has `Construct`, `Set<T>`, `Enumerate` (may return `null`, so
    the row is not enumerated) and `Clear`; a member-level policy may override `Set`.
  - Association is by name only (R13 A): an element named like a row type starts a row wherever it
    is; only a **direct child** element named like a member sets it.
  - **Attributes are not read**, and a row's **own element content** never sets a member: the XML
    media handles elements, child text and end tags only.
  - The media keeps a stack of open rows, but rows of several types open at once (a `LogVolume`
    built while its `Log` is open) have no test; the walking-skeleton spec names "several classes
    in one pass" a later slice.

## R1. What Forestry.Deserialize must add

- **Decision (proposed)**: Context rows (R2) remove most of the earlier list. Two capabilities
  remain, and both belong to the next Forestry.Deserialize slice, driven by this spec:

  | # | Capability | Example | Blocks |
  |---|---|---|---|
  | D1 | Rows of several types open at the same time, nested, each completed at its own end tag, all within one deserialization of one document | `Machine` open → `Stem` open → `Log` open → `LogVolume` built and completed → `Log` completed | US2, US3, US4, US7 |
  | D2 | A row's attributes and its own element content as members | `<LogVolume logVolumeCategory="m3sob" logMeasurementCategory="Machine">0.59</LogVolume>` gives a `LogVolume` row with `LogVolumeCategory`, `LogMeasurementCategory` and `Value` | US4, R6 |

- **Dropped (2026-11-11 review)**:
  - *Members below a direct child*: a context row for the intermediate element
    (`LogMeasurement`, `SingleTreeProcessedStem`) covers it.
  - *Attributes handed to policies with each value*: attributes become members of context rows
    instead (D2).
  - *One element feeding several members by condition*: e.g. three `LogVolume` elements feeding
    `VolumeM3sob`, `VolumeM3sub`, `VolumeM3Price` on `Log` by name association would need three
    `Log` members read from the same element name, which fails when the options lock. A `LogVolume`
    context row whose policy sets the matched `Log` member avoids it.
  - *A typed policy for one row type*: a member-level `Set` policy on a key member covers
    identification and timestamps (R3, R4); policies cast to the package's own row types.
- **Rationale**: Constitution C4 forbids this package reading XML itself; the two remaining gaps
  are media capabilities.

## R2. Context rows

- **Decision (proposed)**: A **context row** is a row type internal to the package, added to the
  options but never enumerated (its policy's `Enumerate` returns `null`). Expect many:

  | Context row | Members | Its policy |
  |---|---|---|
  | `HarvestedProduction` (root) | `Version` (attribute) | Schema version constraint (R6) |
  | `HarvestedProductionHeader` | `CreationDate` | Timestamps |
  | `Machine` | `MachineKey`, `MachineUserID` | Identification (key at `Set`, since `Machine` stays open) |
  | `ObjectDefinition`, `SpeciesGroupDefinition`, `ProductDefinition`, `OperatorDefinition` | key, user id, timestamps per R4 | Held as transient memory for Identification and Timestamps |
  | `SingleTreeProcessedStem` | e.g. `DBH` | Sets Stem members at `Enumerate` |
  | `LogVolume` | `LogVolumeCategory`, `LogMeasurementCategory`, `Value` | Conditional mapping onto the open `Log` |
  | `LogMeasurement` | `LogMeasurementCategory` | Held while its `LogDiameter`s are read |
  | `LogDiameter` | `LogDiameterCategory`, `Value` | Conditional mapping onto the open `Log` |

- **Transient memory**: the policies hold the open enumerated rows (current `Stem`, `Log`, captured
  at `Construct`) and the context rows still needed (the machine, the definitions by key, the open
  `LogMeasurement`). Released when no longer needed; all cleared at `Clear`.
- **Rationale**: Every value a Stem or Log needs is either before the stems (machine, definitions;
  Verified) or inside the row's own element; context rows reuse name association and keep
  enumerated rows flat (C0).
- **Watch**: name association matches a row "anywhere", so a context row name used in several
  places (e.g. `Extension`, R7) needs path association later (Forestry.Deserialize R13 B).

## R3. Identification (US2)

- **Decision (proposed)**: Member-level `Set` policies on key members:
  - `Stem.StemKey`: when set, reads the machine key from transient memory and sets
    `Stem.MachineKey`; fails if no machine key is held (FR-005).
  - `Log.LogKey`: sets `Log.MachineKey` and `Log.StemKey` from the open stem.
  - Reference keys (`ObjectKey`, `SpeciesGroupKey`, `OperatorKey`, `ProductKey`): when set, look up
    the definition in transient memory; fail if not defined earlier.
- **Id member (developer, 2026-11-11)**: in StanForD a key by itself or in combination is globally
  unique. When a stem or log key is set, its policy reads the machine (a context row) from transient
  memory and sets an `Id` string member: `{machine key}-{stem key}` on a Stem,
  `{machine key}-{stem key}-{log key}` on a Log. The separate keys stay as members too.
- **Rationale**: Keys are running numbers per machine; the machine key makes them global (C1).
  State is bounded by definitions and one stem (C5).

## R4. Timestamps per common definition (US3)

- **Decision (proposed)**: Timestamps are defined per common definition: which date it has, and
  whether it is created once or can be modified. Draft from the V4p1 common-definitions schema
  (Verified by element name) and the real document:

  | Definition | Date element | Kind | In real document |
  |---|---|---|---|
  | Object (contract) | `ObjectModificationDate` | modified | yes |
  | Object (contract) | `StartDate` | set when cutting starts (US5: updated?) | yes |
  | Species group | `SpeciesGroupModificationDate` | modified | yes |
  | Product | `ProductCreationDate` | created once | not seen |
  | Operator | none | — | — |
  | Document header | `CreationDate`, `ModificationDate` | document | yes |

  Others in the schema (`CodeListModificationDate`, `DiameterSectionModificationDate`,
  `GISLayerModificationDate`) are not referenced by stems or logs.
- **Rows**: a Stem or Log carries the dates of the definitions it references, by key, set when the
  key member is set (R3); plus the header's `CreationDate` as a fallback source.
- **Partition candidates (FR-009)**: `ObjectStartDate`, `DocumentCreationDate`.
- **Open for US5**: whether `StartDate` changes when cutting restarts; which definitions are
  re-sent with a newer modification date over a contract's life.

## R5. Conditional mapping (US4)

- **Decision (proposed)**: Conditions belong to context rows' policies. At `Enumerate` of a
  `LogVolume` or `LogDiameter` row, the policy matches its members (and, for a diameter, the open
  `LogMeasurement`'s category) against the conditions, sets the matched member on the open `Log`,
  and drops the context row.
- **LogMeasurement (developer, 2026-11-11)**: a context row with one member,
  `LogMeasurementCategory`. Every `LogDiameter`, when closed, updates a member of the open `Log`.
  - Unmatched: `Fail` (default, FR-012) or `Ignore`.
  - Two occurrences for the same member in one log: fail (FR-013).
- **Spec impact**: FR-010 says "the element's own attribute values"; `LogDiameter`'s measurement
  category is on its parent. With context rows this is the enclosing context row's member, so
  FR-010 reads "the attribute values of the element or its enclosing context row".
- **Findings (Verified)**: `LogVolume` × 3 by `logVolumeCategory` (`m3sob`, `m3sub`,
  `m3 (price)`), all `logMeasurementCategory="Machine"`; `LogDiameter` × 8 by
  `logDiameterCategory` under `LogMeasurement logMeasurementCategory="Machine"`;
  `BoomPositioning` × 2 on the Stem by `boomPositioningCategory`.

## R6. Schema version constraint

- **Findings (Verified)**: both real Harvesting documents declare `version="3.5"`
  (`HarvestedProduction_V3p5.xsd`); the newest common-definitions schema in `schemas/` is V4p1. The
  industry has a set number of years to move to a new schema version.
- **Window (developer, 2026-11-11)**: likely the **two latest** schema versions. No upgrade time is
  set: Skogforsk notes that when a version reaches machines depends largely on what machine users
  demand. So the window is counted in versions, not years.
- **Decision (proposed)**: a **schema version constraint policy** on the root context row: reads
  the `version` attribute (D2) and fails (or, by option, ignores) a document outside the two latest
  versions. The number of versions is configurable.
- **Watch**: the real documents are 3.5. If the two latest Harvesting versions are 4.0 and 4.1,
  they fall outside the window and need the policy's ignore option, or a wider window, until US5
  confirms which Harvesting versions exist.

## R7. Extensions and overrides (new)

- **Finding (Verified)**: `Machine/Extension` holds elements named by the program on the machine
  that aggregates the data. In the real document the "Sender XC" program writes `var2990_t1` to
  `var2990_t17`, where `var2990_t2` (`sdcgpx2036`) is what the machine is known by in Sweden,
  overriding `MachineUserID`.
- **Decision (developer, 2026-11-11)**: extensions are earmarked for the sending program, and
  their meaning comes from places like Biometria. Forestry.StanForD does not interpret them. They
  are an add-on when deserializing: a Forestry.Biometria package (`sdk/biometria/`, not started)
  adds its own `Extension` context row and override policy to the same options.
- **Consequence for this spec**: out of scope. The package only has to leave room for an add-on:
  rows and options must accept extra context rows and policies from another package.

## R8. Tests

- **Decision (proposed)**: xUnit tests through `Forestry.Deserialize.Xml` with short handwritten
  Harvesting fragments, one element per line (A4). Real documents back an end-to-end test, read
  from a folder named by an environment variable and skipped when unset.
- **Rationale**: real documents contain personal data (operator names, e-mail) and are not
  committed.

## R9. Project references and existing types

- **Decision (proposed)**: `Forestry.StanForD` references `Forestry.Deserialize` and
  `Forestry.Deserialize.Xml` (A1). `MachineType`, `DiameterUnitType` and `ValidationExtensions` are
  removed at US7 unless a query group needs them. Rows never carry operator contact details.
