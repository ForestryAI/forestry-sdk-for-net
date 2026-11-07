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
dotnet build sdk/xml/Forestry.Xml/Forestry.Xml.slnx -c Release --no-restore
dotnet test sdk/xml/Forestry.Xml/Forestry.Xml.slnx -c Release --no-build
dotnet pack sdk/xml/Forestry.Xml/Forestry.Xml.slnx -c Release -p:VersionSuffixCi=0
```

Expected: no warnings, all tests pass, `artifacts/packages/Release/` holds a `.nupkg` and `.snupkg`
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
