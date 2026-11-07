# Engineering

## Why `eng/` exists

Many teams keep one application per repository, each with its own build and pipeline. That works
for one application, but shared code then gets copied between repositories or hidden inside one
application, and drifts.

This repository is an SDK instead: small libraries (a reader, a deserializer, clients and services
later) that an application puts together like building blocks, taking only what it needs. The
layout follows SDK monorepos such as the Azure SDK for .NET (`sdk/<area>/<package>`, one
`CHANGELOG.md` per package, a shared `eng/`).

For the blocks to fit together, every package has to be built, tested, versioned and published the
same way. `eng/` is where that shared way lives, once, so:

- a package holds only its own code and what is unique to it;
- a new package gets the same builds, checks and release steps without copying anything;
- each package still releases on its own (constitution C1), so an application takes a new
  version of one block without the others changing.

## Who owns what

Where each CI/CD purpose is decided, layer by layer. This page is a map only: how to run each step is
in [`doc/dev/`](../doc/dev/), and the rules are in this silo's constitution
([`.specify/memory/constitution.md`](.specify/memory/constitution.md)).

**Rule**: a package states only what is unique to it; the root and `eng/` hold the defaults every
package inherits; `.github/` only runs the commands a developer runs locally (C3); `doc/dev/`
explains them (D0).

> Draft from spec 001 ([research](specs/001-ci-cd/research.md)). Entries marked *planned* do not
> exist yet.

| Purpose | Package (`sdk/<pkg>/`) | Root | `eng/` | `.github/` |
|---|---|---|---|---|
| SDK version | – | `global.json` | – | `actions/setup` reads `global.json` (*planned*) |
| Restore, feeds, package versions | `PackageReference` without a version | `NuGet.Config` | `packaging/Directory.Packages.props` (central versions, CPM guard) | `actions/setup` NuGet cache (*planned*) |
| Compile defaults (frameworks, warnings as errors, XML docs) | `Directory.Build.props` for package-only settings | `Directory.Build.props` (paths, `artifacts/`) | `Directory.Build.Common.props` | – |
| What CI builds | the package's own `.slnx` | – | – | `workflows/ci.yml` matrix: one job per package (*planned*, R7) |
| Test, coverage | test `.csproj`, `*.Tests.runsettings` | `Directory.Build.targets` (test traceability) | – | `workflows/build-test.yml` (*planned*) |
| Performance gate | `Performance.runsettings`, tests with `Category=Performance` | – | – | `workflows/performance.yml` (*planned*) |
| Version, release notes | `<Version>` in the `.csproj`, `CHANGELOG.md` | – | – | `workflows/release.yml` checks both, after `build-test.yml` (*planned*) |
| Pack, symbols, SourceLink, package validation | package `README.md` | – | `Directory.Build.Common.props` / `.targets` | `workflows/pack.yml` (*planned*) |
| Publish (preview feed) | – | – | – | `workflows/release.yml`: tag and GitHub release (*planned*) |
| Dependency updates | – | – | – | `dependabot.yml` (*planned*) |
| How-to for each purpose | – | `doc/dev/` | – | – |

The pipeline stages that lived in `eng/pipelines/` under Azure DevOps (setup, compile, test,
performance, pack, publish) become one file per phase in `.github/workflows/`, the GitHub equivalent
of Azure's job templates: `ci.yml` only decides when and for which packages, and calls the phases
(`build-test.yml`, `performance.yml`, `pack.yml`) as reusable workflows; `release.yml` calls the same
`build-test.yml`. Steps every phase repeats (setup) are one composite action in
`.github/actions/setup/`, the equivalent of Azure's step templates. Reusable workflows cannot sit in
subfolders of `.github/workflows/`. `.github/` and `eng/` are committed; only `eng/.claude/` (the local spec-kit skills) is
kept out of git, as in the package silos.

## Also here

- [`packaging/`](packaging/README.md): central package management.
- [`.specify/`](.specify/) and [`specs/`](specs/): this silo's spec-kit.
