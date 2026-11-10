# Forestry SDK for .NET - Building

# Projects
Every library requires a ```.csproj``` to spell out what the library does and any specific requirements.  The ```Directory.Build.props``` in the library are read before and ```Directory.Build.targets``` after the ```.csproj```.

## Properties
When MSBuild runs ```Microsoft.Common.props``` it hunts for user defined properties.  Every library delegates default property settings defined by ```Directory.Build.props``` files to others in the following order:

1. Root directory
2. Root engineering directory (suffix Commons)

That MSBuild grabs **properties** is misleading because the properties are not just settings rather even drive how profiling, packaging and artifacts are implemented.

### Root
The root ```Directory.Build.props``` has smart tricks:
- paths that point out engineering and only Forestry libraries
- default profiles e.g. Debug, any CPU and platform
- singular artifacts spot rather than per library
- packaging (central package management CPM)

#### Artifacts
Artifacts pumped to the ```artifacts``` directory ignored by Git speeds up incremental builds in a big repository:
- eliminates output collisions by putting output under a library directory then different Net frameworks (i.e. deterministic paths)
- better caching where deterministic paths ease marks unchanged input and thereby skip compilation i.e. unnecessary rebuilds

With all outputs (i.e. ```bin```) in a predictable place build scripts can reference outputs directly so tools can without re-evaluating a project:
- run tests
- NuGet packing
- run compatibility checks

Faster Git operations are possible in a large repository by not having to ignore ```bin``` and ```object``` per project instead only the ```artifacts``` directory.  Same idea means easy clean by just wiping out ```artifacts```.

Pipelines can expect just one artifact station for build, artifact collections, and publishing makes caching, uploading, and simplier jobs a bonus.

#### Packaging
After delegating to engineering the packaging (CPM) is grabbed ```eng\packaging\Directory.Packages.props``` instructs Nuget (see [documentation](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)).  The package properties segregates Nuget packages for testing, code coverage etc.

### Engineering
The engineering  ```Directory.Build.Common.props``` sets these generic categories for all libraries:
- kind e.g. client or test
- defaults e.g. lastest language version
- code analysis
- metadata e.g. company == Forestry
- support Net frameworks
- references internal SDK library dependencies 

## Build
MSBuild has a ```Microsoft.Common.targets``` that aims to import ```Directory.Build.targets``` early in the **build** process.  Most libraries don't have their own build settings rather delegation starts at the root then balls over to engineering.

### Root
Currently the root build doesn't do much than track where test assemblies originate from using reflection helping:
- CI pipelines trace
- debugging and diagnostics
- tooling that consumes metadata

### Engineering
Here build guidelines are enforced: central package management may not be disabled (FSDK00001, see [eng/packaging/README.md](../../eng/packaging/README.md)).

#### Package version
Every package has its own version, by hand in its `.csproj` (`<Version>`), next to its `CHANGELOG.md`
(eng spec 001, R2):
```
MAJOR.MINOR.PATCH-PRERELEASE
```
Packing settings (symbols, SourceLink, repository and README metadata, XML docs) are shared in
`eng/Directory.Build.Common.props` and apply to a library that sets `IsShippingLibrary` in its own
`Directory.Build.props`; anything else is not packable.

While APIs still move, versions are `1.0.0-beta.N` (R3). Each release has a `CHANGELOG.md` entry for
its version; releasing is described in [Pipelines.md](Pipelines.md).

## Pipelines
CI runs on GitHub Actions: see [Pipelines.md](Pipelines.md) for what runs when and the local command
behind every step, and [eng/README.md](../../eng/README.md) for which layer owns each CI/CD purpose.
