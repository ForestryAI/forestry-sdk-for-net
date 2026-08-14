# ARCHITECTURE.md — Forestry StanForD

## 1. Purpose

**Forestry.StanForD** provides C# types matching StanForD's XML format, published by
[Skogforsk](https://www.skogforsk.se/) for exchanging forest machine data. It is deliberately
**not** wired into `Forestry.Deserialize`/`Forestry.Deserialize.Xml` yet (see §3) — this package's
only job right now is: given a real Skogforsk schema, produce a correct, well-documented C#
representation of it.

## 2. High-Level Architecture

**Type construction is a mixture of Claude-assisted generation and human judgment — not a
mechanical schema compiler.** The real Skogforsk XSDs (`schemas/`) are structurally rich enough
that large parts of a type (property names, nullability from `minOccurs`, doc comments from the
schema's own `<doc:Description>` annotations) translate mechanically and reliably. But real
decisions with no XSD equivalent at all still need a human call — most concretely, **namespace
organization**: XSD is flat, so grouping `DiameterUnitType` under a `Metrics` namespace (rather
than leaving every type in one flat `Forestry.StanForD` namespace) is a human/domain decision
layered on top of the schema, not something derived from it. Expect this pattern to repeat as more
types are added — mechanical translation for structure, explicit human decisions for organization,
naming, and anything the schema's `xsd:extension` inheritance chains imply but don't dictate a C#
shape for.

**Required-ness is enforced by an explicit validation pass, not by C# language features alone.**
Verified directly (not assumed): `System.Xml.Serialization.XmlSerializer` ignores the C# `required`
modifier entirely — it constructs via reflection and sets properties through their public setters,
which never runs the compiler's "were all required members set" check. Deserializing
`<Machine></Machine>` into a `required string MachineKey` property produces `MachineKey = null`
with no error at all. So every type here pairs `required` (correct signal for C# callers
constructing the type directly) with `[Required]` from `System.ComponentModel.DataAnnotations`,
and callers who deserialize via `XmlSerializer` are expected to call the shared
`object.Validate()` extension (`src/ValidationExtensions.cs`) afterward to actually catch missing
required data - `XmlSerializer` alone won't.

**The schema is not uniformly backward-compatible across its whole version history - verified, not
assumed.** `schemas/` holds 11 versions of the common-definitions schema (`V1p0` through `V4p1`).
Comparing every consecutive pair by type name: `V4p0`→`V4p1` (the pair that matters right now,
since types here are built against `V4p1`, the latest) is confirmed purely additive - 7 new types,
nothing renamed or removed. But that does **not** generalize across the full history: `V3p6`→`V4p0`
removed 13 types, and three earlier version bumps (`V1p0`→`V2p0`, `V2p0`→`V2p1`, `V2p1`→`V3p0`)
each removed at least one. Do not assume a type built against an older schema version is still
valid against `V4p1` without checking - it usually is, but not always.

## 3. Module Boundaries

- **`Forestry.StanForD`** — this package. Currently depends on nothing but the BCL
  (`System.Xml.Serialization`, `System.ComponentModel.DataAnnotations`).
- **`Forestry.Deserialize`/`Forestry.Deserialize.Xml`** — **not referenced, deliberately, for now.**
  These types use `[XmlType]`/`[XmlEnum]` (`System.Xml.Serialization`'s attribute system), which is
  a different, non-interoperable system from `Forestry.Deserialize`'s own `[Element]`/`[Collection]`
  attributes. Whether `Forestry.StanForD` eventually becomes the real schema types
  `Forestry.Deserialize.Xml` reads against (requiring `[Element]`/`[Collection]` instead, or in
  addition) is an open question, not yet decided - see §5.

**Stability:** Very early / experimental.
**Dependencies:** None beyond the BCL.

## 4. Core Contracts

### 4.1 `MachineType` (`src/MachineType.cs`)

`[XmlType("Machine")]`. One real property so far: `MachineKey` (`required string`, `[Required]`)
— per the schema fragment reviewed, an element with no `minOccurs` shown, which per XSD's default
(`minOccurs="1"` when omitted) means required unless a not-yet-seen surrounding context says
otherwise. Doc comment carries the schema's own `<doc:Description>` text forward rather than
dropping it, since it explains a real distinction (`MachineKey` vs. `MachineUserId`/
`MachineIdOwner`) not obvious from the property name alone.

### 4.2 `DiameterUnitType` (`src/Metrics/DiameterUnitType.cs`)

`[XmlType("DiameterUnit")]` enum, `Metrics` namespace. One member so far (`Mm` / `"mm"`) — the
first real example of the "mechanical enum from `xsd:simpleType` + `xsd:enumeration`" pattern
described in §2, not yet expanded to the schema's full enumeration set.

### 4.3 `ValidationExtensions` (`src/ValidationExtensions.cs`)

`object.Validate()` — wraps `Validator.TryValidateObject(instance, context,
validateAllProperties: true)`, collecting every failing `[Required]`/DataAnnotation into one
`ValidationException` rather than stopping at the first. The explicit step described in §2 that
actually enforces required-ness after `XmlSerializer` (or any other reflection-based construction,
including a future `Forestry.Deserialize.Xml` path, if one is ever added - see §3) has already run.

## 5. Status / POC Debt

- **Only two real types exist** (`MachineType`, `DiameterUnitType`) against a schema with 178
  complex types and 87 simple types (`V4p1`). Everything else is unbuilt.
- **None of the four document-family libraries (Harvesting, Forwarding, Quality, Production
  Instructions) have been started** - only the shared common-definitions schema is in `schemas/`
  today. `MachineType`/`MachineKey` itself isn't defined in that schema at all (confirmed by
  search - it only appears in other elements' documentation text); its real schema (presumably a
  Harvesting-document-level one) hasn't been added to `schemas/` yet.
- **The `[XmlType]` vs. `Forestry.Deserialize`'s `[Element]`/`[Collection]` question (§3) is still
  open.** Whichever way it resolves changes what "generate a type from the schema" produces for
  everything built after this point.
- **`xsd:extension` inheritance chains** (75 of them in `V4p1` alone) haven't been addressed by
  any real type yet - whether they become C# class inheritance, composition, or something else is
  a per-family decision still to make (§2).
- **Schema version scope**: types here target `V4p1` specifically. Whether older real-world
  documents (produced against earlier schema versions) need to be readable too - and if so,
  whether that means tolerating multiple schema generations rather than just the latest - isn't
  decided.

## 6. Anti-Goals

- Does not cover StanForD's older, non-XML format - out of scope entirely, not deferred.
- Does not assume schema backward-compatibility beyond what's actually been verified (§2) - a
  type built against an older common-definitions version should be re-checked against `V4p1`,
  not assumed compatible.
- Does not attempt to be a general XSD-to-C# compiler - see §2; the mechanical parts of type
  generation are a starting draft, not a finished, review-free pipeline.

## 7. Related Work Items

- Feature #105 — Chat POC external
  - User Story #106 — Deserialize (the `Forestry.Deserialize.Xml` work this package may eventually
    feed into, per §3's open question)
