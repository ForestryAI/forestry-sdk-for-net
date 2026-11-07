# Implementation Plan: CI/CD for the SDK packages

**Branch**: `eng-001-ci-cd` | **Date**: 2026-11-07 | **Spec**: [spec.md](spec.md) | **Issue**: #71

**Input**: Feature specification from `eng/specs/001-ci-cd/spec.md`, decisions R0-R8 in
[research.md](research.md) (all confirmed by the developer, 2026-11-07).

## Summary

Move the SDK's engineering from Azure DevOps to GitHub for the three walking-skeleton packages
(`Forestry.Xml`, `Forestry.Deserialize`, `Forestry.Deserialize.Xml`). Two GitHub Actions workflows
run plain `dotnet` commands a developer can run locally (C3): `ci.yml` checks every pull request and
push to `main` (compile, test, pack, packaging gate, performance gate, coverage), `release.yml`
releases one package by hand to a GitHub release (the preview feed, R4). The MSBuild side
(`Directory.Build.*`, `eng/`) gains what packing and releasing need (symbols, SourceLink,
deterministic CI builds, package metadata, a CI version suffix, release notes from `CHANGELOG.md`,
package validation), and the Azure DevOps leftovers are removed (R0).

## Technical Context

**Language/Version**: C# (`LangVersion` latest), .NET SDK pinned in `global.json` (10.0.201 today,
`rollForward: latestPatch` after R0); target framework net10.0 only (R8)

**Primary Dependencies**: GitHub Actions (`actions/checkout`, `actions/setup-dotnet`,
`actions/cache`, `actions/upload-artifact`, `actions/download-artifact`, all pinned to a commit SHA,
S1); the `gh` CLI preinstalled on GitHub runners for releases; Dependabot. No third-party MSBuild
SDK or tool (MinVer rejected in R2, `Microsoft.Build.Traversal` dropped in R7)

**Storage**: Run artifacts (packages, test results, coverage) kept by GitHub per run; released
packages as GitHub release assets

**Testing**: The packages' xunit tests (`dotnet test`, default `*.Tests.runsettings` excluding
`Category=Performance`); the performance gate with `Performance.runsettings`; coverage with the
existing `coverlet.collector`. The pipeline itself is tested by the acceptance scenarios in
[quickstart.md](quickstart.md), run on pull requests to this branch (C6)

**Target Platform**: GitHub-hosted `ubuntu-latest` runners (R6.1); every step also runs on the
developer's Windows machine

**Project Type**: Build and release engineering for a multi-package .NET library repository

**Performance Goals**: A pull request check under 10 minutes for the three packages (SC-001)

**Constraints**: Public repository: no secrets (S0); fork pull requests read-only; least-privilege
`permissions:`; the performance job gates on allocations and memory growth only, never timings
(R6.7)

**Scale/Scope**: 3 packages, 3 test projects, 1 framework; adding a package is one line in `ci.yml`'s
matrix (FR-015)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Rule | How the plan meets it | Pre | Post |
|---|---|---|---|
| C0 Published only by the pipeline | Only `release.yml` creates releases, from `main`; its job alone has `contents: write` | ✅ | ✅ |
| C1 Independent versions | `<Version>` per `.csproj`; the release target refuses a version without a dated `CHANGELOG.md` heading; one package per release run | ✅ | ✅ |
| C2 Packaging gate | `GenerateDocumentationFile` for every packable project (missing docs fail through warnings as errors); option usage tests required per package (contract `packaging-gate.md`); tests on net10.0; performance job; package validation once a release exists | ✅ | ✅ |
| C3 Same build locally and in CI | Every workflow step is a `dotnet` command on the package's own `.slnx` or project, listed in `doc/dev/Pipelines.md` | ✅ | ✅ |
| C4 Reproducible packages | `global.json` pinned, `latestPatch`; `ContinuousIntegrationBuild` in CI; `.snupkg`; SDK SourceLink | ✅ | ✅ |
| C5 Design ownership | Decisions R0-R8 confirmed by the developer; the plan adds none of its own beyond those marked *plan* below for review | ✅ | ✅ |
| C6 Tried before merged | The workflows are developed on this branch's pull request; `release.yml` has a dry-run input (pack and checks, no release) tried before the first release | ✅ | ✅ |
| D0 Engineering is documented | `doc/dev/Pipelines.md` rewritten, `Building.md` reshaped, `eng/README.md` map updated in the same stories | ✅ | ✅ |
| S0 No secrets | No repository secret at all: releases use the workflow's `GITHUB_TOKEN` | ✅ | ✅ |
| S1 Supply chain | Actions pinned to SHAs; `NuGetAudit` with `NuGetAuditMode` all, failing through warnings as errors; Dependabot (research notes 1-4) | ✅ | ✅ |
| A0 Owner of shared engineering | Changes stay in root `Directory.Build.*`, `eng/`, `.github/`, `doc/dev/`; package folders only get their `CHANGELOG.md` and `<Version>` / `ComptableVersion` edits | ✅ | ✅ |
| G0 Source of truth | `eng/README.md` and `doc/dev/` updated with the code; plan, data model and contracts match the workflows | ✅ | ✅ |

No violations; Complexity Tracking is empty.

**Outside this silo**: the option usage tests (C2) and a performance test for `Forestry.Deserialize`
are package code. This plan requires them and checks them in the pipeline; writing them is a task
in this spec's stories, done under each package silo's rules (A4 test labels, test-first C4 of the
package silos).

## Design decisions made in the plan (*plan*, for the developer's review)

These follow from R0-R8 but were not asked in research. Reviewed by the developer, 2026-11-07: P1
revised (with R7), P2-P10 confirmed as written.

