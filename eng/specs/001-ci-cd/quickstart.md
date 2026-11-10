# Quickstart: validating CI/CD for the SDK packages

Runs that prove each story works end to end. Workflow details are in
[contracts/](contracts/); entities in [data-model.md](data-model.md).

## Prerequisites

- .NET SDK matching `global.json` (`dotnet --version`), PowerShell or bash, `gh` signed in (for
  releases only).
- Run everything from the repository root.

## Locally, before pushing (C3)

Per package, with its own solution (here `Forestry.Xml`; the same for each package in `ci.yml`'s
matrix):

```bash
dotnet restore sdk/xml/Forestry.Xml/Forestry.Xml.slnx
dotnet build sdk/xml/Forestry.Xml/Forestry.Xml.slnx --no-restore
dotnet test sdk/xml/Forestry.Xml/Forestry.Xml.slnx --no-build
dotnet test sdk/xml/Forestry.Xml/test/Forestry.Xml.Tests.csproj --no-build -s sdk/xml/Forestry.Xml/test/Performance.runsettings
dotnet pack sdk/xml/Forestry.Xml/Forestry.Xml.slnx -c Release -p:VersionSuffixCi=0
```

Expected: no warnings, all tests pass (tests in Debug, packing in Release, P11), `artifacts/packages/Release/` holds a `.nupkg` and `.snupkg`
for the package, versioned `<Version>.ci.0`.

## US1 Every change is checked

1. Open a pull request with a harmless change: `ci.yml` runs one job per package, all green, under
   10 minutes (SC-001).
   A failing test in one package turns only that package's job red.
2. Push a commit that fails one test: the check is red and the job summary names the test and
   framework (Story 1, scenario 2).
3. Push a commit that adds a compiler warning: the build step is red (scenario 3).
4. Revert both; after merge, the check runs on `main` and its badge is green (scenario 4).
5. Run the local command for the failing step: the same failure reproduces (scenario 5).
6. After the first Dependabot run, its pull requests touch `eng/packaging/Directory.Packages.props`
   and `Directory.Support.Packages.props` (research, Dependabot note 1).

## US2 Pack and gate

1. Download the `packages` artifact of a pull request run; open a `.nupkg`: version has the CI
   suffix, metadata and README present, `.snupkg` alongside (Story 2, scenarios 1 and 3).
2. Remove a doc comment from a public member: the build fails naming the member (scenario 2).
3. Check each option row in `contracts/packaging-gate.md` has its three tests.

## US3 Release

1. Date the `CHANGELOG.md` heading of `Forestry.Xml` and merge to `main`.
2. Run `release.yml` with `package = Forestry.Xml`, `dry-run = true`: green, packages as artifacts,
   no tag (C6).
3. Run again with `dry-run = false`: tag `Forestry.Xml/v<Version>` and a prerelease GitHub release
   with both packages and the CHANGELOG section (Story 3, scenario 1).
4. Run it once more: refused, version exists (scenario 3).
5. Run for a package whose heading says `Unreleased`: refused (scenario 2).
6. Reviewer check on another machine (SC-003):

   ```bash
   mkdir forestry-feed
   gh release download Forestry.Xml/v<Version> -R ForestryAI/forestry-sdk-for-net -p "*.nupkg" -D forestry-feed
   dotnet new console -o try-forestry
   dotnet nuget add source "$PWD/forestry-feed" -n forestry-preview
   dotnet add try-forestry package Forestry.Xml --prerelease
   ```

   Without `gh`, the `.nupkg` is downloaded from the release page in a browser; no sign-in needed.
   Expected: the package restores and a small read loop runs on net10.0.

## Results (2026-11-10)

| Step | Result |
|---|---|
| US1 1 | Six jobs green in under a minute (#76); eleven since pack (#84), all pull request runs under 10 minutes once started |
| US1 2 | A throwaway failing test turned only `build-test (Forestry.Deserialize)` red, naming the test (#76) |
| US1 3 | A throwaway compiler warning failed the Build step of Forestry.Xml and Forestry.Deserialize.Xml (#76) |
| US1 4 | CI green on `main` after each merge |
| US1 5 | The failing test reproduced locally: 1 failed, 27 passed (#76) |
| US1 6 | The first run found no projects; fixed with `/sdk/*/*` (#77). The rerun opened #78-#81, changing both files in `eng/packaging/` |
| US2 1 | Packages from a run (#84): CI suffix, README, metadata, `.snupkg`, SourceLink with `/_/` |
| US2 2 | A throwaway public class without XML docs failed the build with CS1591, naming the member (#84) |
| US2 3 | Every row in `contracts/packaging-gate.md` has its tests |
| US3 1-3 | `Forestry.Xml` dated (#86), dry run 42783211307, real run 42783580530: tag `Forestry.Xml/v1.0.0-beta.1` and a prerelease with both packages and the notes |
| US3 4 | Dry run for the same version (42784459229): refused, "Forestry.Xml 1.0.0-beta.1 is already released" |
| US3 5 | Dry run for `Forestry.Deserialize` with an `Unreleased` heading (42784463317): refused, FSDK00003 |
| US3 6 | Clean folder, no sign-in: installed and read a document on net10.0 (the time for a reviewer new to the API is not measured) |

Not exercised yet: the comparison with a last release (the first release has no predecessor; it runs on the
next one) and the dependency refusal for a package that has dependencies.
