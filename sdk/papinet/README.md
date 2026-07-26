# PapiNet

[PapiNet](https://www.papinet.org) defines a standard for communicating business agreements
between parties. [PapiNet](https://github.com/papinet) offers no official .NET support, only
YAML HTTP guidelines, so this package provides the missing typed .NET model.

## Status

Not started beyond an initial `Contract` scaffold (`ContractType`, `ContractHeader`) — no
serialization, validation, or PapiNet document mapping yet. Likely to build on
[Forestry.Deserialize](../deserialize/Forestry.Deserialize/README.md) once that package's
XML reading is stable, rather than duplicating streaming/parsing logic here.
