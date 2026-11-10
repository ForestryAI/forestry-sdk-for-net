---

description: "Tasks for engineering spec 001: CI/CD for the SDK packages"
---

# Tasks: CI/CD for the SDK packages

**Input**: Design documents from `eng/specs/001-ci-cd/` (spec.md, research.md R0–R8 and the
Dependabot notes as reviewed by the developer on 2026-11-07, plan.md with P1–P10 as reviewed the same
day, data-model.md, contracts/ci-workflow.md, contracts/release-workflow.md,
contracts/packaging-gate.md, quickstart.md) and `eng/.specify/memory/constitution.md` 1.0.0.

**Issue**: #71. Each story is a sub-issue of #71: US1 #73 (with Setup and Foundational), US2 #74,
US3 #75 (with Polish). One issue per story, as in the package silos, not one per task.

**Tests**: The pipeline is tested by the acceptance runs in [quickstart.md](quickstart.md), run on
the story's own pull request (C6). Package code written here (the option usage tests of US2) follows
the package silos' test-first rule: test shells, then the developer's **test review gate**, then the
tests' bodies.

**Gates in every story** (story gates, as in the package silos):

- **Review gate**: the developer reviews the story's diff before it is pushed.
- **Local gate**: for each package in scope, `dotnet test <slnx>` and
  `dotnet test <test project> -s <test folder>/Performance.runsettings` pass, in Debug as CI tests
  (C3, P11).
- **Pull request gate**: the story's pull request runs green with the story's quickstart runs done
  (C6); the story's docs (`doc/dev/`, `eng/README.md`) are in the same pull request (D0).

**Branches**: this spec's documents merge first (branch `eng-001-ci-cd`, as reader spec 001 did with
PR #68). Then one branch per story from `main` (`eng-001-us1-checks`, `eng-001-us2-pack`,
`eng-001-us3-release`), each deleted after its merge.

**Organization**: Setup, Foundational, one phase per story in priority order, then Polish.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1, US2, US3 (spec.md user stories)
- Paths are relative to the repository root

## Packages in scope (FR-015)

| Package | Solution | Test project |
|---|---|---|
| `Forestry.Xml` | `sdk/xml/Forestry.Xml/Forestry.Xml.slnx` | `sdk/xml/Forestry.Xml/test/Forestry.Xml.Tests.csproj` |
| `Forestry.Deserialize` | `sdk/deserialize/Forestry.Deserialize/Forestry.Deserialize.slnx` | `sdk/deserialize/Forestry.Deserialize/test/Forestry.Deserialize.Tests.csproj` |
| `Forestry.Deserialize.Xml` | `sdk/deserialize/Forestry.Deserialize.Xml/Forestry.Deserialize.Xml.slnx` | `sdk/deserialize/Forestry.Deserialize.Xml/test/Forestry.Deserialize.Xml.Tests.csproj` |

`Forestry.Raindrop` and `Forestry.Turn` are also in `ci.yml`'s `build-test` matrix (R7, 2026-11-09);
they are not packed or released here.

## Not in this spec

nuget.org publishing (a later spec), running only changed packages (R7), a coverage threshold (R6.6),
a performance test for `Forestry.Deserialize` (P9, deserialization silo), amending the package silos'
A0 "Targets net9 and net10" (each silo, R8), other packages (Raindrop, Turn, StanForD, PapiNet,
Biometria) beyond removing their `<ComptableVersion>`.

---

## Phase 1: Setup (spec documents and branch)

**Purpose**: The spec's documents reach `main` before any story changes the build.

- [X] T001 Push branch `eng-001-ci-cd` and open its pull request to `main` with `eng/.specify/`, `eng/README.md`, `eng/specs/001-ci-cd/` and the root `README.md` link; the developer reviews and merges it (no build behaviour changes, so no checks are expected yet)
- [X] T002 Create the story sub-issues of #71 with `/speckit-github-taskstoissues` (US1 with Setup and Foundational, US2, US3 with Polish), each issue's Architecture section carrying its story's scope and design decisions (R/P numbers) and a checklist synced with this file

**Checkpoint**: Spec 001 is on `main`; the board has #71 with three story sub-issues.

---

## Phase 2: Foundational (shared build changes every story needs)

**Purpose**: Clean the Azure DevOps leftovers and move to the supported framework, on branch
`eng-001-us1-checks` from `main` (part of US1's pull request, US1's issue). No package code changes.

