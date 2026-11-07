# Forestry SDK for .NET - Pipelines

CI runs on GitHub Actions. Every step is a plain `dotnet` command you can run on your own machine,
so any failure in CI can be reproduced locally (eng constitution C3). Which file owns which purpose
is mapped in [eng/README.md](../../eng/README.md); the decisions behind this page are in
[eng/specs/001-ci-cd](../../eng/specs/001-ci-cd/).

## What runs when

| Event | What runs |
|---|---|
| A pull request to `main` is opened or updated | `ci.yml`: build and test, and the performance gate, for every package |
| A push to `main` (a merged pull request) | `ci.yml`, the same |
| Weekly | Dependabot opens grouped pull requests for NuGet packages and GitHub Actions; each is checked by `ci.yml` like any change |

A newer push to a pull request cancels its older run; runs on `main` always finish.

## How the workflows are built

```text
.github/
├── actions/setup/action.yml   setup steps every phase repeats: .NET from global.json, NuGet cache
├── workflows/
│   ├── ci.yml                 decides WHEN and FOR WHICH PACKAGES; calls the phases
│   ├── build-test.yml         phase: restore, build, test, coverage, summary
│   └── performance.yml        phase: the performance gate
└── dependabot.yml             weekly dependency updates
```

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

Notes:

- **Debug, not Release**: tests run in the Debug configuration, like your everyday `dotnet test`,
  so `Debug.Assert` invariants are checked too. Packing and releasing will build Release.
- **The default test run leaves out performance tests** (trait `Category=Performance`) through each
  test project's `.runsettings`; `Performance.runsettings` runs only them. They gate allocations and
  memory growth, never timings: shared runners are noisy. A package without performance tests
  passes the gate with zero tests.
- **CI adds `-p:ContinuousIntegrationBuild=true`** to the build: it only normalises file paths in
  the output (deterministic builds); the build behaves the same.
- **Reading a failure**: open the failed job; the *Summary* tab lists failed tests with their target
  framework and the line coverage. Test results and coverage files are attached to the run as
  artifacts (`test-results-<Package>`).

## Target frameworks and SDK

Every package targets the supported frameworks set in `eng/Directory.Build.Common.props`: the
current LTS, plus the newest release when it is newer (net10.0 today). `global.json` pins the SDK
and records the support dates; net11.0 is added once released.

## Adding a package to CI

Add one entry to each matrix in `.github/workflows/ci.yml` (its `slnx` for `build-test`; its test
project and folder for `performance`). Nothing else changes.

## Repository settings (done by a maintainer)

- Settings → Security: Dependabot alerts and security updates on.
- Settings → Branches: the `build-test` jobs required before merging to `main`.
