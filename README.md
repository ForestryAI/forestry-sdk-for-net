# forestry-sdk-for-net

Forestry SDK for .NET is the public client toolkit for working with Forestry AI — voice and
text interactions that turn day-to-day forestry operations (harvesting, forwarding, delivery)
into structured data and answers. This repository hosts the individual client libraries; each
one ships and versions independently.

## Packages

| Package | Path | Purpose | Status |
|---|---|---|---|
| [Forestry.Raindrop](sdk/raindrop/Forestry.Raindrop/README.md) | `sdk/raindrop` | Dependency-free unique identity generation, inspired by Twitter Snowflake. | Stable |
| [Forestry.Turn](sdk/turn/Forestry.Turn/README.md) | `sdk/turn` | Turn-taking pipeline that carries an intention through retry/transform directives to a resolved answer. | In development |
| [Forestry.Deserialize](sdk/deserialize/Forestry.Deserialize/README.md) | `sdk/deserialize` | Streams large documents (starting with XML, e.g. StanForD harvester/forwarder files) without loading them fully into memory. | In development, architecture under revision |
| [Forestry.PapiNet](sdk/papinet/README.md) | `sdk/papinet` | Helper types for [PapiNet](https://www.papinet.org) business agreement documents. | Early scaffolding |
| Biometria | `sdk/biometria` | Helper for publishing/subscribing to [Biometria](https://www.biometria.se/) entities. | Not started |

Raindrop has no dependency on the others. Turn and Deserialize are the two active areas of work:
Deserialize turns a raw document into values, Turn carries an intention through a pipeline to an
answer — together they form the read → decide path that the rest of the SDK builds on.

## Building

Every package is its own MSBuild SDK-style project with a matching `.slnx`. Shared build,
packaging, and versioning behavior is centralized under [`eng/`](eng) and pulled in via
`Directory.Build.props`. See [doc/README.md](doc/README.md) and [doc/dev/Building.md](doc/dev/Building.md)
for how properties, artifacts, and package versions are wired together, and
[doc/dev/Pipelines.md](doc/dev/Pipelines.md) for CI.

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