- [X] T003 [P] Remove `eng/pipelines/` (7 YAML files), `eng/scripts/` (PowerShell modules, Pester tests, `Set-Environment.ps1`) and the root `package.json` (R0)
- [X] T004 Remove `eng/Directory.Build.Version.targets` and its `<Import Project="Directory.Build.Version.targets" />` from `eng/Directory.Build.Common.targets`; keep the FSDK00001 guard (R0)
- [X] T005 [P] Remove `<ComptableVersion>1.0.0</ComptableVersion>` from every `.csproj` that has it: `sdk/xml/Forestry.Xml/src/Forestry.Xml.csproj`, `sdk/deserialize/Forestry.Deserialize/src/Forestry.Deserialize.csproj`, `sdk/deserialize/Forestry.Deserialize.Xml/src/Forestry.Deserialize.Xml.csproj`, `sdk/papinet/src/Forestry.PapiNet.csproj`, `sdk/raindrop/Forestry.Raindrop/src/Forestry.Raindrop.csproj`, `sdk/stanford/Forestry.StanForD/src/Forestry.StanForD.csproj`, `sdk/turn/Forestry.Turn/src/Forestry.Turn.csproj` (R0)
- [X] T006 In `eng/Directory.Build.Common.props` set `RequiredTargetFrameworks` to `$(LatestTargetFramework)` only (net10.0), with a comment pointing to the support policy in `global.json` (R8)
- [X] T007 In `global.json` set `rollForward` to `latestPatch`, remove the `msbuild-sdks` entry `Microsoft.Build.Traversal`, and add a `_support` entry in the file's `_remarks` style: "net10.0 (LTS) end of support 2028-11-14; net11.0 (STS) expected November 2026, add it as a second target framework once released; see https://dotnet.microsoft.com/platform/support/policy" (R0, R8)
- [X] T008 In `eng/Directory.Build.Common.props` turn on NuGet audit for all dependencies: `<NuGetAudit>true</NuGetAudit>`, `<NuGetAuditMode>all</NuGetAuditMode>`, `<NuGetAuditLevel>low</NuGetAuditLevel>`; a finding fails restore through `TreatWarningsAsErrors` (S1, R6.11). If restore fails on today's packages, report the advisories to the developer instead of suppressing them
- [X] T009 Run the local gate for the three packages (table above) after T003–T008: builds without warnings on net10.0 only, all default and performance runs pass; record the restore's audit result (depends on T003–T008)

**Checkpoint**: The build is GitHub-agnostic, net10.0 only and audited; nothing from Azure DevOps remains in `eng/`.

---

## Phase 3: User Story 1 - Every change is checked (Priority: P1) 🎯 MVP

**Goal**: Every pull request and push to `main` compiles and tests each package in scope in its own
job, shows the result on the pull request, and runs the performance gate apart (FR-001 to FR-004,
FR-007, FR-013; R5, R6, R7; P1, P8).

**Independent Test**: quickstart.md, "US1 Every change is checked", steps 1–6.

### Implementation for User Story 1

