# forestry-sdk-for-net

[![ci](https://github.com/ForestryAI/forestry-sdk-for-net/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ForestryAI/forestry-sdk-for-net/actions/workflows/ci.yml)

Forestry SDK for .NET is the public client toolkit for working with Forestry AI — voice and
text interactions that turn day-to-day forestry operations (harvesting, forwarding, delivery)
into structured data and answers. This repository hosts the individual client libraries; each
one ships and versions independently.

## Packages

| Package | Path | Purpose | Status |
|---|---|---|---|
| [Forestry.Raindrop](sdk/raindrop/Forestry.Raindrop/README.md) | `sdk/raindrop` | Dependency-free unique identity generation, inspired by Twitter Snowflake. | Stable |
| [Forestry.Xml](sdk/xml/Forestry.Xml/README.md) | `sdk/xml` | Forward-only UTF-8 XML reader over byte sequences, with no allocation per value. | 1.0.0-beta.1 on GitHub releases |
| [Forestry.Deserialize](sdk/deserialize/Forestry.Deserialize/README.md) | `sdk/deserialize` | Shreds large documents into flat rows through type definitions and policies, without loading them fully into memory. | In development |
| [Forestry.Deserialize.Xml](sdk/deserialize/Forestry.Deserialize.Xml/README.md) | `sdk/deserialize` | XML media for Forestry.Deserialize, built on Forestry.Xml. | In development |
| [Forestry.StanForD](sdk/stanford/Forestry.StanForD/README.md) | `sdk/stanford` | [StanForD](https://www.skogforsk.se/) Harvesting documents as Stem and Log rows. | In development, spec 001 done |
| [Forestry.StanForD.Harvesting](sdk/stanford/Forestry.StanForD.Harvesting/README.md) | `sdk/stanford` | Harvesting-specific logic: deserializer policies and Core direction and answer policies. | Planned |
| [Forestry.Parquet](sdk/parquet/Forestry.Parquet/README.md) | `sdk/parquet` | Forward-only Parquet writer for flat rows. | Planned, later |
| [Forestry.Turn](sdk/turn/Forestry.Turn/README.md) | `sdk/turn` | Pipeline that carries an intention through directives to an answer. To be renamed Forestry.Core. | In development |
| [Forestry.PapiNet](sdk/papinet/README.md) | `sdk/papinet` | Helper types for [PapiNet](https://www.papinet.org) business agreement documents. | Early scaffolding |
| [Biometria](sdk/biometria/README.md) | `sdk/biometria` | Helper for publishing/subscribing to [Biometria](https://www.biometria.se/) entities. | Not started |

## Direction

Raindrop has no dependency on the others. The rest form a path from documents to answers:

1. **Read**: Forestry.Xml reads documents, and Forestry.Deserialize with its media packages shreds
   them into flat rows. Forestry.StanForD defines those rows for StanForD Harvesting.
2. **Write**: next, Forestry.Deserialize gets writing functions, from a row to a similar flat format,
   and a Forestry.Deserialize.Parquet media package writes rows as Parquet. That speeds up the POC; a
   writer of its own in Forestry.Parquet comes later.
3. **Hand over**: rows written as Parquet go to Azure Storage, partitioned so Azure Data Explorer can
   query them as external tables. Forestry.Turn, soon renamed Forestry.Core, becomes the core for
   clients that talk with third-party cloud resources or local resources: an intention goes through
   direction policies, from one format to another, and the answer comes back through matching answer
   policies.
   Forestry.StanForD.Harvesting plugs Harvesting policies into both the deserialization and the
   Core pipeline.

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
