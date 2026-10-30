# Implementation Plan: Walking skeleton

**Branch**: `spec-kit-startup` (the setup script reports `001-walking-skeleton`; no git extension, so no
branch was created) | **Date**: 2026-10-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-walking-skeleton/spec.md`, plan input from GitHub
issue ForestryAI/forestry-sdk-for-net#51 ("Plan phase input"), and the design discussions of
2026-10-26 to 2026-10-29 (constitution 2.0.0).

## Summary

Deserialize a document into rows of one flat class extending `Row`, chosen through the options
(one row per occurrence), with policies in two core pipelines. The media's row enumerator drives
its own reader and hands C# types and members to the core: `Pipeline<T>()` constructs and pushes a
row, `Pipeline(member, utf8, context)` converts and sets a member, `Pipeline()` completes the top
row on the media's end token and hands it out (R2, R6, R7). The core is driven: it owns the entry
points, the options and definitions, the base enumerators (`RowEnumerator`, `RowAsyncEnumerator`),
the pipelines, converters and `Row`. In this slice the media is `Forestry.Deserialize.Xml`; there
is no fake media (C3, R11).

All design decisions are in [research.md](research.md): every item is Confirmed, Rejected or
Deferred (C5).

## Technical Context

**Language/Version**: C# on .NET 10 and .NET 9 (`RequiredTargetFrameworks`, A0)

**Primary Dependencies**: None beyond the .NET base class library (A1); `System.IO.Pipelines` for
async buffering (already used by `PipeReaderBuffering`)

**Storage**: N/A (rows are yielded to the developer; storage is downstream)

**Testing**: xUnit. Shredding scenarios in `Forestry.Deserialize.Xml.Tests` with small hand-written
XML documents through the real XML reader (C3, R11). `Forestry.Deserialize.Tests` for what needs no
document: definitions, constructor choice, converters, policy precedence, read-only options,
`Row.AsType<T>()`. Test shells before code (C4)

**Target Platform**: Any .NET 9/10 runtime (library)

**Project Type**: Library (public SDK package) plus its first media package

**Performance Goals**: None in this slice (C6: working example first). Design must not prevent flat
memory (C1, SC-003).

**Constraints**: Single forward pass (C2); the deserialization package and its tests reference no
media package, the media package references it (C0); no reflection per row (delegates built once,
R5); options read-only after first use (R6); `TreatWarningsAsErrors` is on SDK-wide.

**Dependency**: The XML reader's element and text reading (`Forestry.Deserialize.Xml/BLOCKING.md`,
phase 2) is open; acceptance tests cannot pass before it is done.

**Scale/Scope**: One flat class with a parameterless constructor, built-in member types, both
pipelines in their minimal slot order with built-in defaults plus a test policy (R7).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Checked against constitution 2.0.0 (approved, not yet merged to `main`). The R-items behind each
note are in research.md.

| Rule | Result (pre-research) | Result (post-design) | Notes |
|---|---|---|---|
| C0 Media-agnostic | Pass | Pass | No shared token model; the media's seam is its enumerator override, which hands over C# types and members (R1, R2, R6). `ElementAttribute`, `CollectionAttribute` (XML vocabulary in the core) are deleted (R8, R10). |
| C1 Partial deserialization | Pass | Pass | The core keeps no reference to a row once handed out; per-occurrence metadata is opt-in (R4, R7). SC-003 test. |
| C2 Single pass shredding | Pass | Pass | One forward pass; definitions, converters and policies resolved once per options (R6, R7, R14). |
| C3 Real media reader | Pass | Pass | Shredding tests in `Forestry.Deserialize.Xml.Tests`; core tests reference no media (R11). |
| C4 Test-first | Pass | Pass | In every story, test shells come before any code, declarations included; then a test review task on the board (tasks.md, no Foundational phase). |
| C5 Design ownership | Pass | Pass | Every research item Confirmed, Rejected or Deferred by the developer; Deferred names confirmed in code review. |
| C6 Working example first | Pass | Pass | No performance work in this slice. |
| D0 Public documentation | Pass | Pass | Option B now (XML docs on, missing docs as warnings); A once a real StanForD document deserializes (R9). |
| S0 Developer-chosen types | Pass | Pass | Rows only of types added explicitly through the options (and, from spec 002/003, the row types their constructors need); no assembly scan; a per-options row type lookup checks every handover (R5, R6, R12). |
| S1 Fail fast on wrong input | **Deviation** | **Deviation** | Limits postponed (FR-014); ancestor fail-fast comes with the ancestors slice. Justified below. |
| A0 / A1 / A3 | Pass | Pass | net9+net10, no third-party NuGet, shared SDK build. |
| G0-G2 | Pass | Pass | Spec, plan and tasks are the truth; 2.0.0 takes effect when merged. |

Gate result: **passes with one justified deviation (S1)**.

## Project Structure

### Documentation (this feature)

```text
specs/001-walking-skeleton/
├── plan.md              # This file
├── research.md          # Phase 0: R0-R15
├── data-model.md        # Phase 1: options, definitions, row, field context, state, policies
├── quickstart.md        # Phase 1: validation scenarios
├── contracts/
│   └── public-api.md    # Phase 1: public surface of this slice
├── checklists/
│   └── requirements.md  # From /speckit-specify
└── tasks.md             # /speckit-tasks (not created here)
```

### Source Code (repository root: `sdk/deserialize`)

```text
Forestry.Deserialize/                # references no media (C0)
├── src/
│   ├── Deserialization*.cs          # entry points (R3)
│   ├── DeserializeOptions*.cs       # abstract options, row type lookup, read-only after use (R5, R6)
│   ├── Rows/                        # Row, Field (R4)
│   ├── Reading/                     # RowEnumerator, RowAsyncEnumerator, RowState, DeserializationState,
│   │                                # Pipeline calls, FieldContext, PipeReaderBuffering (R2, R15)
│   ├── Definitions/                 # type, PropertyDefinition<T>, parameter definitions (R5, R6)
│   ├── Converters/                  # Converter<T>, factories, built-ins (R14; name Deferred)
│   ├── Policies/                    # two pipelines, slots, defaults, precedence (R7)
│   └── Attributes/                  # [Row], [Field], [RowConstructor] (R5, R8; names Deferred)
└── test/                            # definitions, constructor choice, converters, precedence, read-only options, AsType

Forestry.Deserialize.Xml/            # references Forestry.Deserialize
├── src/
│   ├── Utf8XmlReader ...            # existing reader (element reading: BLOCKING.md phase 2)
│   ├── DeserializeXmlOptions.cs     # extends DeserializeOptions; supplies enumerators, default converters
│   ├── XmlRowEnumerator.cs          # RowEnumerator subclass (R2)
│   ├── XmlRowAsyncEnumerator.cs     # RowAsyncEnumerator subclass (R2)
│   └── XmlRowReading.cs             # shared override logic: tokens -> Pipeline calls, association (R2, R6, R13 A)
└── test/
    └── WalkingSkeleton*Tests.cs     # US1-US4 acceptance tests over small XML documents
```

**Structure Decision**: Two existing library + test project pairs; the reference goes Xml -> core.
Existing classes are **not** a design basis (R0); per-class decisions are in R10. The `.slnx` layout
question (`sdk/deserialize/BLOCKING.md`) is unaffected.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| S1 fail-fast limits not implemented | The spec (FR-014) keeps the walking skeleton to its core behavior; limits come in a later slice, the ancestor case with ancestors | Implementing them now enlarges the skeleton before the core abstractions are confirmed (C6 spirit) |
