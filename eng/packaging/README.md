# Packaging (CPM)
Here are the NuGet package versions for the ```forestry-sdk-for-net``` repository.  Every library resolves package referenced versions from engineering not by using ```Version``` attributes.

## Library overrides

## Guards
CPM is enforced at build time and pipeline analysis.

### Build errors
#### FSDK00001 - CPM override enabled

#### FSDK00002 - No CHANGELOG heading for the version
A release (`dotnet pack -p:Release=true`) needs the heading `## <Version> (yyyy-MM-dd)` in the package's `CHANGELOG.md`.

#### FSDK00003 - CHANGELOG heading has no release date
The heading for the version still says `(Unreleased)`: replace it with the release date.
