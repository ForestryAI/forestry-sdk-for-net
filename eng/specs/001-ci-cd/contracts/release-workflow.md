# Contract: `.github/workflows/release.yml`

**Trigger**: `workflow_dispatch` only, from `main` (refused on any other ref).
**Inputs**: `package` (choice: `Forestry.Xml`, `Forestry.Deserialize`, `Forestry.Deserialize.Xml`),
`dry-run` (boolean, default `true` until the first release has been tried, C6).
**Permissions**: `contents: read`, except the final release job: `contents: write` (S0). No secrets
besides the workflow's own `GITHUB_TOKEN`.

## Steps

| # | Step | Local command | Refuses when |
|---|---|---|---|
| 1 | Refuse unless on `main` | – | Ref is not `main` |
| 2 | Same checks as `ci.yml` `build-test` for the package and its tests | `dotnet test <package .slnx> -c Release` | Any failure |
| 3 | Pack the package, no suffix, release notes required | `dotnet pack <src .csproj> -c Release -p:ContinuousIntegrationBuild=true -p:Release=true` | No dated `CHANGELOG.md` heading for `<Version>` (P3, FR-008); packaging gate |
| 4 | Version is new | `gh release view <PackageId>/v<Version>` | Release or tag already exists (FR-010) |
| 5 | Dependencies released | `gh release view <Dependency>/v<DependencyVersion>` per dependency in the `.nuspec` | A dependency version has no release (P6, FR-010) |
| 6 | Package validation against the last release, if any (P5) | `dotnet pack … -p:PackageValidationBaselinePath=…` | Breaking API change not intended |
| 7 | **Dry run stops here**: packages uploaded as run artifacts | – | – |
| 8 | Create tag and GitHub release | `gh release create <PackageId>/v<Version> <nupkg> <snupkg> --target <sha> --title … --notes-file <section> --prerelease` | – |

`--prerelease` is set whenever the version has a prerelease label (all `1.0.0-beta.N`, R3).

## Outputs

A tag `<PackageId>/v<Version>` on the run's commit, and a GitHub release with the `.nupkg`, the
`.snupkg` and the `CHANGELOG.md` section as notes (R6.10).
