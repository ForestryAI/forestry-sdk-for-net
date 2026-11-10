# Feature Specification: CI/CD for the SDK packages

**Feature Branch**: `eng-001-ci-cd`

**Created**: 2026-11-07

**Status**: Implemented (2026-11-10): US1 #73, US2 #74, US3 #75; `Forestry.Xml` 1.0.0-beta.1 released

**Issue**: #71 is this spec's feature (it also records the engineering constitution 1.0.0); its
stories become sub-issues once tasks exist.

**Input**: User description: "CI/CD for the Forestry SDK packages (short name ci-cd). Every pull
request and every push to main builds and tests all packages on net9.0 and net10.0 with the pinned
SDK; packages (starting with Forestry.Xml, Forestry.Deserialize, Forestry.Deserialize.Xml) are
packed and, after passing the packaging gate, published by the pipeline only, each with its own
version and CHANGELOG entry, first to a preview feed so others can try them and give feedback, later
to nuget.org behind a manual approval. Stages the developer listed: environment setup (dotnet
version), compile, test, build, performance tests, NuGet packaging and publishing. The existing eng/
and doc/dev/Pipelines.md were copied from Azure DevOps and are only an inventory, not a design
basis."

## User Scenarios & Testing *(mandatory)*

Two people use this feature. The **developer** changes a package and wants every change checked the
same way, and releases a package when it is ready. A **reviewer** (an architect, a colleague, a
potential user) installs a released preview package to try it and give feedback, without building
the repository. Every scenario is checked on the repository itself: a pull request, a merge to
`main`, a release.

### User Story 1 - Every change is checked (Priority: P1)

The developer opens a pull request. Without doing anything else, every package in scope is set up
with the pinned .NET SDK, compiled with warnings as errors, and tested on every supported target framework (net10.0 today;
net11.0 once released, research R8). The
result shows on the pull request within minutes, with the failing test or build error named. The
same check runs again when the change lands on `main`. Each check is a command the developer can run
locally to reproduce a failure (C3).

**Why this priority**: Nothing can be released safely before every change is checked; it is also the
first thing a reviewer looks for (a green check on `main`).

**Independent Test**: Open a pull request with a passing change, then one with a failing test and
one with a compiler warning; the first is green, the other two are red and name the cause.

**Acceptance Scenarios**:

1. **Given** a pull request changing `Forestry.Xml`, **When** it is opened or updated, **Then**
   every package in scope is compiled and its tests run on every supported target framework, and the result shows
   on the pull request.
2. **Given** a pull request whose change makes one test fail on one target framework only (once
   two are supported), **When** checked, **Then** the check fails and names the test and the
   target framework.
3. **Given** a change that adds a compiler warning, **When** checked, **Then** the check fails.
4. **Given** a merge to `main`, **When** it lands, **Then** the same check runs on `main` and its
   result is visible on the repository's front page.
5. **Given** a failing check, **When** the developer runs the documented local command (`doc/dev/`),
   **Then** the same failure reproduces.

---

### User Story 2 - Packages are packed and gated on every change (Priority: P2)

Every checked change also packs each package in scope, with symbols and source links, and runs the
packaging gate (C2) and the performance gate in a job of its own, so a package that would not be
fit to publish is known before anyone tries to release it. The packed packages can be downloaded
from the run for inspection; nothing is published.

**Why this priority**: Releasing (Story 3) depends on packages that pass the gate; finding gate
failures at release time would block the release.

**Independent Test**: On a pull request, download the packed packages and inspect their version,
metadata, symbols and source links; remove an XML doc comment from a public member and see the gate
fail.

**Acceptance Scenarios**:

1. **Given** a checked change, **When** packing runs, **Then** each package in scope has a package
   and a symbols package, with the version from its own version source, and can be downloaded from
   the run.
2. **Given** a public member without an XML doc comment, **When** checked, **Then** the gate fails
   and names the member.
3. **Given** a packed package, **When** opened by a debugger, **Then** its source links resolve to
   the exact commit in the repository.
4. **Given** a change that breaks a package's public API compared with its last release, **When**
   checked, **Then** the check fails or warns, naming the break (once a last release exists).
5. **Given** the performance gate, **When** it runs, **Then** it runs apart from the main check and
   its failure is shown separately.

---

### User Story 3 - Release a package to a preview feed (Priority: P3)

When a package is ready, the developer adds its version's entry to the package's `CHANGELOG.md`,
merges to `main`, and starts its release. The pipeline publishes that package only (C1), with its
release notes taken from the `CHANGELOG.md` entry, to the preview feed. A reviewer adds the feed and
installs the package by name and version.

**Why this priority**: Showing the libraries to others for feedback is the reason for this spec,
but it needs Stories 1 and 2.

**Independent Test**: Release `Forestry.Xml` to the preview feed, then install it in a fresh console
project on another machine by following the README, and read a document with it.

**Acceptance Scenarios**:

1. **Given** `Forestry.Xml` with a `CHANGELOG.md` entry for its version, **When** its release is
   started, **Then** only `Forestry.Xml` is published to the preview feed, with that entry as its
   release notes.
2. **Given** a release whose version has no `CHANGELOG.md` entry, **When** started, **Then** it is
   refused and nothing is published.
3. **Given** a version already published to the feed, **When** a release of the same version is
   started, **Then** it is refused.
4. **Given** `Forestry.Deserialize.Xml`, which depends on `Forestry.Xml` and `Forestry.Deserialize`,
   **When** released, **Then** its dependencies point at versions that exist on the same feed.
