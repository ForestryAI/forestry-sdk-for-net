# Forestry SDK for .NET - Pipelines

CI runs on GitHub Actions. Every step is a plain `dotnet` command you can run on your own machine,
so any failure in CI can be reproduced locally (eng constitution C3). Which file owns which purpose
is mapped in [eng/README.md](../../eng/README.md); the decisions behind this page are in
[eng/specs/001-ci-cd](../../eng/specs/001-ci-cd/).

## What runs when

| Event | What runs |
|---|---|
| A pull request to `main` is opened or updated | `ci.yml`: build and test, the performance gate, and packing (nothing is published) |
| A push to `main` (a merged pull request) | `ci.yml`, the same |
| Started by hand (Actions → release → Run workflow) | `release.yml`: checks, packs and publishes one package |
| A pull request changing `release.yml`, `build-test.yml` or `Directory.Build.Common.targets` | `release.yml` as a dry run of `Forestry.Xml` |
| Weekly | Dependabot opens grouped pull requests for NuGet packages and GitHub Actions; each is checked by `ci.yml` like any change |

A newer push to a pull request cancels its older run. On `main` a running run finishes, but GitHub
keeps only one waiting: when pull requests are merged in quick succession the waiting run is
replaced, so some show as cancelled and the latest commit is the one that runs.

## How the workflows are built

```text
.github/
├── workflows/
│   ├── ci.yml                 decides WHEN and FOR WHICH PACKAGES; calls the phases
│   ├── build-test.yml         phase: restore, build, test, coverage, summary
│   ├── performance.yml        phase: the performance gate
│   ├── pack.yml               phase: pack, compare with the last release, keep the packages
│   └── release.yml            by hand: one package to the preview feed
└── dependabot.yml             weekly dependency updates

eng/actions/
├── setup/action.yml           setup steps every phase repeats: .NET from global.json, NuGet cache
└── baseline/action.yml        finds the package's last GitHub release, for breaking changes
```

GitHub only reads workflows and `dependabot.yml` from `.github/`. Composite actions can live anywhere,
so the shared steps sit in `eng/` and a step refers to them by path (`uses: ./eng/actions/setup`).

- **`ci.yml` is the orchestrator.** It holds the triggers, the permissions (read-only) and one
  *matrix* per phase: a list of the packages in scope. GitHub runs the phase once per package, as
  its own job, so a failure shows on that package's job only (`build-test / Forestry.Xml`).
- **A phase is a reusable workflow** (`on: workflow_call`): a whole job that another workflow calls
  with inputs, like a method. It cannot run on its own. The GitHub equivalent of an Azure DevOps job
  template. Reusable workflows must sit directly in `.github/workflows/` (no subfolders).
- **The setup action is a composite action**: a group of steps used inside a job, the equivalent of
  an Azure DevOps step template. Each phase first checks out the repository (so the action exists
  on disk), then uses it.
- **Every third-party action is pinned to a commit** (`uses: actions/checkout@<sha> # v7.0.1`), so
  nobody can change what runs by moving a tag (eng constitution S1). Dependabot proposes new
  commits as pull requests and keeps the version comment up to date.

## Each step and its local command

Run from the repository root. `<slnx>` is the package's own solution, e.g.
`sdk/xml/Forestry.Xml/Forestry.Xml.slnx`; `<test folder>` is its `test/` folder.

| Phase | Step | Local command | Fails when |
|---|---|---|---|
| setup | .NET from `global.json` | `dotnet --version` shows the version in `global.json` (same feature band, latest patch) | SDK missing |
| build-test | Restore | `dotnet restore <slnx>` | A known vulnerable package (NuGet audit, warnings are errors) |
| build-test | Build | `dotnet build <slnx> --no-restore` | Any compiler warning (warnings are errors) |
| build-test | Test with coverage | `dotnet test <slnx> --no-build --collect "XPlat Code Coverage"` | A failing test |
| performance | Performance gate | `dotnet test <test folder>/<Package>.Tests.csproj -s <test folder>/Performance.runsettings` | An allocation or memory-growth gate fails |
| pack | Pack | `dotnet pack <slnx> -c Release -p:VersionSuffixCi=0` | A missing XML doc on a public member; a breaking change against the last release |

