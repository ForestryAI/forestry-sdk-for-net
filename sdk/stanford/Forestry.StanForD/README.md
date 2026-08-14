# Forestry StanForD

C# types for [StanForD](https://www.skogforsk.se/) — the data format standard published by
Skogforsk for forest machine data exchange (harvesters, forwarders). Skogforsk publish several
StanForD document families; this package's `Metrics`/common types are shared building blocks
those families are built from.

For how the package is put together — how much of a given type is generated versus written by
hand, and what's verified against the real schema versus assumed — see
[ARCHITECTURE.md](ARCHITECTURE.md).

## Scope

Two things worth being explicit about:

- **This covers StanForD's XML format only.** An older, non-XML StanForD format is still in
  circulation for some machines/workflows; it is out of scope for this package entirely.
- **The document-level libraries don't exist yet.** StanForD's XML format spans several distinct
  document families — Harvesting, Forwarding, Quality, and Production Instructions — each with its
  own schema built on the shared common definitions. Only the common definitions and a small
  number of hand-picked example types (`MachineType`) exist here so far; none of the four
  document-family libraries have been started.

## Status

Very early. `schemas/` holds the real Skogforsk-published common-definitions schema across 11
versions (`V1p0` through `V4p1`); `src/` has exactly two real types (`MachineType`,
`Metrics/DiameterUnitType`) built against it, plus a shared `ValidationExtensions.Validate()`
helper for catching required-but-missing data that serialization alone won't. See
ARCHITECTURE.md's Status section for the precise, current list.
