# Contract: `.github/workflows/ci.yml` and its phases

**Files** (P10): `ci.yml` orchestrates; each phase is a reusable workflow (`on: workflow_call`) in
`.github/workflows/`, called once per matrix entry with inputs `package`, `slnx`, `test-project`
and `test-folder`; every phase job starts with `actions/checkout` and then the composite action
`./.github/actions/setup` (setup-dotnet from `global.json`, NuGet cache).

| File | Kind | Story |
|---|---|---|
| `ci.yml` | Triggers, permissions, concurrency, matrix; one calling job per phase | US1 |
| `build-test.yml` | Phase: restore, build, test, coverage, summary | US1 |
| `performance.yml` | Phase: performance gate | US1 |
| `pack.yml` | Phase: pack, package validation, upload | US2 |
| `.github/actions/setup/action.yml` | Composite action: setup-dotnet, NuGet cache | US1 |

**Triggers**: `pull_request` (branches: `main`), `push` (branches: `main`).
**Permissions**: `contents: read` for the whole workflow (S0).
**Runner**: `ubuntu-latest` (R6.1). **Concurrency**: one run per ref, newer cancels older.
**Matrix** (R7, P1): one entry per package in scope, `<slnx>` = the package's own solution:

| Package | `<slnx>` |
|---|---|
| `Forestry.Xml` | `sdk/xml/Forestry.Xml/Forestry.Xml.slnx` |
| `Forestry.Deserialize` | `sdk/deserialize/Forestry.Deserialize/Forestry.Deserialize.slnx` |
| `Forestry.Deserialize.Xml` | `sdk/deserialize/Forestry.Deserialize.Xml/Forestry.Deserialize.Xml.slnx` |
| `Forestry.Raindrop` (`build-test` only) | `sdk/raindrop/Forestry.Raindrop/Forestry.Raindrop.slnx` |
| `Forestry.Turn` (`build-test` only) | `sdk/turn/Forestry.Turn/Forestry.Turn.slnx` |

Each job below runs once per matrix entry (`fail-fast: false`, so one package's failure does not
hide another's result).

Every step is a command a developer runs locally from the repository root (C3); the "Local command"
column is what `doc/dev/Pipelines.md` lists.

## Phase `build-test.yml` (called as job `build-test`)

Builds and tests in the Debug configuration, as a developer's everyday `dotnet test` does, so
`Debug.Assert` invariants are checked too (P11); `pack.yml` and `release.yml` build Release.

| Step | Local command | Fails when |
|---|---|---|
| Checkout | `git clone` | – |
| `./.github/actions/setup`: .NET from `global.json`, NuGet cache keyed on `eng/packaging/*.props` and `**/*.csproj` | (installed SDK; `dotnet --version` matches `global.json`) | SDK missing |
| Restore | `dotnet restore <slnx>` | Known vulnerable package (S1) |
| Build | `dotnet build <slnx> --no-restore -p:ContinuousIntegrationBuild=true` | Any warning (warnings as errors), a missing XML doc |
| Test with coverage | `dotnet test <slnx> --no-build --collect "XPlat Code Coverage" --logger trx` | A failing test; the result names test and framework |
| Upload test results and coverage | – | – |

## Phase `pack.yml` (called as job `pack`, for the packages released; independent of `build-test`)

Built apart so that one package's failure never hides another's package (R7).

| Step | Local command | Fails when |
|---|---|---|
| Pack with CI suffix | `dotnet pack <slnx> -c Release -p:ContinuousIntegrationBuild=true -p:VersionSuffixCi=<run>` | Packaging gate (contract `packaging-gate.md`) |
| Package validation against last release (once one exists, P5) | `dotnet pack … -p:PackageValidationBaselinePath=<downloaded .nupkg>` | A breaking API change |
| Upload packages | – | – |

## Phase `performance.yml` (called as job `performance`, independent of `build-test`)

| Step | Local command | Fails when |
|---|---|---|
| Performance gate for the package's test project | `dotnet test <test project> -s <test folder>/Performance.runsettings` | An allocation or memory-growth gate fails (no timing gates, R6.7) |

## Not in this workflow

Publishing of any kind (C0) — see `release-workflow.md`.
