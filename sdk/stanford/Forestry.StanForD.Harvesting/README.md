# Forestry StanForD Harvesting

The Harvesting knowledge of the Forestry SDK: everything a client needs to turn StanForD Harvesting
documents (`.hpr`) into big data, without the client itself knowing StanForD. Nothing is built yet;
this README states the goals. The SDK's [README](../../../README.md#big-data-clients) explains the
whole path from a device document to big data, and where this package plugs in.

## Why a package of its own

[Forestry.StanForD](../Forestry.StanForD/README.md) flattens StanForD documents into rows. What a
document measures depends on its message: a Harvesting document measures stems and logs, while a
Forwarding document measures loads. The dimensions those metrics are copied with (object, species
group, product, operator, machine) are shared by all of StanForD's production messages. So the shared
part stays in Forestry.StanForD, and each message gets its own package, starting with this one.

A client gets Harvesting by adding this package to its options. Each part comes with defaults that
the client can adjust.

## Where it is heading

Not a complete list: an idea of what is particular to Harvesting.

### Moved from Forestry.StanForD

- **Metrics**: the `Stem` and `Log` rows, the Harvesting header, and the policies that make each row
  self-contained (identification, partitioning, conditional mapping of measurements, stem metrics,
  dimensions copied onto stems and logs).
- **Options**: `AddHarvesting` and `HarvestingOptions`.

### Deserialization policies, per row

- **Validation** of what Harvesting requires. These halt deserialization with an exception naming the
  stem or log they concern:
  - the machine key is a GUID (mandatory), and the machine user ID, when present, is a number;
  - the measuring time is neither too old nor in the future (mandatory, with configurable limits);
  - the product ID is not empty;
  - the contract is not empty and is made of digits;
  - the product's destination exists, since without it the contract cannot be associated with other
    agreements.

  Most of the others are data washing and checks that relationships exist, such as definitions being
  present before the rows that refer to them.
- **Operator measurements** (M2, beside the machine's M1), deliberately ignored today (#109).
- **Units**: the document's own today; converting them to one unit per column is an option to come.

### Pipeline directives, per chunk or input

- **Partition folders**, e.g. `/{year}/{month}/harvesting/{contract}/`. The keys come from each row,
  so one document can land in several partitions.
- **File names per chunk**, e.g. by stem key range: `stem-{first stem key}-{last stem key}` and
  `log-{first stem key}-{last stem key}`.
- **Document checks** before deserialization, such as the StanForD version a document declares.

### Monitoring

Rather than an answer, a Harvesting ingestion is followed through monitoring: exceptions, metrics
(documents, stems and logs read, chunks written) and events (an unmatched category, a document that
failed). In time, a monitoring library (Forestry.Monitoring, or a part of Forestry.Core) will let
the client decide where monitoring goes, likely through a pipeline directive. Until then, this
package writes basic console output.

## Biometria builds on it

Anything particular to Biometria goes in Forestry.Biometria, not here. That includes the `Extension`
elements written by Sender, the program the Swedish forest industry uses to move documents from the
machine to Biometria. Forestry.Biometria will have its own `AddHarvesting`: Stem and Log rows that
extend these with Biometria's members, plus its own policies and directives, such as checking
production planners.

## What it will not do

- **No client, resource or output format.** Those belong to the clients that use this package.
- **Nothing Biometria.** That is Forestry.Biometria's, built on this package.
- **No other messages.** Only Harvesting (`HarvestedProduction`). Harvesting quality control (`.hqc`),
  which only checks volumes, and the other production messages are out of scope.
- **No removal of duplicates.** Each Harvesting document repeats the previous document's last stem,
  with the same `Id`, and a client may run on several nodes at once. Duplicates are ingested as
  historical big data, into storage that combs them out before the data is made available for
  analysis (e.g. to Azure Data Explorer). The client pipeline does not handle them.

## Status

Not started: no source and no `.csproj` yet. It comes after writing rows as JSON Lines and the first
local client; see [where the POC stands](../../../README.md#where-the-poc-stands).
