# Data Model: CI/CD for the SDK packages

The "data" of this feature is what the pipeline reads and produces. No database; everything lives in
the repository, in a run, or in a GitHub release.

## Package

| Field | Source | Rule |
|---|---|---|
| PackageId | `.csproj` file name (`Forestry.Xml`) | One of the packages in `ci.yml`'s matrix, each with its own `.slnx` (FR-015, R7) |
| Version | `<Version>` in the `.csproj` | SemVer 2.0, `1.0.0-beta.N` while APIs move (R2, R3) |
| Release notes | `CHANGELOG.md` in the package folder (`sdk/<area>/<PackageId>/CHANGELOG.md`) | Section `## <Version> (<yyyy-MM-dd>)`; `Unreleased` instead of a date is refused at release (P3) |
| Dependencies | `ProjectReference`s in the `.csproj` | Each becomes a package dependency at the referenced package's Version (P6) |
| Target frameworks | `eng/Directory.Build.Common.props` | Supported target frameworks (constitution definition; net10.0 today, R8) |

## Check (one `ci.yml` run)

| Field | Meaning |
|---|---|
| Trigger | `pull_request` to `main` or `push` to `main` |
| Run number | GitHub's run number; becomes the CI suffix `.ci.<n>` / `-ci.<n>` (P2) |
| Jobs | `build-test`, `pack`, `performance` (contract `ci-workflow.md`) |
| Outputs | Test results, coverage (Cobertura), packages with CI suffix, as run artifacts |
| Result | Pass only if every job passes; shown on the pull request |

## Release (one `release.yml` run)

| Field | Meaning |
|---|---|
| Inputs | `package` (one PackageId), `dry-run` (bool) |
| Commit | The tip of `main` at the time of the run |
| Tag | `<PackageId>/v<Version>` (P4); must not exist yet |
| Assets | `<PackageId>.<Version>.nupkg`, `<PackageId>.<Version>.snupkg` |
| Notes | The version's `CHANGELOG.md` section |

### State transitions

```text
CHANGELOG entry "Unreleased" ──(developer dates the heading, PR merged to main)──▶ releasable
releasable ──(release.yml, dry-run)──▶ checked, nothing published
releasable ──(release.yml)──▶ released: tag + GitHub release with assets
released ──(same version again)──▶ refused (FR-010)
```

A release is never deleted to be redone: a fix gets a new version (`beta.N+1`).

## Feed (preview)

The repository's GitHub releases. A reviewer downloads a release's `.nupkg` into a folder and adds
that folder as a NuGet source (quickstart, reviewer section). nuget.org comes in a later spec.
