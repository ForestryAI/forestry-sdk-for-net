# Forestry StanForD

Turns [StanForD](https://www.skogforsk.se/) Harvesting documents into flat rows for big data analysis.
StanForD is the XML standard published by Skogforsk for exchanging forest machine data.

Production StanForD is big data: machines produce documents every day, and some are very large. The
analysis is grouped, usually by an actor (an operator, a machine, a contract and the parties behind
it), over time. So instead of mirroring StanForD's deep XML hierarchy as C# objects, this package
shreds each document into flat rows. Rows suit a column-based format such as Parquet, read by Azure
Data Explorer in this POC, but the final format is yours: JSON, CSV, another format, or an application
using the rows directly.

For how the package is put together, see [ARCHITECTURE.md](ARCHITECTURE.md).

## What a row gives you

Every row is:

- **Identified globally**: the machine key (a GUID) plus the machine's own running-number keys for
  stems, logs and loaded definitions (contract, species group, product, operator).
- **Timestamped**: from the creation or modification time of the definitions it references, so the
  data stays historical and can be partitioned.
- **Flat**: members are single values (numbers, `Guid`, `DateTimeOffset`, ...), one per column. StanForD
  elements that repeat by name, such as several log volumes, are told apart by their attributes and
  land in their own members.

## Scope

- **Harvesting (`hpr`) only.** Harvesting holds the most valuable metrics (the value of cut products,
  how much of a contract has been cut over time) and the widest range of questions, so it shows
  everything the package does on its own. Forwarding, Quality and Production Instructions documents
  are out of scope for now.
- **XML only.** StanForD's older, non-XML format is out of scope.
- **Rows, not storage.** Writing rows to storage (e.g. Azure Blob) is done by whoever enumerates them.

## Status

Very early. Spec 001 ([#90](https://github.com/ForestryAI/forestry-sdk-for-net/issues/90)) is
starting: identification, timestamps and conditional mapping as policies, then the Log and Stem row
types. `schemas/` holds Skogforsk's StanForD 2010 schemas at the latest version, V4p1: every message
(Harvesting's `HarvestedProduction` included), the common definitions and the envelope, each also
without documentation.
