# Release History

## 1.0.0-beta.1 (Unreleased)

### Breaking Changes
- Stem, Log and the definition types now extend `Forestry.Serialization.Telemetry` (was `Forestry.Deserialize.Row`): the package it builds on is renamed Forestry.Serialization and *row* is renamed *telemetry* (#142). Entries below keep the names they were written with.

### Features Added
- `HarvestingOptionsExtensions.AddHarvesting()` sets up `DeserializeXmlOptions` to shred StanForD
  Harvesting (`hpr`) documents into `Stem` and `Log` rows, the only rows that come out (spec 001, #90).
  The header, the machine, the common definitions and the elements that carry measurements are read
  as rows that never come out, and their policies set the stems and logs.
- Identification (#93): every stem and log carries `MachineKey`, its keys and a global `Id`,
  `{machine key}-{stem key}[-{log key}]` with the machine key as a GUID without dashes
  (`Identification.Format`). A missing machine key, or a reference to an object, sub-object, species
  group, operator or product the document has not defined before it, throws `InvalidDataException`.
- Partitioning (#94): `HarvestingCreationTime`, the header's creation date in UTC, and `ContractNumber`
  on every stem and log. A missing creation date or object key throws.
- Conditional mapping (#95): a log's three volumes, eight diameters and length are set from their
  StanForD categories, machine (M1) measurements only. A category matching no member throws by
  default, or is ignored with `HarvestingOptions.Unmatched = UnmatchedOccurrence.Ignore`; a member
  set twice throws. Operator (M2) measurements are ignored (#109).
- Dimensions (#98): every stem and log carries its stem number, object, sub-object, species group
  (name, info, user id, version), operator, machine user id and logging contractor; logs also their
  product (name, info, user id, version) and destination. Dimensions are copied onto each row, so no
  query needs a join.
- Stem metrics (#108): signed coordinates (north and east positive), altitude, coordinate reference
  system, boom angle and extension when felling and at the last log cut, and DBH.
- `StanForDEnumConverter<TEnum>` reads StanForD texts with spaces (e.g. "Other manual") into the
  package's enums (`CuttingReason`, `ProcessingCategory`, `CoordinateReferenceSystem`).
- `schemas/` holds the StanForD 2010 schemas at V4p1.

### Behavior Changes
- The package now references `Forestry.Deserialize.Xml` and is a shipping library.

### Other Changes
- Uses Forestry.Deserialize.Xml's renamed attributes, `XmlAttributeName` and `XmlElementText`
  (Forestry.Deserialize spec 003, #127); no behavior change.

### Breaking Changes
- `MachineType`, `DiameterUnitType` and `ValidationExtensions`, `System.Xml.Serialization` types from
  before the row model, are removed. They were never released.

### Known Issues
- A row type and a member sharing an element name are not rejected by Forestry.Deserialize, and row
  types match their element name anywhere in a document; `Latitude` and `Longitude` rely on the first,
  and coordinates outside `StemCoordinates` are ignored for the second (#110).