- [X] T010 [US1] Create the composite action `.github/actions/setup/action.yml` (P10): `actions/setup-dotnet` with `global-json-file: global.json` (no extra runtime, R8) and `actions/cache` for `~/.nuget/packages` keyed on `runner.os` and `hashFiles('eng/packaging/*.props', '**/*.csproj')` (R6.3); every action pinned to a full commit SHA with its version as a trailing comment (S1, R6.12)
- [X] T011 [US1] Create the phase `.github/workflows/build-test.yml` (`on: workflow_call`, inputs `package`, `slnx`) per `contracts/ci-workflow.md`: `actions/checkout`, `./.github/actions/setup`, `dotnet restore <slnx>`, `dotnet build <slnx> --no-restore -p:ContinuousIntegrationBuild=true`, `dotnet test <slnx> --no-build --collect "XPlat Code Coverage" --logger trx --results-directory artifacts/test-results/<package>`; then upload `artifacts/test-results/<package>` with `actions/upload-artifact` (`if: always()`) and write a job summary naming each failed test with its target framework (from the `.trx` files) and the line coverage from the Cobertura file; no coverage threshold (FR-003, R6.6, P8) (depends on T010)
- [X] T012 [US1] Create the phase `.github/workflows/performance.yml` (`on: workflow_call`, inputs `package`, `test-project`, `test-folder`): `actions/checkout`, `./.github/actions/setup`, `dotnet test <test-project> -s <test-folder>/Performance.runsettings` (Debug, P11); `Forestry.Deserialize` has no performance test and passes with zero tests (R6.7, P9) (depends on T010)
- [X] T013 [US1] Create the orchestrator `.github/workflows/ci.yml` per `contracts/ci-workflow.md`: triggers `pull_request` (branches `main`) and `push` (branches `main`); `permissions: contents: read`; `concurrency` group per ref with `cancel-in-progress: true`; a matrix of the three packages (`package`, `slnx`, `test-project`, `test-folder`, table above) with `fail-fast: false`; job `build-test` calling `./.github/workflows/build-test.yml` and job `performance` calling `./.github/workflows/performance.yml`, each with the matrix entry as inputs; runner `ubuntu-latest` in the phases (R6.1) (depends on T011, T012)
- [X] T014 [P] [US1] Create `.github/dependabot.yml` per the research's Dependabot notes: ecosystem `nuget` with `directories: ["/sdk/*/*"]` (corrected 2026-11-09, R9: Dependabot does not search below a listed folder), weekly, groups `test-packages` (`Microsoft.NET.Test.Sdk`, `xunit*`, `coverlet.*`) and `microsoft-extensions` (`Microsoft.Extensions.*`), `open-pull-requests-limit: 5`, ignoring `Microsoft.Extensions.*` and `System.*` major versions above 10 (note 4); ecosystem `github-actions` at `/` and `/.github/actions/setup`, weekly, one group
- [X] T015 [P] [US1] Rewrite `doc/dev/Pipelines.md`: what `ci.yml` runs and when (R5), the matrix (R7), and per step the local command from `contracts/ci-workflow.md` (C3, D0); no Azure Pipelines content
- [X] T016 [P] [US1] Reshape `doc/dev/Building.md`: keep the MSBuild layout (props order, `artifacts/`, CPM), remove the "Biometria" wording, the Azure Pipelines section and the date-based version section (T004), link `doc/dev/Pipelines.md` and `eng/README.md` (D0)
- [X] T017 [P] [US1] Update `eng/README.md`: drop *planned* from the rows that now exist (SDK version, restore and cache, what CI builds, test and coverage, performance gate, dependency updates), and add a status badge for `ci.yml` to the root `README.md`
- [X] T018 [US1] **Review gate**: the developer reviews the diff of T003–T017 (depends on T003–T017)
- [X] T019 [US1] Push `eng-001-us1-checks`, open its pull request to `main`, and run quickstart.md "US1" steps 1–3 and 5 on it (a deliberately failing test and a deliberate warning in throwaway commits, reverted before merge); all jobs green at the end, under 10 minutes (SC-001) (depends on T018)
- [X] T020 [US1] After the developer merges: check the run on `main` (quickstart US1 step 4) and, after Dependabot's first weekly run, that its pull requests change `eng/packaging/Directory.Packages.props` and `eng/packaging/Directory.Support.Packages.props`; if the support file is not updated, fold it into `Directory.Packages.props` in a follow-up pull request (Dependabot note 1); delete branch `eng-001-us1-checks` (depends on T019)
- [X] T021 [US1] Ask the developer to switch on Dependabot alerts and security updates in the repository's Settings → Security, and to make the `build-test` jobs required checks for `main` (repository settings are the developer's)

**Checkpoint**: Every pull request shows one green or red job per package (US1 done, MVP).

---

## Phase 4: User Story 2 - Packages are packed and gated on every change (Priority: P2)

**Goal**: Every check packs each package with symbols, SourceLink, metadata and a CI version suffix,
and runs the packaging gate; packages can be downloaded from the run (FR-005, FR-006; R6.8, R6.9;
P2, P5, P7; contract `packaging-gate.md`).

**Independent Test**: quickstart.md, "US2 Pack and gate", steps 1–3.

### Test shells for User Story 2 (package code, package silos' test-first rule) ⚠️