| # | Decision | Why |
|---|---|---|
| P1 | `ci.yml` matrix: one job per package in scope, each on its own `.slnx` (`sdk/xml/Forestry.Xml/Forestry.Xml.slnx`, …); every package on every pull request for now | R7 as revised by the developer: packages are separate building blocks; the local command is the package's own `dotnet test <slnx>` |
| P2 | Version suffix for non-release packs: `-p:VersionSuffixCi=<run number>` appends `.ci.<n>` to a prerelease (`1.0.0-beta.1.ci.42`) or `-ci.<n>` to a release version, in `eng/Directory.Build.Common.targets` | R2; a `<Version>` in the `.csproj` overrides `VersionSuffix`, so the SDK's own suffix cannot be used |
| P3 | `CHANGELOG.md` is read by an MSBuild target in `eng/` (`ReadReleaseNotes`): it finds `## <Version> (<yyyy-MM-dd>)`, sets `PackageReleaseNotes`, and with `-p:Release=true` fails if the heading is missing or still says `Unreleased` | C1, C3: the same check runs locally (`dotnet pack -p:Release=true`) and in `release.yml` |
| P4 | Tag format `<PackageId>/v<Version>` (`Forestry.Xml/v1.0.0-beta.1`); release title the same; assets `.nupkg` and `.snupkg` | R5 |
| P5 | Package validation baseline from the package's last GitHub release: `release.yml` and `ci.yml` download the last release's `.nupkg` with `gh release download` and pass `-p:PackageValidationBaselinePath=…`; skipped while no release exists | R6.9; the baseline is not on a NuGet feed (R4), so `PackageValidationBaselineVersion` cannot be used |
| P6 | Dependency check: `release.yml` refuses a package whose `ProjectReference`d packages have no release tag for the version packed as its dependency | FR-010, spec Story 3 scenario 4 |
| P7 | Test projects and anything outside `sdk/*/src` get `IsPackable=false` centrally | Only the three libraries are packed |
| P8 | Coverage: `--collect "XPlat Code Coverage"`, Cobertura files uploaded as artifacts and summarised in the job summary; no threshold | R6.6 |
| P9 | `Forestry.Deserialize`'s performance run has no tests yet: the gate passes with zero tests (exit 0, checked 2026-11-07) and the plan adds no test there; a memory test is a package-silo follow-up | R6.7, A0 |
| P10 | Workflow names `ci.yml` and `release.yml`; `dependabot.yml` in `.github/` | `eng/README.md` map |

## Project Structure

### Documentation (this feature)

```text
eng/specs/001-ci-cd/
├── spec.md
├── research.md          # R0-R8, confirmed
├── plan.md              # This file
├── data-model.md        # Package, version, release, check
├── quickstart.md        # Acceptance runs for each story
├── contracts/
│   ├── ci-workflow.md       # Triggers, jobs, steps and their local commands
│   ├── release-workflow.md  # Inputs, checks, outputs
│   └── packaging-gate.md    # What a package must pass before release
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
.github/
├── dependabot.yml                 # new: nuget (/eng/packaging), github-actions; weekly, grouped
└── workflows/
    ├── ci.yml                     # new: pull_request + push to main
    └── release.yml                # new: workflow_dispatch (package, dry run)

Directory.Build.props              # reshape: CI build, symbols, package metadata
Directory.Build.targets            # keep
global.json                        # reshape: latestPatch, no Traversal, support dates (R8)
NuGet.Config                       # keep (audit settings in eng/)
package.json                       # remove
README.md                          # links eng/README.md (done)

eng/
├── README.md                      # map, updated as files land
├── Directory.Build.Common.props   # reshape: net10.0 only, metadata, docs, IsPackable, audit
├── Directory.Build.Common.targets  # reshape: CI suffix (P2), release notes (P3); keep FSDK00001
├── Directory.Build.Version.targets # remove
├── packaging/                     # keep; stale test packages left to Dependabot
├── pipelines/                     # remove
└── scripts/                       # remove

doc/dev/
├── Pipelines.md                   # rewrite
└── Building.md                    # reshape

sdk/xml/Forestry.Xml/src/Forestry.Xml.csproj                         # drop ComptableVersion
sdk/deserialize/Forestry.Deserialize/src/*.csproj, CHANGELOG.md      # drop ComptableVersion; new CHANGELOG
sdk/deserialize/Forestry.Deserialize.Xml/src/*.csproj, CHANGELOG.md  # drop ComptableVersion; new CHANGELOG
sdk/{papinet,raindrop,stanford,turn}/**/*.csproj                     # drop ComptableVersion only (R0)
```

**Structure Decision**: Workflows in `.github/workflows/` only call `dotnet` on
a package's own `.slnx` or project; all build behaviour lives in MSBuild files under the root and
`eng/` (A0, C3). Package folders change only in their version and changelog files.

## Story order

1. **US1 Every change is checked**: P1 (matrix), reshaped props (net10.0, audit), removals, `ci.yml` build
   and test jobs, performance job, coverage, `dependabot.yml`, `global.json`, `doc/dev/` for these.
2. **US2 Pack and gate**: symbols, SourceLink, metadata, `IsPackable`, P2, pack job and artifacts,
   option usage tests per package (contract `packaging-gate.md`), package validation wiring (P5,
   inactive until a release).
3. **US3 Release**: P3, P4, P6, `release.yml` with dry run, CHANGELOGs for the two Deserialize
   packages, first release of `Forestry.Xml` after a dry run, reviewer instructions in the package
   READMEs.

## Complexity Tracking

None.
