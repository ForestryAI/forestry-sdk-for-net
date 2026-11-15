# ARCHITECTURE.md — Forestry StanForD

## 1. Purpose

**Forestry.StanForD** turns StanForD Harvesting documents (`hpr`) into flat rows ready for big data
analysis. StanForD is the XML standard published by [Skogforsk](https://www.skogforsk.se/) for
exchanging forest machine data.

Production StanForD is big data: every working machine produces documents every day, and documents
from live machines can be very large. The analysis it feeds is grouped, usually by an actor (an
operator, a machine, a contract and the parties behind it), over time. So the package does not mirror
StanForD's hierarchy as C# objects; it shreds each document into flat rows whose members could become
columns in a column-based format.

The final format is up to the user. Defining Log and Stem row types does not make Parquet the end
format: rows can just as well be written as JSON, CSV or another format, or used directly by an
application rather than for ADX analysis. The guiding principles below are those of this POC, which
targets ADX.

The package is also an example of shredding a standard with Forestry.Deserialize: everything
StanForD-specific is a row type or a policy here, nothing in the deserialization core.

## 2. Guiding Principles

**Deserialization from XML to a big data format.** The target is not an object model of the document
but rows that suit a column-based format. In this POC that is [Parquet](https://www.databricks.com/blog/what-is-parquet),
read by Azure Data Explorer (ADX) through
[external tables over Azure Blob Storage](https://learn.microsoft.com/en-us/azure/data-explorer/external-azure-storage-tables-query).
Kusto queries are fastest against cached ADX data, next fastest against partitioned Parquet. A row
type is therefore flat, like a record, with members that are single values: primitives, enums or
single-value structs (e.g. `Guid`, `DateTimeOffset`).

**Only Stem and Log come out.** Everything standard in the common definitions becomes Stem and Log
dimensions, copied onto every row (a stem's onto its logs too) rather than joined: Kusto avoids joins,
and Parquet stores a document's repeated values as ranges.

**Analytics by group, usually an actor.** Questions are asked per contract, then a time period, then a
few predefined perspectives (coordinator, receiver, contractor). Which members a row carries follows
from those queries, collected and grouped in `specs/001-harvesting-rows/queries.md` (#97), not from
what the schema happens to offer. Only what the Harvesting document holds is in scope; the commercial
chain is Biometria's, work time and fuel are StanForD's operational monitoring (MOM).

**Every row is identified.** Identification is global:
- A machine has a globally unique key (GUID).
- Stem keys are running numbers created by the machine and never reset; log keys restart for every
  stem.
- Common definitions (object, species group, product, operator) are external entities loaded into the
  machine. Their **User Id** is the external reference; their **key** is a running number created by
  the machine.
- So `Id` is `{machine key "N"}-{stem key}` on a stem and `{machine key}-{stem key}-{log key}` on a log.

**Every row is partitioned by time, then contract.** Stems and logs have no timestamp of their own, and
an object's start date is set once (findings-us5.md). Documents are incremental, so the header's
creation date, in UTC, is as close to the cut time as the document allows: `HarvestingCreationTime`,
then `ContractNumber`. A document repeats the previous document's last stem; both copies share an `Id`
and are removed downstream.

**Conditional mapping.** StanForD repeats elements by name and tells them apart by attributes:
`LogVolume` three times by `logVolumeCategory` and `logMeasurementCategory`, `LogDiameter` eight times by
`logDiameterCategory` under a `LogMeasurement`, `BoomPositioning` twice by `boomPositioningCategory`.
Each such element is read as a row of its own, and its policy sets the matching member of the row it
belongs to. Only machine measurements (M1) are read; operator measurements (M2) are ignored for now
(#109).

**Document size affects only time.** Deserializing a large document must not change CPU or memory use
per row, only how long the enumeration takes. What is held is the open rows and the kept common
definitions, never stems or logs (measuring it is a later specification).

## 3. Scope

- **In**: StanForD 2010 Harvesting documents (`hpr`) in XML, read with row types following the latest
  schema version, V4p1; the document's version is not checked (the real documents tested are 3.5).
- **Out**: the other StanForD document families (Forwarding, Quality, Production Instructions);
  StanForD's older, non-XML format; writing rows to storage (the Azure Blob sink is later work);
  extension elements, which belong to the sending program (an add-on such as a Forestry.Biometria
  package may read them).

## 4. Module Boundaries

- **`Forestry.StanForD`**: this package. Harvesting row types, the rows read for them, converters for
  StanForD texts, and the policies interpreting StanForD; set up with `AddHarvesting()`.
- **`Forestry.Deserialize` / `Forestry.Deserialize.Xml`**: the deserialization package and its XML
  media package (spec 002, the deserialization context). They read the document, build rows, keep rows
  in the deserialization context and call the policies (`Construct`, `Set`, `Complete`, `Clear`).
  Forestry.StanForD references `Forestry.Deserialize.Xml`.
- **Later**: a Forestry.StanForD.Harvesting package with Harvesting-specific validation and
  documentation, leaving here what StanForD document types share.

**Stability:** POC, a working example; fragile in places (section 7).

## 5. Core Contracts

### 5.1 Rows

| Kind | Rows | Comes out |
|---|---|---|
| Enumerated | `Stem`, `Log` | yes |
| Header and machine | `HarvestedProductionHeader` (kept until the end), `Machine` (open around every stem) | no |
| Common definitions | `ObjectDefinition`, `SpeciesGroupDefinition`, `OperatorDefinition`, `ProductDefinition`, `SubObject` (kept until the end) | no |
| Parts of definitions | `ContactInformation`, `ClassifiedProductDefinition`, `ProductDestination`, `LoggingContractor` (copied onto their definition or machine) | no |
| Log measurements | `LogVolume`, `LogMeasurement`, `LogDiameter` (copied onto the open log) | no |
| Stem metrics | `StemCoordinates`, `Latitude`, `Longitude`, `BoomPositioning`, `SingleTreeProcessedStem` (copied onto the open stem) | no |

Members and their StanForD sources: `specs/001-harvesting-rows/data-model.md`.

### 5.2 Policies

All act when a row completes (`Complete`), in `src/Policies/`:

| Policies | Story | What they do |
|---|---|---|
| `MachinePolicy`, `CommonDefinitionPolicy<T>`, `StemIdentificationPolicy`, `LogIdentificationPolicy` | #93 | Keep definitions out and in the context; set `MachineKey`, keys and `Id`; fail on a missing machine key or an undefined reference |
| `HeaderPolicy`, `StemPartitionPolicy`, `LogPartitionPolicy` | #94 | Set `HarvestingCreationTime` (UTC), `ContractNumber`, `ObjectName` |
| `LogVolumePolicy`, `LogMeasurementPolicy`, `LogDiameterPolicy` | #95 | Set the log's volumes, diameters and length by category |
| Definition part policies, `StemDimensionPolicy`, `LogDimensionPolicy` | #98 | Copy dimensions onto stems and logs |
| Stem metric policies | #108 | Set signed coordinates, altitude, coordinate reference system, boom positions, DBH |

### 5.3 Options

`AddHarvesting(Action<HarvestingOptions>? configure)` adds every row type, the StanForD enum converter
(`StanForDEnumConverter<TEnum>`, for texts such as "Other manual") and the policies.
`HarvestingOptions.Unmatched` decides what happens to a category matching no member: `Fail` (default)
or `Ignore`.

## 6. Schema Findings

`schemas/` holds Skogforsk's StanForD 2010 schemas at the latest version, V4p1, for the POC: every
message (Harvesting's `HarvestedProduction` included), the common definitions and the envelope, each
also in a `_no_documentation` variant (2026-11-13; the older common-definitions versions `V1p0` to
`V4p0` were removed). Found while they were here, verified by comparing type names, not assumed:
`V4p0`→`V4p1` is purely additive (7 new types), but `V3p6`→`V4p0` removed 13 types and three earlier
version bumps each removed at least one. A type built against an older version is not assumed valid
against `V4p1` without checking.

Findings from real documents (findings-us5.md and the end-to-end test): the object start date is set
once; documents are incremental and repeat the previous document's last stem; the boom extension is
often 65535, the schema's default value; a `Latitude` also appears in species groups' bark functions.

## 7. Status / POC Debt

- Spec 001 (#90) is implemented and passes against real Harvesting documents.
- **#110**: a row type and a member sharing an element name are not rejected (Forestry StanForD relies
  on it for `Latitude` and `Longitude`), and row types match their element name anywhere in the
  document (handled for coordinates by ignoring those outside `StemCoordinates`).
- **#109**: operator (M2) measurements are ignored.
- Enum conversion lives here until Forestry.Deserialize converts enums.
- The schema version window and its check wait until after the POC.
- Performance on large documents is not measured yet (a later specification).

## 8. Anti-Goals

- Not an object model of StanForD: no hierarchical C# types mirroring the XML.
- Not a general XSD-to-C# compiler.
- Not a storage writer: rows are handed to whoever enumerates them.
- Does not read StanForD's older, non-XML format.

## 9. Related Work Items

- Feature #90: StanForD deserialization (spec 001)
  - #91 US1 C# engineering
  - #93 US2 Identification
  - #94 US3 Partitioning
  - #95 US4 Conditional mapping
  - #96 US5 When Harvesting values are updated (closed early)
  - #97 US6 Collect and group queries
  - #98 US7 Log and Stem dimensions
  - #108 US8 Stem metrics
- Feature #99: Forestry.Deserialize spec 002, the deserialization context this package builds on
- Backlog: #109 operator (M2) measurements, #110 element names shared by a row type and a member