Notes:

- **Debug, not Release**: tests run in the Debug configuration, like your everyday `dotnet test`,
  so `Debug.Assert` invariants are checked too. Packing and releasing will build Release.
- **The default test run leaves out performance tests** (trait `Category=Performance`) through each
  test project's `.runsettings`; `Performance.runsettings` runs only them. They gate allocations and
  memory growth, never timings: shared runners are noisy. A package without performance tests
  passes the gate with zero tests.
- **CI adds `-p:ContinuousIntegrationBuild=true`** to the build: it only normalises file paths in
  the output (deterministic builds); the build behaves the same.
- **Packing**: only a library that sets `IsShippingLibrary` in its own `Directory.Build.props` is
  packed and held to the packaging gate. Each gets a `.nupkg` and a `.snupkg` under
  `artifacts/packages/Release/<Package>/`, downloadable from the run (`packages-<Package>`).
- **The CI suffix**: `-p:VersionSuffixCi=<n>` turns `1.0.0-beta.1` into `1.0.0-beta.1.ci.<n>`, so a CI
  package never collides with a release. A real release packs without it.
- **Breaking changes**: once a package has a GitHub release, `pack` passes that release's `.nupkg` as
  `-p:PackageValidationBaselinePath=<file>` and the SDK compares the public API with it. Until then it
  says "nothing to compare with".
- **An intended breaking change**: list it under `### Breaking Changes` in the package's `CHANGELOG.md`,
  then run the pack with the last release's `.nupkg` as the baseline and
  `-p:ApiCompatGenerateSuppressionFile=true`. That writes `CompatibilitySuppressions.xml` next to the
  project, accepting exactly those differences; it is reviewed in the pull request. Once the version
  is released it becomes the new baseline and every later pack fails with "Unnecessary suppressions
  found", so delete the file in the first pull request after the release.
- **Reading a failure**: open the failed job; the *Summary* tab lists failed tests with their target
  framework and the line coverage. Test results and coverage files are attached to the run as
  artifacts (`test-results-<Package>`).

## Releasing a package

The preview feed is this repository's GitHub releases. To release:

1. In the package's `CHANGELOG.md`, change the heading from `## 1.0.0-beta.1 (Unreleased)` to the date
   (`## 1.0.0-beta.1 (2026-11-10)`), and merge that to `main`. For the next one, raise `<Version>` in
   its `.csproj` and add a new heading.
2. Actions → **release** → Run workflow: choose the package, leave **dry-run** on. It runs the checks
   and packs, publishing nothing; its packages and notes are attached to the run.
3. Run it again with **dry-run** off. It creates the tag `<Package>/v<Version>` and a prerelease GitHub
   release with the `.nupkg`, the `.snupkg` and the changelog section as notes.

Locally, `dotnet pack <src project> -c Release -p:Release=true` applies the same changelog check.

It refuses: any branch but `main`; a version without a dated heading (FSDK00002, FSDK00003); a version
already released; a dependency without a release, so `Forestry.Xml` and `Forestry.Deserialize` are
released before `Forestry.Deserialize.Xml`.

A release is never redone: a fix gets a new version.

## Target frameworks and SDK

Every package targets the supported frameworks set in `eng/Directory.Build.Common.props`: the
current LTS, plus the newest release when it is newer (net10.0 today). `global.json` pins the SDK
and records the support dates; net11.0 is added once released.

## Adding a package to CI

Add one entry to the `build-test` matrix in `.github/workflows/ci.yml` (its `slnx`), one to the
`performance` matrix (its test project and folder) if it has performance tests, and one to the `pack`
matrix if it is released (and set `IsShippingLibrary`, once its public members have XML docs).

Check every package that uses the central versions in `eng/packaging/`: a Dependabot update reaches
all of them, and CI only proves it for the packages in the matrix.

## Repository settings (done by a maintainer)

- Settings → Advanced Security: Dependabot alerts and security updates, Secret Protection and push
  protection on.
- Settings → Rules → Rulesets → `main`: the five `build-test` jobs required before merging; deleting
  `main` and force-pushing to it blocked. A required name must match a job exactly: renaming a job in
  `ci.yml` means updating this list.
