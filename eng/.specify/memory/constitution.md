# Forestry SDK Engineering Constitution

Rules no design decision may override, for the SDK-wide engineering silo (`eng/`) only. Each rule
names the spec-kit area it covers (Architecture, Code quality, Security, Documentation, Workflow).
Terms are defined under Definitions. Agreed by the developer in #71 (2026-11-07).

**Scope**: what every package in `forestry-sdk-for-net` is built, tested, versioned, packed and
published with: `eng/`, the root `Directory.Build.props` and `Directory.Build.targets`,
`global.json`, `NuGet.Config`, `.github/` (workflows, Dependabot) and `doc/dev/`. A package's own
code and tests are governed by that package's silo (`sdk/xml/Forestry.Xml/`,
`sdk/deserialize/Forestry.Deserialize/`), whose rule A3 points here.

## Core Principles

### C0. Published only by the pipeline

- **Covers**: Workflow
- **Rule**: A package MUST reach a feed only through the pipeline, from a commit on `main`, never
  pushed from a developer machine.
- **Rationale**: What others install must be traceable to reviewed, tested source.
- **Checked by**: Only the pipeline holds publish rights to each feed.

### C1. Independent versions

- **Covers**: Workflow
- **Rule**: Each package MUST version on its own, following SemVer 2.0. Every release has an entry
  in that package's `CHANGELOG.md`.
- **Rationale**: The packages mature at different speeds; a reader fix must not force a release of
  every package.
- **Checked by**: The pipeline refuses a release whose version has no `CHANGELOG.md` entry.

### C2. Packaging gate

- **Covers**: Code quality
- **Rule**: Before a package is published it MUST have XML doc comments on every public member (a
  missing doc fails the build), tests for each public option's default, an override and the values
  it rejects, passing tests on every supported target framework, and a passing performance gate.
- **Rationale**: Published APIs are what others judge the SDK by; the package silos deferred usage
  tests for public settings to this gate (Forestry.Xml spec 001, research).
- **Checked by**: The pipeline runs the gate before publishing; a package failing it is not
  published.

### C3. Same build locally and in CI

- **Covers**: Workflow
- **Rule**: The pipeline MUST run the same `dotnet` commands a developer runs locally (build, test,
  pack). No build logic exists only in the pipeline.
- **Rationale**: Any CI failure can be reproduced on a machine.
- **Checked by**: Each pipeline step is a documented local command (`doc/dev/`).

### C4. Reproducible packages

- **Covers**: Code quality
- **Rule**: Builds MUST pin the .NET SDK (`global.json`) and be deterministic. Every package ships
  symbols (`.snupkg`) and SourceLink.
- **Rationale**: A consumer can step into the exact source of a release, and the same commit packs
  the same package.
- **Checked by**: The pipeline uses `global.json`; packages are validated for symbols and SourceLink.

### C5. Design ownership

- **Covers**: Workflow
- **Rule**: The developer owns the constitution and every spec-kit input. AI decisions are proposals
  until the developer confirms them.
- **Rationale**: Same as the package silos.
- **Checked by**: Confirmed decisions; review tasks closed by the developer.

### C6. Tried before merged

- **Covers**: Workflow
- **Rule**: A pipeline change MUST run green on its own pull request before it merges. Release and
  publish steps are first tried as a dry run (pack without push, or a preview feed).
- **Rationale**: A broken pipeline on `main` blocks every package.
- **Checked by**: The pull request's checks; a dry run before the first real publish.

### D0. Engineering is documented

- **Covers**: Documentation
- **Rule**: `doc/dev/` MUST describe how to build, test, version and release, and match what the
  pipeline does. Leftovers from Azure DevOps (Azure Pipelines) are rewritten or removed.
- **Rationale**: Others are invited to try the libraries and give feedback; they need to build them.
- **Checked by**: Each spec touching the pipeline updates `doc/dev/` in the same change.

### S0. No secrets in the repository

- **Covers**: Security
- **Rule**: Secrets MUST NOT be in the repository or in logs. Publishing prefers keyless trusted
  publishing (OIDC); any remaining secret lives in a protected GitHub environment. Workflows declare
  least-privilege permissions.
- **Rationale**: The repository is public.
- **Checked by**: Review of every workflow; no long-lived publish key where a keyless option exists.

### S1. Supply chain

- **Covers**: Security
- **Rule**: Third-party CI actions MUST be pinned to a commit SHA, and restore MUST fail on known
  vulnerable packages (NuGet audit). Updates come in as reviewed pull requests (e.g. Dependabot).
- **Rationale**: The packages are only as trustworthy as what builds them.
- **Checked by**: Workflow review; the restore's audit setting.

## Additional Constraints

- **A0** (Architecture): This silo owns `Directory.Build.*`, `eng/`, `global.json`, `NuGet.Config`,
  `.github/` and `doc/dev/`. The package silos' A3 points here: packages reference the shared
  engineering and never copy it. A change here that affects packages is checked by building and
  testing every package.

## Definitions

- **Pipeline**: The CI/CD workflows that build, test, pack and publish the packages.
- **Feed**: A NuGet package source others install from (a preview feed or nuget.org).
- **Release**: A package version published to a feed, with its `CHANGELOG.md` entry.
- **Packaging gate**: The checks in C2 a package passes before it is published.
- **Supported target frameworks**: The .NET versions every package targets: the current LTS, plus
  the newest release when it is newer; a version out of Microsoft support is dropped. Listed in
  `eng/Directory.Build.Common.props`, with the support dates in `global.json`.
- **Performance gate**: The tests carrying the trait `Category=Performance`, run with
  `dotnet test -s Performance.runsettings` (package silos, A4).

## Governance

- **G0** (Workflow): The spec-kit markdown is the source of truth and MUST NOT diverge from the
  code. Board features, tasks and bugs are motivators for moving forward: they may diverge from the
  truth, the spec-kit markdown may not.
- **G1** (Workflow): Changing a principle is a deliberate amendment (versioned, dated, with a
  reason). Versions follow MAJOR (principle removed or redefined), MINOR (principle or section
  added), PATCH (wording only). A version takes effect when merged to `main`.
- **G2** (Workflow): Until a working `1.0.0-beta` exists, the developer is the only contributor.

## Amendments

- **1.0.0** (2026-11-07, agreed by the developer in #71, takes effect on merge to `main`). First
  version.

**Version**: 1.0.0 | **Ratified**: 2026-11-07 | **Last Amended**: 2026-11-07
