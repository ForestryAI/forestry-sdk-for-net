# Research: CI/CD for the SDK packages

**Spec**: [spec.md](spec.md) | **Issue**: #71 | **Date**: 2026-11-07

Every decision below is a **proposal until the developer confirms it** (C5). Each lists options and
a recommendation; its Status says Open or Confirmed (with date).

## R0. Inventory of what exists (not a design basis)

`eng/` and `doc/dev/` come from the SDK's time on Azure DevOps and were never run on GitHub; this
spec shifts the engineering from Azure DevOps to GitHub. Each piece below is kept, reshaped or removed; the proposal is in the last column.
**Status**: Confirmed by the developer, 2026-11-07 (every row as proposed; the `eng/README.md` row
added and confirmed during the developer's review).

| Piece | What it does today | Proposal |
|---|---|---|
| `Directory.Build.props` (root) | Repo paths; Debug/AnyCPU defaults; all output under `artifacts/`; imports `eng/Directory.Build.Common.props` for `Forestry.*` projects; points CPM at `eng/packaging/Directory.Packages.props` | **Keep**, reshape: add deterministic CI builds, symbols (`.snupkg`), package metadata (repository URL, README, license), XML docs for packable projects |
| `Directory.Build.targets` (root) | Stamps test assemblies with their source path; imports `eng/Directory.Build.Common.targets` | **Keep** |
| `eng/Directory.Build.Common.props` | Client/test kinds; `LangVersion` latest; `TreatWarningsAsErrors` true; metadata (`Company` Forestry, `Authors` "Coders", MIT); net10.0 + net9.0; `TurnSharedSources` | **Reshape**: real `Authors`, drop the TODOs, move package metadata here; frameworks per R8 (drop net9.0); keep warnings as errors |
| `eng/Directory.Build.Common.targets` | Imports the version targets; guard FSDK00001 (CPM may not be disabled) | **Keep** the guard; drop the import if R2 removes the version targets |
| `eng/Directory.Build.Version.targets` | Date-based `AssemblyVersion` from an Azure DevOps `BUILD_IDENTIFICATION`; has a bug (`$(OfficialBuildId[8])`, a property that does not exist); `doc/dev/Building.md` says it rewrites prereleases to `alpha.yyyyMMdd.r`, which it does not | **Remove**; versions follow R2 |
| `eng/packaging/Directory.Packages.props` + `Directory.Support.Packages.props` | Central package versions: `coverlet.collector` 3.2.0, `Microsoft.NET.Test.Sdk` 17.6.0, `xunit` 2.4.2, `xunit.runner.visualstudio` 2.4.5, `Microsoft.ServiceFabric` (Raindrop tests), `Microsoft.Extensions.*` 10.0.7, `System.Memory.Data` 10.0.0 | **Keep**; versions updated by Dependabot pull requests (S1), the stale test packages first |
| `eng/packaging/README.md` | Explains CPM and FSDK00001 | **Keep** |
| `eng/pipelines/**` (7 YAML files) | Azure Pipelines templates; mostly empty (`stages/library.yml` empty, `jobs/flow.yml` malformed, `steps/install-dotnet.yml` references undeclared parameters); a self-hosted pool `Forestry-sdk-pool` | **Remove**; replaced by `.github/workflows/` (R1) |
| `eng/scripts/**` (PowerShell modules, Pester tests) | `Logging` (`Write-Trace` for Azure Pipelines / GitHub Actions) and `Package` (empty `Get-Version.ps1`, empty class); `Set-Environment.ps1` imports both | **Remove**; nothing calls them, and C3 prefers plain `dotnet` commands. A script returns only if a step needs one |
| `global.json` | SDK 10.0.201, `rollForward: feature`; `msbuild-sdks` `Microsoft.Build.Traversal` 4.1.82 (no traversal project exists) | **Keep**, reshape: `rollForward: latestPatch` (C4: same feature band locally and in CI); drop `Microsoft.Build.Traversal` unless R7 picks a traversal project; add the support dates of the targeted .NET versions (R8) |
| `NuGet.Config` | Only nuget.org, sources cleared | **Keep**; consider package source mapping (S1) |
| `package.json` (root) | `name`, `version` 1.0.0, nothing else | **Remove** (unused leftover) |
| Every `.csproj` | `<Version>1.0.0-beta.1</Version>` (Raindrop `beta.2`) and `<ComptableVersion>1.0.0</ComptableVersion>` (misspelled, read by nothing) | **Remove** `ComptableVersion` everywhere; version per R2/R3 for the three packages in scope |
| `CHANGELOG.md` | `Forestry.Xml` has one (Azure SDK style: `## 1.0.0-beta.1 (Unreleased)`), as do Raindrop and Turn; `Forestry.Deserialize` and `Forestry.Deserialize.Xml` have none | **Add** for the two missing packages, same style; the release check reads the `## <version> (<date>)` heading |
| `doc/dev/Pipelines.md` | A title only, no content | **Rewrite** (D0) |
| `doc/dev/Building.md` | MSBuild layout (useful); an Azure Pipelines section; the version targets | **Reshape** (D0) |
| Per-package `Directory.Build.props`, `*.slnx`, `*.runsettings`, `Performance.runsettings` | Each package imports the root props; tests exclude `Category=Performance` by default | **Keep** |
| `eng/README.md` | Missing: nothing says which layer (package, root, `eng/`, `.github/`) owns which CI/CD purpose | **Add** a purpose map: one row per purpose, the file that owns it in each layer. Rule: a package states only what is unique to it, the root and `eng/` hold the defaults, `.github/` only runs commands, `doc/dev/` explains how (D0); the root `README.md` links to it. Kept up to date as decisions change (developer, 2026-11-07). **Status**: Confirmed |

Facts checked on 2026-11-07: the three packages in scope build with 0 warnings and 0 errors
locally (SDK 10.0.203); `Forestry.PapiNet` has 8 errors (out of scope, FR-015). The repository is
public. `Forestry.Deserialize.Xml` references `Forestry.Xml` and `Forestry.Deserialize` as projects,
so its package depends on theirs (spec Story 3, scenario 4).

## R1. CI host

- **Options**: (a) GitHub Actions; (b) Azure Pipelines, as the copied `eng/pipelines/` assumes.
- **Recommendation**: (a). The repository, issues and board are on GitHub; Actions is free for
  public repositories, needs no service connection, and supports keyless publishing (S0).
- **Status**: Confirmed by the developer, 2026-11-07

## R2. Where versions come from

- **Options**: (a) by hand: `<Version>` in each package's `.csproj`, changed in the same pull
  request as its `CHANGELOG.md` entry; (b) from git tags with a tool such as MinVer (one tag prefix
  per package), a third-party build dependency.
- **Recommendation**: (a). The version and its release notes are reviewed together; no third-party
  build dependency; every build (local or CI) knows its version without git history. CI builds that
  are not releases add a suffix (e.g. `-ci.<run number>`) so their packages never collide with a
  release.
- **Status**: Confirmed by the developer, 2026-11-07

## R3. Version scheme while APIs still move

- **Options**: (a) `1.0.0-beta.N`, as the `.csproj` files and `Forestry.Xml`'s `CHANGELOG.md` have
  today; (b) `0.x.y`.
- **Recommendation**: (a). A prerelease is hidden from consumers unless they ask for prereleases,
  which suits feedback; it matches the existing CHANGELOG and the "working `1.0.0-beta`" milestone
  in G2 of all three constitutions; NuGet treats `0.x` as stable. Breaking changes between betas are
  allowed by SemVer.
- **Status**: Confirmed by the developer, 2026-11-07

## R4. Preview feed (open to anyone, spec FR-011)

- **Options**: (a) `.nupkg` and `.snupkg` attached to a GitHub release per package version; a
  reviewer downloads them and adds the folder as a source; (b) a public third-party feed (e.g.
  feedz.io) with anonymous install; (c) nuget.org prerelease versions; (d) GitHub Packages.
- **Recommendation**: (a). Anonymous, no account or service to add, publish rights are the
  workflow's own token (S0), and release notes sit next to the files. (b) is the upgrade if adding a
  folder source proves clumsy for reviewers. (c) is what the later nuget.org spec is for; (d) needs
  a GitHub sign-in and a token even to install a public package, ruled out by the developer's answer.
- **Status**: Confirmed by the developer, 2026-11-07

## R5. What runs when

- **Proposal**:
  - **Pull request to `main`**: restore, compile (warnings as errors), test on every supported target framework (R8),
    pack with a `-ci.N` suffix, packaging gate, coverage report; performance gate as its own job.
    Fork pull requests run with read-only permissions and no secrets.
  - **Push to `main`**: the same; packages kept as run artifacts.
  - **Release**: started by hand for one package from `main`; checks the `CHANGELOG.md` entry and
    that the version is new, runs the gate, packs without suffix, creates the tag
    `<Package>/v<version>` (e.g. `Forestry.Xml/v1.0.0-beta.1`) and the GitHub release with the
    entry as notes and the packages attached.
  - **Weekly**: Dependabot pull requests for NuGet packages and the workflow's actions.
- **Status**: Confirmed by the developer, 2026-11-07

## R6. Further engineering choices (proposed together)

| # | Choice | Proposal |
|---|---|---|
| R6.1 | Runner | `ubuntu-latest`, fastest on shared runners; Windows only if a test needs it |
| R6.2 | Setup | SDK from `global.json`; a further runtime only when a second framework is supported (R8) |
| R6.3 | Restore and caching | Cache the NuGet folder keyed on `eng/packaging/*.props` and the `.csproj` files |
| R6.4 | Warnings as errors | Already on (`TreatWarningsAsErrors`); CI adds nothing |
| R6.5 | Test matrix | Each test project runs both target frameworks in one job; results uploaded per framework |
| R6.6 | Coverage | Collected with the existing `coverlet.collector`, shown in the run summary; no threshold yet |
| R6.7 | Performance gate | A separate job running `Performance.runsettings`; only allocation and memory-growth gates, no timings (shared runners are noisy) |
| R6.8 | Determinism, symbols, SourceLink | `ContinuousIntegrationBuild` in CI, `.snupkg` symbols, the SDK's built-in SourceLink |
| R6.9 | Breaking changes | The SDK's package validation against the package's last release (on once a release exists) |
| R6.10 | Release notes | The version's `CHANGELOG.md` section, copied into the GitHub release |
| R6.11 | Vulnerable packages | NuGet audit on all dependencies; a warning fails the build through warnings as errors (S1) |
| R6.12 | Actions pinned | Every third-party action pinned to a commit SHA; Dependabot updates them (S1) |
| R6.13 | Permissions | `contents: read` by default; `contents: write` only in the release job (S0) |

**Dependabot notes** (R5's weekly updates, R6.11, R6.12; added during the developer's review,
2026-11-07):

1. **Central package files outside the default place.** Versions live in
   `eng/packaging/Directory.Packages.props` and the imported `Directory.Support.Packages.props` (a
   non-standard name, with a test-only conditional `ItemGroup`). `dependabot.yml` lists
   `/eng/packaging` explicitly, and US1 checks that the first Dependabot run updates both files; if
   it does not, the support file is folded into `Directory.Packages.props`.
2. **Pull request noise.** Updates are grouped (test packages; GitHub Actions), weekly, with a cap on
   open pull requests.
3. **Major versions.** The first wave includes breaking jumps of stale test packages (e.g.
   `coverlet.collector` 3 → 6, `Microsoft.NET.Test.Sdk` 17.6 → newer); with warnings as errors some
   need hand fixes. xunit v3 is a different package (`xunit.v3`) and stays a deliberate choice, not
   a Dependabot update.
4. **Library dependencies are minimums.** A published package's dependency version is the minimum
   its consumers get, so libraries do not chase the latest version. The three packages in scope have
   no package dependencies (A1); when a package gains one, major updates beyond the supported
   frameworks' line (e.g. `Microsoft.Extensions.*` 11.x while net10.0 is the LTS) are ignored.

Security alerts and security updates are separate repository settings (Settings → Security), switched
on by the developer; the .NET SDK in `global.json` is updated by deliberate change (R8), not by
Dependabot.

- **Status**: Confirmed by the developer, 2026-11-07

## R7. How CI finds the packages in scope

- **Options**: (a) one solution file listing the packages in scope (e.g. `eng/Forestry.Ci.slnx`):
  adding a package to CI is one line (FR-015); (b) a traversal project (`Microsoft.Build.Traversal`,
  already named in `global.json`); (c) a list of the packages' own `.slnx` in the workflow.
- **Recommendation** (first): (a). One local command builds and tests exactly what CI does (C3); no
  extra MSBuild SDK.
- **Revised in the plan review** (developer, 2026-11-07): (c). The packages are building blocks:
  some are integrated (`Forestry.Xml` → `Forestry.Deserialize.Xml`, later StanForD), others stand
  alone (Raindrop, Turn, Biometria later). One solution would tie them together: a failure in one
  would turn every pull request red, against C1 and the purpose of `eng/` (`eng/README.md`).
  Instead `ci.yml` runs a matrix with one job per package, each on the package's own `.slnx`, the
  same command the developer runs locally (C3). Adding a package is one matrix line (FR-015). An
  integrated package's job builds the packages it references from source, so a reader change is
  tested by the reader's tests and through the bridge's tests in the same pull request. Every
  package runs on every pull request for now; running only changed packages (and their
  dependents) is deferred until the number of packages makes it worth a dependency map.
- **Status**: Confirmed by the developer (revised to (c)), 2026-11-07

## R8. Target frameworks

- **Facts** (Microsoft's .NET support policy, checked 2026-11-07): net9.0 (STS) and net8.0 (LTS) end
  support on 2026-11-10; net10.0 (LTS, released 2025-11-11) is supported to 2028-11-14; net11.0
  (STS, 24 months of support) is expected in November 2026, its end date not yet published.
- **Options**: (a) keep net10.0 + net9.0; (b) net10.0 only, net11.0 added once released; (c)
  net10.0 + net11.0 from the start (preview).
- **Decision**: (b). Policy: the current LTS, plus the newest release when it is newer; a version out
  of support is dropped. No code in the three packages is framework-conditional, so dropping net9.0
  is one line in `eng/Directory.Build.Common.props`. `global.json` carries the support dates
  (net11.0's release, net10.0's end of support) next to the pinned SDK, in its `_remarks` style.
  The package silos' A0 ("Targets net9 and net10") is amended in each silo, a follow-up outside
  this spec.
- **Status**: Confirmed by the developer, 2026-11-07

