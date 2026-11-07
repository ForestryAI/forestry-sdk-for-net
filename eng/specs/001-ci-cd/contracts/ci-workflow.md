# Contract: `.github/workflows/ci.yml`

**Triggers**: `pull_request` (branches: `main`), `push` (branches: `main`).
**Permissions**: `contents: read` for the whole workflow (S0).
**Runner**: `ubuntu-latest` (R6.1). **Concurrency**: one run per ref, newer cancels older.
**Matrix** (R7, P1): one entry per package in scope, `<slnx>` = the package's own solution:

| Package | `<slnx>` |
|---|---|
| `Forestry.Xml` | `sdk/xml/Forestry.Xml/Forestry.Xml.slnx` |
| `Forestry.Deserialize` | `sdk/deserialize/Forestry.Deserialize/Forestry.Deserialize.slnx` |
| `Forestry.Deserialize.Xml` | `sdk/deserialize/Forestry.Deserialize.Xml/Forestry.Deserialize.Xml.slnx` |

Each job below runs once per matrix entry (`fail-fast: false`, so one package's failure does not
hide another's result).

Every step is a command a developer runs locally from the repository root (C3); the "Local command"
column is what `doc/dev/Pipelines.md` lists.

## Job `build-test`

| Step | Local command | Fails when |
|---|---|---|
| Checkout | `git clone` | – |
| Setup .NET from `global.json` | (installed SDK; `dotnet --version` matches `global.json`) | SDK missing |
| Restore (NuGet cache keyed on `eng/packaging/*.props` and `**/*.csproj`) | `dotnet restore <slnx>` | Known vulnerable package (S1) |
| Build | `dotnet build <slnx> -c Release --no-restore -p:ContinuousIntegrationBuild=true` | Any warning (warnings as errors), a missing XML doc |
| Test with coverage | `dotnet test <slnx> -c Release --no-build --collect "XPlat Code Coverage" --logger trx` | A failing test; the result names test and framework |
| Upload test results and coverage | – | – |

## Job `pack` (needs `build-test`)

| Step | Local command | Fails when |
|---|---|---|
| Pack with CI suffix | `dotnet pack <slnx> -c Release -p:ContinuousIntegrationBuild=true -p:VersionSuffixCi=<run>` | Packaging gate (contract `packaging-gate.md`) |
| Package validation against last release (once one exists, P5) | `dotnet pack … -p:PackageValidationBaselinePath=<downloaded .nupkg>` | A breaking API change |
| Upload packages | – | – |

## Job `performance` (independent of `build-test`)

| Step | Local command | Fails when |
|---|---|---|
| Performance gate for the package's test project | `dotnet test <test project> -c Release -s <test folder>/Performance.runsettings` | An allocation or memory-growth gate fails (no timing gates, R6.7) |

## Not in this workflow

Publishing of any kind (C0) — see `release-workflow.md`.
