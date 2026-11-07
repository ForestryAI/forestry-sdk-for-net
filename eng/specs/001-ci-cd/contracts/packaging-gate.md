# Contract: packaging gate (constitution C2)

What a package in scope passes before `release.yml` publishes it. Every item is checked by the
pipeline on every pull request (`ci.yml`), so a release finds no surprises.

| Item | Checked by | Today (2026-11-07) |
|---|---|---|
| XML doc comment on every public member | `GenerateDocumentationFile=true` for packable projects; CS1591 fails through warnings as errors | `Forestry.Xml`, `Forestry.Deserialize` on; `Forestry.Deserialize.Xml` off but 0 missing docs when switched on |
| Each public option tested for its default, an override and the values it rejects | Tests in the package's test project, named per option; listed below and reviewed in US2 | Partly: options are used in existing tests, not yet one test set per option |
| Tests pass on every supported target framework | `ci.yml` `build-test` | net10.0 (R8) |
| Performance gate passes | `ci.yml` `performance` | `Forestry.Xml` (allocations), `Forestry.Deserialize.Xml` (memory); `Forestry.Deserialize` has no performance test (P9) |
| Symbols and SourceLink | `.snupkg` produced; SourceLink from the SDK | Not set yet (US2) |
| Package metadata | `PackageId`, `Version`, `Description`, `Authors`, license, repository URL, README, release notes | Partly (`Description`, license) |
| No breaking change against the last release, unless intended | SDK package validation with the last release as baseline (P5) | No release yet |

## Public options in scope

| Package | Option | Default | Override | Rejected |
|---|---|---|---|---|
| `Forestry.Xml` | `ReaderOptions.MaxDepth` | to be read from code in US2 | a different valid value | values the setter throws on |
| `Forestry.Xml` | `ReaderOptions.MaxNameLength` | 128 bytes (constitution S1) | a different valid value | values the setter throws on |
| `Forestry.Deserialize` | `DeserializeOptions.Rows`, `.Converters`, `.Policies`, `MakeReadOnly()` / `IsReadOnly` | read from code in US2 | added converter / policy | changes after `MakeReadOnly()` |
| `Forestry.Deserialize` | `DefinitionPolicy`, `EnumerationPolicy` | read from code in US2 | each documented setting | invalid combinations |
| `Forestry.Deserialize.Xml` | `DeserializeXmlOptions` (members beyond `DeserializeOptions`) | read from code in US2 | – | – |

The table is completed from the code in US2's test-shell task and reviewed by the developer before
the tests are written (package silos' C4).
