# Data Model: Harvesting rows

Spec: [spec.md](spec.md) · Research: [research.md](research.md)

All members are single values (C0). Names follow the StanForD element names where one exists.
Members marked *(US7)* wait on the query groups (US6) and carry a trace to their use (C3).

## Enumerated rows

### Stem

| Member | Type | Source | Set by | Trace (C3) |
|---|---|---|---|---|
| `Id` | `string` | `{machine key}-{stem key}` | Identification, when `StemKey` is set | identification (C1), joins |
| `MachineKey` | `Guid` | `Machine/MachineKey` | Identification | identification (C1) |
| `StemKey` | `long` | `Stem/StemKey` | name | identification (C1) |
| `ObjectKey` | `long` | `Stem/ObjectKey` | name | contract filters/groupings |
| `SubObjectKey` | `long` | `Stem/SubObjectKey` | name | *(US7)* |
| `SpeciesGroupKey` | `long` | `Stem/SpeciesGroupKey` | name | species group groupings |
| `OperatorKey` | `long` | `Stem/OperatorKey` | name | operator comparisons |
| `ObjectStartDate` | `DateTimeOffset` | `ObjectDefinition/StartDate` | Timestamps | timeline, partition candidate |
| `ObjectModificationDate` | `DateTimeOffset?` | `ObjectDefinition/ObjectModificationDate` | Timestamps | history (C2) |
| `SpeciesGroupModificationDate` | `DateTimeOffset?` | `SpeciesGroupDefinition/SpeciesGroupModificationDate` | Timestamps | history (C2) |
| `DocumentCreationDate` | `DateTimeOffset` | `HarvestedProductionHeader/CreationDate` | Timestamps | fallback timestamp, partition candidate |
| further members | | e.g. `StemNumber`, `DBH`, coordinates, boom positions | | *(US7)* |

### Log

| Member | Type | Source | Set by | Trace (C3) |
|---|---|---|---|---|
| `Id` | `string` | `{machine key}-{stem key}-{log key}` | Identification, when `LogKey` is set | identification (C1), joins |
| `MachineKey` | `Guid` | `Machine/MachineKey` | Identification | identification (C1) |
| `StemKey` | `long` | enclosing `Stem/StemKey` | Identification | identification (C1) |
| `LogKey` | `int` | `Log/LogKey` | name | identification (C1) |
| `ProductKey` | `long` | `Log/ProductKey` | name | product value groupings |
| `ObjectStartDate` | `DateTimeOffset` | via its stem | Timestamps | timeline |
| `DocumentCreationDate` | `DateTimeOffset` | header | Timestamps | fallback timestamp |
| `VolumeM3sobMachine` | `decimal?` | `LogVolume` [`logVolumeCategory`=`m3sob`, `logMeasurementCategory`=`Machine`] | Conditional mapping | product value |
| `VolumeM3subMachine` | `decimal?` | `LogVolume` [`m3sub`, `Machine`] | Conditional mapping | product value |
| `VolumeM3PriceMachine` | `decimal?` | `LogVolume` [`m3 (price)`, `Machine`] | Conditional mapping | product value |
| `DiameterButtOb` ... `DiameterTopUb` (8) | `int?` | `LogDiameter` [`logDiameterCategory`; enclosing `LogMeasurement` `logMeasurementCategory`=`Machine`] | Conditional mapping | measurement quality |
| `LogLength` | `int?` | `LogMeasurement/LogLength`, via the `LogMeasurement` context row | Conditional mapping | product value |

Member names for conditional members are examples; final names are decided at US7.

## Context rows (internal, never enumerated)

Read like any row, held in transient memory by their policies, never handed to the caller. Expect
the list to grow (research R2).

| Row | Members read | Used by |
|---|---|---|
| `HarvestedProduction` (root) | `Version` (attribute, D2) | Schema version constraint (R6) |
| `HarvestedProductionHeader` | `CreationDate` | Timestamps |
| `Machine` | `MachineKey`, `MachineUserID` | Identification |
| `ObjectDefinition` | `ObjectKey`, `StartDate`, `ObjectModificationDate` | Identification (defined keys), Timestamps |
| `SpeciesGroupDefinition` | `SpeciesGroupKey`, `SpeciesGroupModificationDate` | Identification, Timestamps |
| `ProductDefinition` | `ProductKey`, `ProductCreationDate` | Identification, Timestamps |
| `OperatorDefinition` | `OperatorKey` only (never contact details) | Identification |
| `SingleTreeProcessedStem` | e.g. `DBH` | sets Stem members *(US7)* |
| `LogVolume` | `LogVolumeCategory`, `LogMeasurementCategory` (attributes), `Value` (content) | Conditional mapping onto the open Log |
| `LogMeasurement` | `LogMeasurementCategory` (attribute), its only member | held while its diameters are read; each closed `LogDiameter` updates a Log member |
| `LogDiameter` | `LogDiameterCategory` (attribute), `Value` (content) | Conditional mapping onto the open Log |

## Policy state (per enumeration, cleared at its end)

| State | Bounded by (C5) |
|---|---|
| Machine key | one per document |
| Header creation date | one per document |
| Defined keys and their timestamps, per definition kind | number of definitions |
| Current stem key and its object start date | one stem |
| Open Stem and Log rows (captured at `Construct`) | one stem, one log |
| Open context rows (e.g. `LogMeasurement`) | the document's depth |

## Validation rules

- No machine key held when a Stem starts: fail (FR-005).
- A reference key not defined earlier: fail, naming the stem and the key.
- An occurrence matching no condition: fail by default, or ignore (FR-012).
- Two occurrences for the same conditional member in one row: fail (FR-013).
