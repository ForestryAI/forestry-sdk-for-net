# forestry-sdk-for-net

[![ci](https://github.com/ForestryAI/forestry-sdk-for-net/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ForestryAI/forestry-sdk-for-net/actions/workflows/ci.yml)

Forestry SDK for .NET turns telemetries from forestry devices, starting with StanForD Harvesting
documents, into big data ready collections. The end game is observability that opens up insights
into forestry operations.

**Phase:** POC · **Status:** Partial — StanForD Harvesting documents become flat Stem and Log telemetries;
writing those telemetries as JSON Lines files through a local client is next.

## Purpose

### Telemetry, monitoring and observability

Forestry devices (harvesters, forwarders, scales at terminals and mills, ...) are the only sources
this SDK targets. Three terms describe what happens to what they send:

- **Telemetry** is the information a system emits. The word means measuring at a distance (Greek
  *tele*, distant, and *metron*, measure) and comes from the space industry, where measurements such
  as temperature and position were sent back to the ground. It imposes no structure or
  representation: each device emits its telemetries in its own format, such as StanForD Harvesting
  as one large XML document. In this SDK, one flat record is *a telemetry*; many are
  *telemetries*.
- **Monitoring** is the practice of collecting and analyzing that information. Part of it is this
  SDK's pipeline: it flattens telemetries from a device's format into big data ready collections and
  transmits them to a big data resource.
- **Observability** is the ability to understand a system through that information. Here it is the
  AI step: a natural language intention is translated into a query against the big data resource,
  and the answer comes back in a form a human understands.

The end game is observability that opens up insights. During the POC, telemetries are not split into
kinds (as some monitoring products split events, metrics, traces and exceptions); that may come
later. The SDK's own signals, such as exceptions and metrics from the pipeline, are called
**diagnostics**, so *telemetry* and *monitoring* always mean the forestry devices.

### Big data

The purpose of the Forestry SDK for .NET is to transform StanForD information and other metrics (e.g.
grading at terminals and mills), with their relationships to other industry entities (e.g. contract
chains), into big data friendly structures. Metrics from devices, including production machines, are
historical constants (unless they fail validation) and central to aggregations. Dimensions,
relationships and partitions are usually filters, and may need corrections **before** ingestion into
big data systems.

Big data provides:

- fast aggregations and analysis
- compression, i.e. storage friendly
- vectorization, i.e. operations on batches of values, which is CPU friendly
- partition pruning, where whole folders are skipped by the path, such as month and contract
- statistics, with min/max values per row group or extent that skip blocks inside files
- queries built around metrics: sums, averages and counts over time and groups

These advantages require that dimensions and relationships are flattened together with the metrics,
without nested structures.

Device telemetries can be complex, like StanForD in XML. Ingestion into big data resources is the
first part, monitoring: flattening complex telemetries into big data ready collections. Natural
language (NL) intentions as queries against big data resources is the second part, observability:
making reporting as simple as a personal chat. This SDK covers the first part.

## Scandinavian StanForD

StanForD is primarily operative metrics or instructions for production (e.g. price and bucking
instructions). Secondarily, both are mixed with commercial references, e.g. products, and dimensions,
e.g. machine manufacturer. StanForD metrics are big data ready, if transformed correctly, because of:

- **Granularity**: every stem and every log is measured at the stump
- **Volume**: documents come daily from every machine
- **Identity**: the machine key plus running stem and log keys make each telemetry unique
- **Relationships**: each stem refers to its contract, product and species, which are the dimensions
  described under [Purpose](#purpose)

Harvesting in Scandinavia is CTL (cut-to-length): a harvester processes a stem into separate logs at
the stump, and StanForD can record both tree-level and log-level measurements. The picture outside
Scandinavia is mixed: some countries have documented operational use, while others have machines that
can support StanForD but little evidence of broad use.

| Region | Usage |
| :--- | :--- |
| Sweden | Skogforsk reported in a 2020 article that about 80% of Swedish harvesters used StanForD 2010. A later Skogforsk article says "the vast majority" of Swedish harvesters report production in StanForD 2010, without a newer percentage. |
| Norway | No reliable, published national percentage could be found. |
| Finland | Strong adoption. A research paper reports that approximately 90% of operational harvesters use StanForD. Finland was also involved in developing StanForD 2010 through Metsäteho. |
| Central Europe | Germany had StanForD 2010 machines in operation by 2015, while a recent research paper says Classic remained more widespread in Germany and that multiple 2010 versions are in use. Bavaria's guidance describes using 2010-compatible data exchange for harvester instructions and reports. In Latvian State Forests about 52% supported StanForD 2010, but most of those weren't using it or didn't have it installed. Mostly at the research level in Portugal, Italy, Ireland and Poland. |
| North America | StanForD is used in at least some operations, but evidence points to uneven, regional adoption rather than universal use. A Quebec study reports companies using harvester production files, including `.hpr` (Harvesting, 2010) and `.pri` (Harvesting, Classic), for weekly volume accounting; an eastern Canada study analyzed StanForD 2010 `.hpr` data. CTL harvesting is established in parts of eastern Canada and the northeastern and Lake States regions, while other regions and systems differ. |
| South America | StanForD 2010 is described and used as a machine-data standard in Portuguese-language forestry material, but not enough to establish how widespread it is in South America. Manufacturer presence or machine compatibility alone would not prove operational adoption. |

Globally, the forest industry may have no other standard that captures metrics with relationships and
dimensions like StanForD does in Scandinavia. That doesn't mean other regions lack big data needs, only
that this SDK starts with StanForD.

## Big data shape

A flat columnar layout without nested structures is the shape for broad data-lake compatibility. The
dominant format is [Apache Parquet](https://parquet.apache.org/), supported by these big data resources:

| Resource | How it uses Parquet |
| :--- | :--- |
| Azure Synapse Analytics – serverless SQL pool | Queries Parquet files in a data lake directly with T-SQL |
| Microsoft Fabric – Lakehouse / SQL analytics endpoint | Uses Delta Lake tables, which store data in Parquet files |
| Azure Data Explorer (ADX) | Queries Parquet in Azure Storage as external tables with Kusto |
| Databricks | Reads and writes Parquet directly; Delta Lake tables are Parquet-based |
| Amazon Athena | Serverless SQL queries over Parquet in S3 |
| Amazon Redshift Spectrum | Queries external Parquet data in S3 |
| Google BigQuery | Can load Parquet or query it through external tables, including in Cloud Storage |
| Trino / Presto | Distributed SQL engines commonly used to query Parquet in object storage |
| Apache Spark | A common batch and streaming engine for Parquet data |

The list is not exhaustive, but shows a good number of resources that support Parquet.

Telemetries written by this SDK follow these conventions:

- **Values**: primitives and simple structs (`Guid`, `DateTimeOffset`) only
- **Dimensions copied, not joined**: contract, species and product are copied onto every Stem and Log
  telemetry, so queries never join. Columnar compression keeps the repetition cheap
- **Partitions**: telemetries are partitioned by month and contract, e.g. `/{year}/{month}/harvesting/{contract}/`,
  which enables partition pruning. The keys come from each telemetry, not from the document, so one document
  can land in several partitions, and partition paths and file names must allow their composite keys
  (date, contract, etc.) to vary
- **One telemetry type per file**: Stem and Log telemetries go to separate files, not mixed
- **Chunks, not documents**: files are written from chunks of telemetries, as described under
  [Big data clients](#big-data-clients)

StanForD metrics as Parquet is the long-term goal. During the POC, [JSON Lines](https://jsonlines.org/)
speeds up short-term development. Several big data resources support JSON Lines as external tables,
and it suits a POC because:

- it is one flat JSON object per line, with no header or footer
- built-in .NET writes it forward to a stream (`System.Text.Json`) and compresses it as a stream
  (`GZipStream` in `System.IO.Compression`)

The tradeoff is slower queries and bigger files.

## Big data clients

The separate libraries in this SDK come together in big data **clients**. A client communicates with
one resource, local or remote, and carries that resource's own options: location, credentials,
security. Clients are named after their resource, e.g. `Forestry.StanForD.Azure.BlobStorage`. During
the POC there is a single client, which turns local StanForD documents into local files of JSON Lines.

Whatever the resource, every StanForD client does the same work:

- **Input.** The client takes in what to ingest: usually a single document, but possibly several, as
  its configuration decides.
- **Deserialization, per telemetry.** The client hands each document to `Forestry.Serialization`, which reads
  it through a format package (`Forestry.Serialization.Xml`, built on `Forestry.Xml`) while policies
  flatten it into self-contained telemetries: identification, partitioning, conditional mapping, dimensions.
  Deserialization is mandatory: its telemetries are what feed the pipeline.
- **Pipeline, per chunk.** A chain of directives that the telemetries pass through on their way to the
  resource. Telemetries are gathered into chunks, one telemetry type and one partition per chunk, and written in
  the client's format (JSON Lines, later Parquet). Directives handle what concerns a chunk or the whole
  input: validation, Biometria extras, partition folders and file names, sending, retries, diagnostics.

There is no one-to-one mapping from a document to a file. Stem and Log telemetries go to separate chunks,
and a document is not limited to one contract, so one document can produce chunks in several
partitions. A chunk ends on size, where compaction matters for the format, as with Parquet. Whether
the end of the input flushes a final chunk, and when chunks are sent, is the client's configuration,
chosen for its resource: for Azure Data Explorer, for example, appending and compacting locally before
sending is easier than appending to Parquet already in blob storage. Replaying documents that were
already ingested needs a feature of its own, after the POC.

Deserialization and the pipeline are kept apart on purpose. Policies run for every value and must
stay fast, with no allocation. Directives run once per chunk or input and may wait, retry or call out.

Directives and policies are registered through the client's options. Each library plugs itself in with
defaults that can be adjusted:

- `Forestry.StanForD.Harvesting` adds Harvesting policies and directives.
- `Forestry.Biometria` builds on it with its own Harvesting: Stem and Log telemetries extended with
  Biometria's members, and its own policies and directives, such as Extensions from Sender.

So every StanForD client shares common options (deserialization policies, format, common directives)
and adds its resource's options, including how it takes in documents and how it chunks, flushes and
sends telemetries.

Querying the files happens outside this SDK.

## Where the POC stands

- **Done**: the forward-only UTF-8 XML reader (`Forestry.Xml`, 1.0.0-beta.1); reading with a
  deserialization context (`Forestry.Serialization` spec 002); StanForD Harvesting documents as flat Stem
  and Log telemetries (`Forestry.StanForD` spec 001).
- **Next**: writing telemetries in `Forestry.Serialization`, JSON Lines first; the single local client that turns
  StanForD documents into JSON Lines files in partition folders.
- **Later**: `Forestry.StanForD.Harvesting` split out;
  `Forestry.Biometria` with Extensions from Sender; clients for remote resources such as `Forestry.StanForD.Azure.BlobStorage`; a
  UTF-8 Parquet writer (`Forestry.Parquet`); performance testing against real documents.

## Packages

In the order of the path from a StanForD document to big data files:

| Package | Path | Purpose | Status |
|---|---|---|---|
| [Forestry.Xml](sdk/xml/Forestry.Xml/README.md) | `sdk/xml` | Forward-only UTF-8 XML reader over byte sequences, with no allocation per value. | 1.0.0-beta.1 on GitHub releases |
| [Forestry.Serialization](sdk/serialization/Forestry.Serialization/README.md) | `sdk/serialization` | Flattens large documents into telemetries through type definitions and policies, without loading them fully into memory. | In development |
| [Forestry.Serialization.Xml](sdk/serialization/Forestry.Serialization.Xml/README.md) | `sdk/serialization` | XML format for Forestry.Serialization, built on Forestry.Xml. | In development |
| [Forestry.StanForD](sdk/stanford/Forestry.StanForD/README.md) | `sdk/stanford` | [StanForD](https://www.skogforsk.se/) Harvesting documents as Stem and Log telemetries. | In development, spec 001 done |
| [Forestry.StanForD.Harvesting](sdk/stanford/Forestry.StanForD.Harvesting/README.md) | `sdk/stanford` | Harvesting-specific logic: deserialization policies and pipeline directives. | Planned |
| [Forestry.Biometria](sdk/biometria/README.md) | `sdk/biometria` | [Biometria](https://www.biometria.se/)'s Harvesting on top: Stem and Log telemetries extended with Sender's Extension, the production planner above all. | Planned |
| [Forestry.Core](sdk/core/Forestry.Core/README.md) | `sdk/core` | The core for clients: client options and the pipeline that carries an intention through directives to an answer. Formerly Forestry.Turn. | In development |
| [Forestry.Parquet](sdk/parquet/Forestry.Parquet/README.md) | `sdk/parquet` | Forward-only UTF-8 Parquet writer for flat telemetries. | Planned, later |

Outside that path:

| Package | Path | Purpose | Status |
|---|---|---|---|
| [Forestry.Raindrop](sdk/raindrop/Forestry.Raindrop/README.md) | `sdk/raindrop` | Dependency-free unique identity generation, inspired by Twitter Snowflake. | Stable |
| [Forestry.PapiNet](sdk/papinet/README.md) | `sdk/papinet` | Helper types for [PapiNet](https://www.papinet.org) business agreement documents. | Early scaffolding |
## Building

Every package is its own MSBuild SDK-style project with a matching `.slnx`. Shared build,
packaging, and versioning behavior is centralized under [`eng/`](eng) and pulled in via
`Directory.Build.props`; [eng/README.md](eng/README.md) maps which layer (package, root, `eng/`,
`.github/`) owns each CI/CD purpose. See [doc/README.md](doc/README.md) and [doc/dev/Building.md](doc/dev/Building.md)
for how properties, artifacts, and package versions are wired together,
[doc/dev/Pipelines.md](doc/dev/Pipelines.md) for CI, and
[doc/dev/Troubleshooting.md](doc/dev/Troubleshooting.md) for local environment issues that aren't
about the SDK's own code.

```bash
dotnet build sdk/<package>/<Project>/<Project>.slnx
dotnet test sdk/<package>/<Project>/<Project>.slnx
```

## Contributing

This SDK is under active, early development — expect breaking changes between beta releases.
Each package keeps its own `CHANGELOG.md`; update it alongside any change to public API surface.

## License

MIT licensed — see [LICENSE](LICENSE). Some packages adapt design patterns and, in places,
specific code from Microsoft's open-source .NET runtime; see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for attribution.