- [X] T022 [US2] Complete the "Public options in scope" table in `eng/specs/001-ci-cd/contracts/packaging-gate.md` from the code: for each public option of `sdk/xml/Forestry.Xml/src/Reading/ReaderOptions.cs`, `sdk/deserialize/Forestry.Deserialize/src/DeserializeOptions.cs` (and `DeserializeOptions.Caching.cs`), `sdk/deserialize/Forestry.Deserialize/src/Policies/*.cs` and `sdk/deserialize/Forestry.Deserialize.Xml/src/DeserializeXmlOptions.cs`, its default, a valid override and the values it rejects (what the setter or `MakeReadOnly()` throws); list existing tests that already cover a cell
- [X] T023 [P] [US2] Write test shells (`// Arrange`, `// Act`, `// Assert`, bodies `Assert.Fail("shell")`) for the uncovered `Forestry.Xml` cells of T022 in `sdk/xml/Forestry.Xml/test/ReaderOptionsTests.cs`, one test per cell, named `<Option>_<Default|Override|Rejects><Case>` (depends on T022)
- [X] T024 [P] [US2] Write test shells for the uncovered `Forestry.Deserialize` cells in `sdk/deserialize/Forestry.Deserialize/test/DeserializeOptionsTests.cs` (and `PolicyOptionsTests.cs` for the policies), same naming (depends on T022)
- [X] T025 [P] [US2] Write test shells for the uncovered `Forestry.Deserialize.Xml` cells in `sdk/deserialize/Forestry.Deserialize.Xml/test/DeserializeXmlOptionsTests.cs`, same naming; none if T022 finds no members beyond `DeserializeOptions` (depends on T022)
- [X] T026 [US2] **Test review gate**: the developer reviews T022's table and the shells T023–T025 before any body is written (depends on T023–T025)

### Implementation for User Story 2

- [X] T027 [US2] Write the bodies of T023–T025; the default run of each package passes. A rejected value the code does not reject today is reported to the developer, not fixed here (a package silo's change) (depends on T026)
- [X] T028 [US2] In `eng/Directory.Build.Common.props`, for a library that sets `IsShippingLibrary` in its own `Directory.Build.props` (P7, 2026-11-10): `IsPackable=true`, `GenerateDocumentationFile=true`, `IncludeSymbols=true`, `SymbolPackageFormat=snupkg`, `PublishRepositoryUrl=true`, `EmbedUntrackedSources=true`, `PackageLicenseExpression=MIT`, `RepositoryUrl=https://github.com/ForestryAI/forestry-sdk-for-net`, `RepositoryType=git`, `PackageProjectUrl` to the package's folder on GitHub, `PackageReadmeFile=README.md` with the package folder's `README.md` packed at `/`; for everything else `IsPackable=false` (P7). `Authors` and `Company`: ask the developer at T034 (today "Coders" / "Forestry"); `ContinuousIntegrationBuild=true` when `GITHUB_ACTIONS` is `true` (C4, R6.8)
- [X] T029 [US2] Make a missing XML doc fail for packable projects: remove `<WarningsNotAsErrors>$(WarningsNotAsErrors);CS1591</WarningsNotAsErrors>` from `sdk/xml/Forestry.Xml/src/Forestry.Xml.csproj` and `sdk/deserialize/Forestry.Deserialize/src/Forestry.Deserialize.csproj` (0 missing docs in the three packages, checked 2026-11-07). This reverses the deserialization silo's R9 ("missing docs warn until the first real StanForD document") and the reader's T001 choice, as the developer confirmed on 2026-11-07 (C2, A0) (depends on T028)
- [X] T030 [US2] In `eng/Directory.Build.Common.targets` add the CI suffix (P2): when `VersionSuffixCi` is set, a target before `GetAssemblyVersion` and `GenerateNuspec` sets `Version` to `$(Version).ci.$(VersionSuffixCi)` when `Version` contains `-`, else `$(Version)-ci.$(VersionSuffixCi)`; the SDK's default `AssemblyVersion` and `FileVersion` are kept (they ignore the prerelease part)
- [X] T031 [US2] In `eng/Directory.Build.Common.targets` wire package validation (P5): `EnablePackageValidation=true` for packable projects; when `PackageValidationBaselinePath` is passed, validation compares against that file; nothing when it is not (no release yet, R6.9)
- [X] T032 [US2] Create the phase `.github/workflows/pack.yml` (`on: workflow_call`, inputs `package`, `slnx`; `permissions: contents: read`): `actions/checkout`, `./.github/actions/setup`, `dotnet pack <slnx> -c Release -p:VersionSuffixCi=${{ github.run_number }}`; when the package has a GitHub release, download the latest one's `.nupkg` with `gh release list` / `gh release download` (pattern `<PackageId>/v*`, `GH_TOKEN` the workflow's token, read-only) and pass `-p:PackageValidationBaselinePath=<file>`; upload `artifacts/packages/Release/` as artifact `packages-<package>`; in `ci.yml` add job `pack` calling it per matrix entry, independent of `build-test` (R7: a failure stays on its own package) (contract `ci-workflow.md`, P10) (depends on T028–T031)
- [X] T033 [P] [US2] Update `doc/dev/Pipelines.md` (pack job, CI suffix, package validation, local commands), `doc/dev/Building.md` (package metadata, symbols, README in the package), `eng/README.md` (drop *planned* for the pack row) and `contracts/packaging-gate.md`'s "Today" column (D0)
- [X] T034 [US2] **Review gate**: the developer reviews the diff of T027–T033 and settles `Authors`/`Company` (T028) (depends on T027–T033)
- [ ] T035 [US2] Push `eng-001-us2-pack`, open its pull request, run quickstart.md "US2" steps 1–3 (step 2 in a throwaway commit, reverted): packages downloadable with CI suffix, README, metadata, `.snupkg`; then local gate; after merge delete the branch (depends on T034)

