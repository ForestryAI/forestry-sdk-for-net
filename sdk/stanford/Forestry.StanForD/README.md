# Forestry StanForD

This library flattens [StanForD](https://www.skogforsk.se/) documents into telemetries for big data analysis
([what a telemetry is](../../../README.md#telemetry-monitoring-and-observability)).
StanForD is the XML standard published by Skogforsk for exchanging forest machine data; the SDK's
[README](../../../README.md#scandinavian-stanford) explains why it is where the SDK starts.

Production StanForD is big data: machines produce documents every day, and some are very large. The
analysis is grouped, usually by an actor (an operator or production planner), a device (a machine) and
a contract. So instead of mirroring StanForD's deep XML hierarchy as C# objects, this package
flattens each document into self-contained telemetries, which feed a client's pipeline on the way to a big
data resource. The SDK's [README](../../../README.md#big-data-clients) explains the whole path.

It is built on [Forestry.Serialization](../../serialization/Forestry.Serialization/README.md) and its XML
format package, and is also an example of how to flatten a standard with them: everything
StanForD-specific lives in this package's telemetry types and policies, nothing in the deserialization core.
For how it is put together, see [ARCHITECTURE.md](ARCHITECTURE.md).

## Flattening a document

One call sets up the options; then deserialize as with any Forestry.Serialization format. `Stem` and
`Log` are the only metrics, and the only telemetries that come out, in completion order (a stem's logs before
the stem):

```csharp
using Forestry.Serialization;
using Forestry.Serialization.Xml;
using Forestry.StanForD;

DeserializeXmlOptions options = new DeserializeXmlOptions().AddHarvesting();

await using FileStream file = File.OpenRead("harvest.hpr");

await foreach (Telemetry telemetry in TelemetrySerialization.DeserializeAsync(file, options, cancellationToken))
{
    if (telemetry.Is<Stem>())
    {
        Stem stem = telemetry.AsType<Stem>();
        // identification: stem.Id
        // partitioning:   stem.HarvestingCreationTime, stem.ContractNumber
        // metrics:        stem.DBH, stem.Latitude
        // dimensions:     stem.SpeciesGroupName
    }
}
```

`TelemetrySerialization.Deserialize(xml, options)` reads a `string` or UTF-8 bytes synchronously. Use one
options instance per concurrent deserialization (Forestry.Serialization's policies serve one enumeration
at a time).

## Writing stems and logs

`AddHarvesting()` also sets up any format's serialize options: it adds `Stem` and `Log` and the
StanForD enum converters, so enums are written as their StanForD texts (e.g. `"Top break"`) whatever
the format's own enum form. With
[Forestry.Serialization.JsonLines](../../serialization/Forestry.Serialization.JsonLines/README.md),
one line per telemetry, a file per telemetry type:

```csharp
SerializeJsonLinesOptions writing = new SerializeJsonLinesOptions().AddHarvesting();

using FileStream stems = File.Create("stems.jsonl");
using FileStream logs = File.Create("logs.jsonl");

foreach (Telemetry telemetry in TelemetrySerialization.Deserialize(xml, options))
{
    TelemetrySerialization.Serialize(telemetry, telemetry.Is<Stem>() ? stems : logs, writing);
}
```

Reading StanForD texts is tolerant: a text with or without its spaces, in any case, reads to the
member (`"TopBreak"`, `"top break"`). The tests make the round trip, HPR XML → stems and logs → JSON
Lines → parsed and compared member by member, on a fragment and on real documents.

## What a telemetry gives you

Stem and Log show what a machine's metrics expose. So that each telemetry is self-contained, every Stem and
Log telemetry has four kinds of members:

- **Identification**: `Id` is globally unique, `{machine key}-{stem key}` on a stem and
  `{machine key}-{stem key}-{log key}` on a log, with the machine key as a GUID without dashes. The keys
  are there on their own too (`MachineKey`, `StemKey`, `LogKey`).
- **Partitioning**: `HarvestingCreationTime`, when the machine created the document, in UTC (documents
  are incremental, so this is as close to the cut time as the document allows), then `ContractNumber`.
- **Metrics**: on a log, the machine's three volumes, eight diameters and length, told apart by their
  StanForD categories; on a stem, signed coordinates, altitude, boom positions and DBH. A measurement a
  log does not have stays 0, "not measured". Units are the document's (`DiameterUnit`, `LengthUnit`,
  `VolumeUnit`), not converted.
- **Dimensions**: the object, sub-object, species group, operator, machine and (on a log) product and
  destination, with their names and StanForD identifiers. They are copied onto every telemetry, a stem's onto
  its logs too, so no query needs a join.

Every member is a single value.

## How the code is organized

The source is grouped by the same terms, in folders under `src/`; the namespace is `Forestry.StanForD`
throughout:

- **`Metrics`**: `Stem` and `Log`, the telemetries that come out.
- **`Dimensions`**: the object, species group, product and operator definitions and the machine, read
  so their names and identifiers can be copied onto every metric telemetry. They never come out themselves.
- **`Parts`**: telemetries that only carry values one level down, such as a log's volumes and diameters or a
  stem's coordinates, whose policies copy them onto their parent.
- **`Policies`**: identification, partitioning, conditional mapping and dimensions, per telemetry type.

The split also shows what stays shared once Harvesting moves to
[Forestry.StanForD.Harvesting](../Forestry.StanForD.Harvesting/README.md). Metrics depend on the
message: Stem and Log are harvesting metrics, while forwarding has loads. So the metrics, the
Harvesting header and their policies move. Dimensions and parts are shared by StanForD's production
messages, so they stay here.

## Choices

```csharp
DeserializeXmlOptions options = new DeserializeXmlOptions()
    .AddHarvesting(harvesting => harvesting.Unmatched = UnmatchedOccurrence.Ignore);
```

- **Unmatched categories**: a volume, diameter, boom position or coordinate whose StanForD category
  matches no member fails by default (`UnmatchedOccurrence.Fail`), naming the element and its
  categories, so nothing is silently dropped. `Ignore` skips it instead.
- **Operator measurements** (M2, beside the machine's M1) are ignored deliberately for now (#109).

## Failing fast

Deserialization throws an `InvalidDataException`, naming the stem or log and what is missing, when:

- a stem or log has no machine key before it, or a log is not inside a stem;
- a stem or log refers to an object, sub-object, species group, operator or product the document has
  not defined before it;
- the header has no creation date, or a stem has no object key (no contract);
- a measurement matches no member (by default), or two occurrences set the same member;
- a product has more than one destination.

## Duplicates across documents

A Harvesting document repeats the previous document's last stem. Both copies carry the same `Id`, with
different creation times, so they land in two time partitions; remove duplicates downstream by `Id`
(e.g. the latest by `HarvestingCreationTime`).

## Scope

- **Harvesting (`hpr`) only.** Harvesting holds the most valuable metrics (the value of cut products,
  how much of a contract has been cut over time) and the widest range of questions. Forwarding, Quality
  and Production Instructions documents are out of scope for now.
- **The latest schema version only**, V4p1, for the POC; the document's version is not checked. The
  real documents tested against are version 3.5.
- **XML only.** StanForD's older, non-XML format is out of scope.
- **Production, not monitoring.** Work time, fuel and machine effectiveness are in StanForD's
  operational monitoring (MOM) documents; the commercial chain (agreements, production plans, actor
  perspectives) is Biometria's. Neither is read here.

## Status

Spec 001 ([#90](https://github.com/ForestryAI/forestry-sdk-for-net/issues/90)) is implemented: Stem
and Log telemetries with identification, partitioning, conditional mapping of measurements, dimensions and
stem metrics, tested against handwritten fragments and real Harvesting documents. Serialization
spec 005 added writing them (StanForD enum texts) and the round trip to JSON Lines. It is a working
example, fragile in places: see #110 (element names shared by a telemetry type and a member, and telemetries matched
anywhere in the document) and #109 (operator measurements). Performance is a later specification.

`schemas/` holds Skogforsk's StanForD 2010 schemas at V4p1: every message (Harvesting's
`HarvestedProduction` included), the common definitions and the envelope, each also without
documentation.

## Tests

```bash
dotnet test sdk/stanford/Forestry.StanForD/test/Forestry.StanForD.Tests.csproj
```

The tests read small handwritten Harvesting fragments (`test/Fragments/`). The end-to-end test reads
real documents from a local folder named by `STANFORD_HPR_FOLDER` (`*.hpr`, `*.hpr.xml`, `*.hpr.gz`)
and is skipped when it is not set; real documents hold personal data and are never committed.
