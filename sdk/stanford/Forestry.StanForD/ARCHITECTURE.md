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

## 2. Guiding Principles

**Deserialization from XML to a big data format.** The target is not an object model of the document
but rows that suit a column-based format. In this POC that is [Parquet](https://www.databricks.com/blog/what-is-parquet),
read by Azure Data Explorer (ADX) through
[external tables over Azure Blob Storage](https://learn.microsoft.com/en-us/azure/data-explorer/external-azure-storage-tables-query).
Kusto queries are fastest against cached ADX data, next fastest against partitioned Parquet. A row
type is therefore flat, like a record, with members that are primitive C# types or single-value
structs (e.g. `Guid`, `DateTimeOffset`). Flattening StanForD is a conscious design, not a one-to-one mapping of XML values
to C# types.

**Analytics by group, usually an actor.** Questions are asked per operator, machine, contract, species
group or product, and over time. Which members a row carries follows from the queries expected against
it, collected and grouped (#97), not from what the schema happens to offer.

**Every row is identified.** Identification is global:
- A machine has a globally unique key (GUID).
- Stem keys are running numbers created by the machine and never reset; log keys are unique within a
  stem.
- Common definitions (contract, species group, product, operator, ...) are external entities loaded
  into the machine. Their **User Id** is the external reference; their **key** is a running number
  created by the machine.
- So machine key + key identifies a stem or a common definition globally, and machine key + stem key
  + log key a log.

**Every row is timestamped.** Loading a common definition gives it a creation timestamp (new entity) or
a modification timestamp (existing entity). Stems and logs have no timestamp of their own; a stem
references a contract (object), which has a start date. Timestamps keep the data historical, and a
subset of them can become partitions, which has a major impact on queries. Whether the contract start
date changes each time the machine starts cutting is being investigated (#96).

**Identification and timestamps follow references.** They come from elements earlier in the document,
not from the row's parent: a stem links to its definitions through keys (`ObjectKey`,
`SpeciesGroupKey`, `ProductKey`, `OperatorKey`), and the definitions are siblings before the stems.

**Conditional mapping.** StanForD repeats elements by name and tells them apart by the element's own
attributes. In a real `hpr` document `LogVolume` occurs three times by `logVolumeCategory`, and
`LogDiameter` eight times by `logDiameterCategory`. Element content is mapped to a row member by
evaluating those attributes, possibly several at once (e.g. volume category and measurement
category).

**Document size affects only time.** Deserializing a large document must not change CPU or memory use
per row, only how long the enumeration takes.

## 3. Scope

- **In**: StanForD 2010 Harvesting documents (`hpr`) in XML. Harvesting holds the most important
  metrics (the value of cut products, how much of a contract is cut over time, seasonal effects such
  as measurement quality when tree sap is highest in February), so it has the widest variety of
  query kinds and shows the basic functionality on its own.
- **Out**: the other StanForD document families (Forwarding, Quality, Production Instructions);
  StanForD's older, non-XML format; writing rows to storage (the Azure Blob sink is later work).

## 4. Module Boundaries

- **`Forestry.StanForD`**: this package. Harvesting row types and the policies that interpret
  StanForD (identification, timestamps, conditional mapping).
- **`Forestry.Deserialize` / `Forestry.Deserialize.Xml`**: the deserialization package and its XML
  media package. They read the document, build rows and call policies (`Construct`, `Set`,
  `Enumerate`, `Clear`). Forestry.StanForD will reference them once its row types exist (spec 001);
  today it references nothing beyond the BCL.
- **Policies first, attributes later**: StanForD's interpretation lives in policies in this package.
  Generic XML C# attributes that create such policies may come to the deserialization packages later.

**Stability:** very early / experimental.

## 5. Core Contracts

### 5.1 Row types (planned, #98)

`Stem` and `Log` rows extending `Forestry.Deserialize.Row`, with members chosen from the query groups
(#97). Until then, minimal Stem and Log rows serve as test rows for the policies.

### 5.2 Policies (planned)

| Policy | Story | What it does |
|---|---|---|
| Identification | #93 | Holds the machine key and current keys, sets the global identification on each row |
| Timestamps | #94 | Carries creation / modification timestamps from referenced definitions to rows |
| Conditional mapping | #95 | Holds an element's attributes until its content arrives, then sets the matching member; unmatched occurrences are ignored or fail, by option |

### 5.3 Existing types

`MachineType` and `Metrics/DiameterUnitType` use `[XmlType]` / `[XmlEnum]` from
`System.Xml.Serialization`, with `ValidationExtensions.Validate()` catching required data that
`XmlSerializer` leaves missing. They predate the row model; whether they stay, change or go is decided
with the row types (#98).

## 6. Schema Findings

`schemas/` holds Skogforsk's StanForD 2010 schemas at the latest version, V4p1, for the POC: every
message (Harvesting's `HarvestedProduction` included), the common definitions and the envelope, each
also in a `_no_documentation` variant (2026-11-13; the older common-definitions versions `V1p0` to
`V4p0` were removed). Found while they were here, verified by comparing type names, not assumed:
`V4p0`→`V4p1` is purely additive (7 new types), but `V3p6`→`V4p0` removed 13 types and three earlier
version bumps each removed at least one. A type built against an older version is not assumed valid
against `V4p1` without checking.

## 7. Status / POC Debt

- No row types or policies yet; spec 001 (#90) is starting.
- `schemas/` has only V4p1, while the real documents found are version 3.5; which versions to
  read is spec 001's FR-019.
- Open: whether the contract start date is updated each time cutting starts (#96).
- Open (shared with Forestry.Deserialize): how definitions handle references across the document's
  hierarchy (key lookups rather than ancestors).
- Which schema versions real documents use, and whether older ones must be read, is not decided.

## 8. Anti-Goals

- Not an object model of StanForD: no hierarchical C# types mirroring the XML.
- Not a general XSD-to-C# compiler.
- Not a storage writer: rows are handed to whoever enumerates them.
- Does not read StanForD's older, non-XML format.

## 9. Related Work Items

- Feature #90: StanForD deserialization (spec 001)
  - #91 US1 C# engineering (done)
  - #93 US2 Identification
  - #94 US3 Timestamps
  - #95 US4 Conditional mapping
  - #96 US5 When Harvesting values are updated
  - #97 US6 Collect and group queries
  - #98 US7 Log and Stem row types