**Checkpoint**: Every check produces gated packages; nothing is published (US2 done).

---

## Phase 5: User Story 3 - Release a package to a preview feed (Priority: P3)

**Goal**: The developer releases one package by hand from `main`: CHANGELOG and version checked,
dependencies released, tag and prerelease GitHub release with the packages and notes (FR-008 to
FR-012; R2–R5, R6.10, R6.13; P3, P4, P6; contract `release-workflow.md`).

**Independent Test**: quickstart.md, "US3 Release", steps 1–6.

### Implementation for User Story 3

- [ ] T036 [US3] In `eng/Directory.Build.Common.targets` add target `ReadReleaseNotes` before `GenerateNuspec` for packable projects (P3): read `$(MSBuildProjectDirectory)/../CHANGELOG.md`, find the heading `## $(Version) (` and take its section up to the next `## `; set `PackageReleaseNotes` to it; with `-p:Release=true`, error `FSDK00002` when the heading is missing and `FSDK00003` when it says `Unreleased` instead of a date `yyyy-MM-dd`; also write the section to `artifacts/release-notes/$(PackageId).md` for `release.yml`. Document both errors in `eng/packaging/README.md` next to FSDK00001
- [ ] T037 [P] [US3] Add `sdk/deserialize/Forestry.Deserialize/CHANGELOG.md` and `sdk/deserialize/Forestry.Deserialize.Xml/CHANGELOG.md` in `Forestry.Xml`'s style (`# Release History`, `## 1.0.0-beta.1 (Unreleased)`, `### Features Added` / `### Breaking Changes` / …), the entries drafted from the packages' READMEs and spec 001 (`sdk/deserialize/Forestry.Deserialize/specs/001-walking-skeleton/`) for the developer to edit at T041
- [ ] T038 [US3] Create `.github/workflows/release.yml` per `contracts/release-workflow.md`: `workflow_dispatch` with inputs `package` (choice of the three) and `dry-run` (boolean, default `true`); also `pull_request` on changes to `.github/workflows/release.yml`, `.github/workflows/build-test.yml` and `eng/Directory.Build.Common.targets`, always as a dry run of `Forestry.Xml` (so the workflow runs on its own pull request, C6); a job `checks` calling the phase `./.github/workflows/build-test.yml` for the chosen package (the very same checks as CI, P10); job `check` (`needs: checks`, `contents: read`): refuse unless the ref is `main` (except the pull request dry run), `actions/checkout`, `./.github/actions/setup`, `dotnet pack <src .csproj> -c Release -p:Release=true` (and the baseline of T032), refuse when `gh release view <PackageId>/v<Version>` succeeds (FR-010), refuse when a dependency in the packed `.nuspec` has no release `<Dependency>/v<Version>` (P6), upload the packages and release notes; all actions SHA-pinned (S1)
- [ ] T039 [US3] In `release.yml` add job `release` (`needs: check`, `if: inputs.dry-run == false && github.ref == 'refs/heads/main'`, `permissions: contents: write`, the only write permission in the repository's workflows, S0): download the check's artifacts and run `gh release create <PackageId>/v<Version> <nupkg> <snupkg> --target ${{ github.sha }} --title "<PackageId> <Version>" --notes-file artifacts/release-notes/<PackageId>.md`, with `--prerelease` when the version contains `-` (P4, R6.10) (depends on T038)
- [ ] T040 [P] [US3] Add a "Try the preview" section to `sdk/xml/Forestry.Xml/README.md`, `sdk/deserialize/Forestry.Deserialize/README.md` and `sdk/deserialize/Forestry.Deserialize.Xml/README.md`: download the `.nupkg` from the package's latest GitHub release (no sign-in), `dotnet nuget add source <folder> -n forestry-preview`, `dotnet add package <PackageId> --prerelease`; for `Forestry.Deserialize.Xml`, download its two dependencies too (quickstart US3 step 6, SC-003)
- [ ] T041 [P] [US3] Update `doc/dev/Pipelines.md` (how to release: date the CHANGELOG heading, merge, run `release.yml` with dry run, then without; what it refuses), `eng/README.md` (drop *planned* for the version, release and publish rows) (D0)
- [ ] T042 [US3] **Review gate**: the developer reviews the diff of T036–T041, edits the two new CHANGELOGs (T037) (depends on T036–T041)
- [ ] T043 [US3] Push `eng-001-us3-release`, open its pull request: `ci.yml` green and `release.yml`'s pull request dry run green (packages and notes as artifacts, no tag); run `dotnet pack sdk/xml/Forestry.Xml/src/Forestry.Xml.csproj -c Release -p:Release=true` locally to see FSDK00003 on today's `Unreleased` heading; after merge delete the branch (depends on T042)
- [ ] T044 [US3] First release, with the developer: date `Forestry.Xml`'s `## 1.0.0-beta.1` heading in a small pull request; after merge run `release.yml` for `Forestry.Xml` with `dry-run: true`, then `false`: tag `Forestry.Xml/v1.0.0-beta.1` and a prerelease with `.nupkg`, `.snupkg` and the notes; run it again and see it refused (quickstart US3 steps 1–5) (depends on T043)
- [ ] T045 [US3] Reviewer check on another machine or a clean folder without the repository: follow `sdk/xml/Forestry.Xml/README.md` "Try the preview", install, read a small document on net10.0, in under 10 minutes (quickstart US3 step 6, SC-003) (depends on T044)

**Checkpoint**: `Forestry.Xml` 1.0.0-beta.1 is on the preview feed; the other two follow when the developer dates their headings (Deserialize before Deserialize.Xml, P6).

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T046 Run quickstart.md end to end on `main` and record the results (dates, run links) in `eng/specs/001-ci-cd/quickstart.md`
- [ ] T047 Check `doc/dev/`, `eng/README.md` and this spec's documents against the workflows and MSBuild files (G0); mark the spec's status Implemented and close #71 and its sub-issues
- [ ] T048 Propose to the developer the follow-ups found: package silos' A0 amendment (net10.0), a performance test for `Forestry.Deserialize`, adding net11.0 once released (R8), the nuget.org spec, any rejected value T027 found unrejected

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: first; T001 merges the spec before any story branch exists.
- **Foundational (Phase 2)**: on US1's branch; blocks every story.
- **US1 (Phase 3)**: after Foundational; MVP.
- **US2 (Phase 4)**: after US1 is merged (its `pack` job extends `ci.yml`).
- **US3 (Phase 5)**: after US2 is merged (it uses the pack settings, the baseline download and the targets file).
- **Polish (Phase 6)**: after US3.

### Within each story

Implementation → docs → review gate → pull request with quickstart runs → merge → delete branch. In
US2, test shells → test review gate → bodies come before the build changes.

### Parallel Opportunities

- Foundational: T003 and T005 touch different files.
- US1: T011 and T012 (separate phase files) after T010; T014, T015, T016, T017 in parallel with the workflow tasks T010–T013.
- US2: T023, T024, T025 (one package each); T033 alongside T030–T032.
- US3: T037, T040, T041 in parallel with T036, T038, T039.

## Parallel Example: User Story 1

```text
T010 setup action → T011 build-test.yml, T012 performance.yml (parallel) → T013 ci.yml (calls both)
T014 dependabot.yml | T015 Pipelines.md | T016 Building.md | T017 eng/README.md  (in parallel)
```

## Implementation Strategy

### MVP first (User Story 1 only)

Setup, Foundational and US1 give a green check per package on every pull request and on `main`
(about a day, as estimated with the developer). Stop and show it.

### Incremental delivery

1. US1: every change checked (MVP).
2. US2: packages packed and gated on every change.
3. US3: `Forestry.Xml` released to the preview feed for reviewers; the Deserialize packages when
   their CHANGELOGs are dated.

## Notes

- [P] tasks touch different files with no dependency on an incomplete task.
- Every task's local command is the one `doc/dev/Pipelines.md` lists (C3).
- Commit after each task or logical group; ask the developer before committing, pushing or opening
  a pull request.
