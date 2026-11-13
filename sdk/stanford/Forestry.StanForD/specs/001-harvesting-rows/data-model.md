# Data Model: Harvesting rows

Spec: [spec.md](spec.md) · Research: [research.md](research.md)

All members are single values (C0). Names follow the StanForD element names where one exists.
Members marked *(US7)* wait on the query groups (US6) and carry a trace to their use (C3).

## Enumerated rows

Drafted in `src/` during US5 (2026-11-13, [findings-us5.md](findings-us5.md)); the code is the
source of each member's StanForD origin (its doc comment). Each row has four regions:
**identification**, **partitioning**, **metrics** and **dimensions**. Members copied from a common
definition sit next to the key that refers to it. Set by: *name* = read directly by element name;
*policy* = set by an identification, partition or conditional-mapping policy (none written yet).

### Stem (`src/Stem.cs`)

| Region | Members | Set by |
|---|---|---|
| identification | `Id` (`string`, `{machine key "N"}-{stem key}`), `StemKey` (`int`), `MachineKey` (`Guid`) | `StemKey` name; others policy |
| partitioning | `HarvestingCreationTime` (`DateTimeOffset`, UTC, header `CreationDate`), `ContractNumber` (`string`) | policy |
| metrics | `ProcessingCategory`, `Latitude`, `Longitude` (signed `decimal` degrees), `Altitude`, `CoordinateReferenceSystem`, `BoomAngleFelling`, `BoomAngleLastLogCut`, `BoomExtensionFelling`, `BoomExtensionLastLogCut` (`int`; 65535 is the schema's default value) | name or policy (coordinates and boom positions sit below the stem) |
| dimensions | `ObjectKey`, `ObjectName`, `SpeciesGroupKey`, `SpeciesGroupName`, `OperatorKey`, `OperatorBusinessName` | keys by name; names by policy from the definitions |

### Log (`src/Log.cs`)

| Region | Members | Set by |
|---|---|---|
| identification | `Id` (`string`, `{machine key "N"}-{stem key}-{log key}`), `LogKey`, `StemKey` (`int`), `MachineKey` (`Guid`) | `LogKey` name; others policy |
| partitioning | `HarvestingCreationTime`, `ContractNumber` | policy |
| metrics | `MachineVolumeOverBark`, `MachineVolumeUnderBark`, `MachineVolumePrice` (`decimal`); `MachineLength` and eight `MachineDiameter…` members (`int`, 0 when not measured) | conditional mapping |
| dimensions | `DiameterUnit`, `LengthUnit`, `VolumeUnit` (root attributes, not converted); `ObjectName`; `ProductKey`, `ProductName`, `ProductNamespace`, `ProductVersion`; `CuttingReason`; `TopSawing`; `FindButtEndFunction` | `ProductKey`, `TopSawing`, `FindButtEndFunction` by name; others policy |

### Partitioning sources (US3, FR-006, FR-007, FR-009)

| Member (Stem and Log) | Source | Set when |
|---|---|---|
| `HarvestingCreationTime` | `HarvestedProductionHeader/CreationDate`, converted to UTC (the document keeps the machine's offset) | the row completes; no creation date fails |
| `ContractNumber` | `ObjectDefinition/ContractNumber` of the object the stem's `ObjectKey` refers to (a log through its stem) | the row completes; a stem with no `ObjectKey` fails |
| `ObjectName` (a dimension) | `ObjectDefinition/ObjectName`, same object | the row completes |

`HarvestedProductionHeader` is a row type that never comes out, kept until the document ends.

### Common definitions (`src/*Definition.cs`, `src/Machine.cs`)

| Row | Identification | Members the rows copy |
|---|---|---|
| `Machine` | `MachineKey` | — |
| `ObjectDefinition` | `Id`, `ObjectKey`, `MachineKey` | `ObjectName`, `ContractNumber` |
| `SpeciesGroupDefinition` | `Id`, `SpeciesGroupKey`, `MachineKey` | `SpeciesGroupName` |
| `OperatorDefinition` | `Id`, `OperatorKey`, `MachineKey` | `OperatorBusinessName` (from `ContactInformation`, by policy; never personal details) |
| `ProductDefinition` | `Id`, `ProductKey`, `MachineKey` | `ProductName`, `ProductNamespace` (`ProductInfo`), `ProductVersion` (from `ClassifiedProductDefinition`, by policy) |

Traces to query groups (C3) are added at US7, after US6.

## Context rows (internal, never enumerated)

Read like any row, held in transient memory by their policies, never handed to the caller. Expect
the list to grow (research R2).

| Row | Members read | Used by |
|---|---|---|
| `HarvestedProduction` (root) | `DiameterUnit`, `LengthUnit`, `VolumeUnit` (attributes); `Version` only after the POC | unit dimensions on Log |
| `HarvestedProductionHeader` | `CreationDate` | partitioning (`HarvestingCreationTime`, UTC) |
| `Machine` | `MachineKey`, `MachineUserID` | Identification |
| `ObjectDefinition` | `ObjectKey`, `ObjectName`, `ContractNumber` | Identification (defined keys), partitioning |
| `SpeciesGroupDefinition` | `SpeciesGroupKey`, `SpeciesGroupName` | Identification, dimensions |
| `ProductDefinition` | `ProductKey`; names via `ClassifiedProductDefinition` | Identification, dimensions |
| `OperatorDefinition` | `OperatorKey`; business name via `ContactInformation` (never personal details) | Identification, dimensions |
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