5. **Given** a reviewer following the package README, **When** they add the feed and install the
   package, **Then** it restores and runs on a supported target framework.

---

### Edge Cases

- A package outside the scope (FR-015) fails to build (`Forestry.PapiNet`: 8 errors on 2026-11-07):
  it does not affect the check, which covers only the packages in scope.
- Two packages released from the same commit: each release is independent and neither blocks the
  other.
- A dependency between packages (`Forestry.Deserialize.Xml` → `Forestry.Xml`) whose dependency has
  no release yet: the dependent release is refused.
- The shared runners are noisy: timings vary run to run, so the performance gate must not fail on
  a timing alone (gates like the memory-growth test fit; time-based ones do not).
- A pull request from a fork: it is checked but has no access to any publish rights or secrets (S0).
- A known-vulnerable package version in the dependency graph: the restore fails (S1).
- The pinned SDK is updated: the change is checked like any other (C6).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The pipeline MUST check every pull request to `main` and every push to `main`.
- **FR-002**: The check MUST set up the .NET SDK pinned in the repository, compile every package in
  scope with warnings as errors, and run its tests on every supported target framework (R8).
- **FR-003**: The check MUST show its result on the pull request and name the failing step, test and
  target framework.
- **FR-004**: Each step of the check MUST be reproducible locally with a command documented in
  `doc/dev/` (C3, D0).
- **FR-005**: The check MUST pack every package in scope with symbols and source links, from a
  deterministic build, and keep the packages downloadable from the run (C4).
- **FR-006**: The check MUST run the packaging gate (C2): XML docs on every public member, tests for
  each public option's default, an override and its rejected values, and the API compared with the
  package's last release once one exists.
- **FR-007**: The performance gate MUST run as a job of its own and fail only on gates robust to
  noisy runners (e.g. memory growth), not on timings.
- **FR-008**: Each package MUST carry its own version (SemVer 2.0), and a release MUST be refused
  unless the package's `CHANGELOG.md` has an entry for that version (C1).
- **FR-009**: A release MUST publish one package only, from a commit on `main`, by the pipeline only,
  with its `CHANGELOG.md` entry as release notes (C0).
- **FR-010**: A release MUST be refused if that version already exists on the target feed, or if a
  package it depends on has no matching release on the same feed.
- **FR-011**: The first feed MUST be a preview feed anyone can install from without signing in or
  creating a token, by following the package README (developer, 2026-11-07).
- **FR-012**: Publishing MUST hold no long-lived secret in the repository; publish rights are scoped
  to the release job, and fork pull requests get none (S0).
- **FR-013**: Third-party pipeline steps MUST be pinned to a commit, the restore MUST fail on known
  vulnerable packages, and dependency updates MUST arrive as reviewed pull requests (S1).
- **FR-014**: `doc/dev/` MUST describe how to build, test, version and release, and the Azure DevOps
  leftovers in `eng/` and `doc/dev/` MUST be kept, reshaped or removed per the inventory decided in
  research (D0).
- **FR-015**: The packages in scope are the walking-skeleton packages `Forestry.Xml`,
  `Forestry.Deserialize` and `Forestry.Deserialize.Xml` (developer, 2026-11-07). Other packages
  (`Forestry.Raindrop`, `Forestry.Turn`, `Forestry.StanForD`, `Forestry.PapiNet`) are added when
  their silo is ready, each by adding it to the scope, not by changing the pipeline.
  `Forestry.Raindrop` and `Forestry.Turn` are also checked (build and test, not packed or released),
  because they share the central package versions (developer, 2026-11-09, R7).

### Key Entities

- **Package**: A library in `sdk/` packed for NuGet, with its own version, `CHANGELOG.md` and README.
- **Check**: One pipeline run on a pull request or on `main`: setup, compile, test, pack, gates.
- **Release**: One package version published to a feed, with its release notes.
- **Feed**: Where a reviewer installs from: the preview feed in this spec, nuget.org in a later one.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every pull request to `main` shows a check result without the developer doing anything,
  in under 10 minutes for the packages in scope.
- **SC-002**: A failing check names its cause (step, test, target framework) so the developer can
  reproduce it locally with one documented command, first time.
- **SC-003**: A reviewer who has never built the repository installs a released preview package and
  runs it in under 10 minutes by following its README alone.
- **SC-004**: No package reaches any feed except through a release run; the repository holds no
  publish secret.
- **SC-005**: Releasing one package changes no other package's version.

## Assumptions

- The repository is public on GitHub (`ForestryAI/forestry-sdk-for-net`); the pipeline's host is
  decided in research (GitHub Actions is the natural candidate).
- Where versions come from (by hand next to the `CHANGELOG.md`, or from git tags), whether pre-1.0
  versions are `1.0.0-beta.N` or `0.x`, which preview feed, and what runs on pull requests vs `main`
  vs a release are decided in research with the developer, with a recommendation each.
- The existing `eng/`, `doc/dev/Pipelines.md`, root `Directory.Build.props` / `.targets`,
  `global.json`, `NuGet.Config` and every `.csproj`'s `<Version>1.0.0-beta.1</Version>` and
  misspelled, unused `<ComptableVersion>` are an inventory, not a design basis; each piece is kept,
  reshaped or removed in research.
- The packaging gate's usage tests for public options are written in each package's own silo (they
  test package code); this spec makes the pipeline run and require them.
- Publishing to nuget.org behind the developer's manual approval is a later spec, once the preview
  packages have had feedback (developer, 2026-11-07); this spec ends at the preview feed.
- A "rows to Azure Blob" sink library comes later and is not in scope.
