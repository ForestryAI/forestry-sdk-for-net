# Implementation Plan: Deserialization context

**Branch**: `deserialize-002-context` (the setup script reports `002-reading-context`) | **Date**:
2026-11-12 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-reading-context/spec.md` (clarified 2026-11-12,
5 questions), GitHub issue ForestryAI/forestry-sdk-for-net#99, and spec 001's research (R2, R7,
R13, R19), which this plan builds on.

## Summary

Give each enumeration a public **deserialization context** that replaces 001's `DeserializationState`: the
open-row stack as today, plus rows policies keep, and per row its parent id,
set-members bitmap and the actions policies took (R20-R22). Completion becomes two independent
decisions taken as actions, each at most once, with containment defaults (R23, R24). Rows both
enumerated and kept are handed out as shallow copies (R25). Policies receive the context as a
parameter; options with enumeration policies still serve one enumeration at a time (R26). The XML media adds
attributes and element text as members, declared with its own attributes (R30); empty nodes are rows like any other
(R29, R30). A limit on rows in the context fails fast with an explanatory exception (R28).

All design decisions are in [research.md](research.md), each Proposed until the developer decides
(C5).

## Technical Context

**Language/Version**: C# on .NET 10 (`RequiredTargetFrameworks`, A0, constitution 3.0.0)

**Primary Dependencies**: None beyond the base class library (A1); `Forestry.Xml` (sdk/xml) for the
XML media, which already gives attribute tokens (R30)

**Storage**: N/A (rows are yielded; the context is in memory per enumeration)

**Testing**: xUnit. Shredding scenarios in `Forestry.Deserialize.Xml.Tests` over small hand-written
documents (C3, A4); core-only checks in `Forestry.Deserialize.Tests`; memory (SC-005) under the
`Category=Performance` trait, run with `Performance.runsettings` at each story's gate (R31). Test
shells before code (C4).

**Target Platform**: Any .NET 10 runtime (library)

**Project Type**: Library (public SDK package) and its XML media package

**Performance Goals**: None in this slice (C6). The common case, rows enumerated and not kept, must
cost no more than today: no copy, no list insertion (R20, R25).

**Constraints**: Single forward pass (C2); memory grows only with open and kept rows (C1, SC-005;
parents by id, R21); no reflection per row; options read-only once used; core references no media
(C0); `TreatWarningsAsErrors` SDK-wide.

**Scale/Scope**: Three stories (US1 context and isolation, US2 containment, US3 named and own
values), 23 functional requirements; replaces 001's `Enumerate` policy method and keeps its
one-enumeration-at-a-time guard.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Checked against constitution 3.0.0.

| Rule | Pre-research | Post-design | Notes |
|---|---|---|---|
| C0 Media-agnostic | Pass | Pass | The context and actions are core and media-neutral; where a value comes from (attributes, text) is declared with the XML package's own attributes, not the core's (R29, R30). |
| C1 Partial deserialization | Pass | Pass | Not-kept rows leave at completion; parents held by id so a kept row never holds a left parent (R21); limit on rows held (R28); SC-005 test. |
| C2 Single pass | Pass | Pass | Nesting and relationships in one pass; no forward references (spec Assumptions). |
| C3 Real media reader | Pass | Pass | Scenarios through the XML reader; core tests need no document (R31). |
| C4 Test-first | Pass | Pass | Shells per story before code; test review task on the board (tasks.md). |
| C5 Design ownership | Pass | Pass | Every R20-R31 item Proposed; tasks wait for decisions. |
| C6 Working example first | Pass | Pass | No pooling or indexed store; list plus LINQ (spec Assumptions). |
| D0 Public documentation | Pass | Pass | New public types (`DeserializationContext`, `ContextRow`, `Lifetime`, the limit option, the XML package's source attributes) get XML docs; a how-to page on the deserialization context. |
| S0 Developer-chosen types | Pass | Pass | Unchanged: only added row types are constructed. |
| S1 Fail fast | **Deviation (001)** | **Partly met** | Context limit added (R28); actions taken twice throw (R23). Other 001 limits (depth, value length) still postponed as in 001. |
| A0 / A1 / A3 / A4 | Pass | Pass | net10, no third-party NuGet, shared build, A4 test layout. |
| G0-G2 | Pass | Pass | Spec, plan and tasks are the truth. |

Gate result: **passes**, carrying 001's justified S1 deviation for limits other than the context's.

## Project Structure

### Documentation (this feature)

```text
specs/002-reading-context/
├── plan.md              # This file
├── research.md          # Phase 0: R20-R31
├── data-model.md        # Phase 1: context, context row, set members, actions, lifetime
├── quickstart.md        # Phase 1: validation scenarios
├── contracts/
│   └── public-api.md    # Phase 1: public surface changes
├── checklists/
│   └── requirements.md  # From /speckit-specify and clarify
└── tasks.md             # /speckit-tasks (not created here)
```

### Source Code (repository root: `sdk/deserialize`)

```text
Forestry.Deserialize/
├── src/
│   ├── Reading/
│   │   ├── DeserializationContext.cs  # new, replaces DeserializationState.cs (R20, R26)
│   │   ├── ContextRow.cs            # new (R21, R22, R23)
│   │   ├── ContainerRelationship.cs # new (R21)
│   │   ├── Lifetime.cs              # new (R23)
│   │   ├── RowBuilder.cs            # completion steps, copies, limit (R24, R25, R28)
│   │   └── MediaRow*Enumerator.cs   # Clear(context) at the end
│   ├── Policies/
│   │   ├── EnumerationPolicy.cs     # context parameter; Complete replaces Enumerate (R23, R26)
│   │   ├── PolicyResolver.cs, TypeChains.cs  # compose Complete
│   ├── Definitions/TypeDefinition.cs   # member index by name (R22)
│   ├── DeserializeOptions*.cs       # limit option (R28)
│   └── Rows/Row.cs                  # internal shallow copy (R25)
└── test/                            # limit option, member index lookup

Forestry.Deserialize.Xml/
├── src/
│   ├── XmlRowReading.cs             # attributes, own text (R30)
│   ├── XmlAssociation.cs            # attribute and text members, per-source duplicate checks (R30)
│   ├── XmlAttributeValueAttribute.cs, XmlTextValueAttribute.cs  # new (R30; names Deferred)
│   └── DeserializeXmlOptions.cs     # TextValue<TRow>(memberName) (R30)
└── test/
    ├── DeserializationContextTests.US1.cs   # US1 + isolation + limit
    ├── DeserializationContextTests.US2.cs   # containment
    ├── DeserializationContextTests.US3.cs   # named and own values
    └── Fixtures/                    # Site, Contact, Order, Line, Price, Reference
```

**Structure Decision**: The same two library and test project pairs as 001; no new project.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| S1 limits other than the context's (depth, value length) still not implemented | Carried from 001; this spec adds only the limit its own feature introduces | Adding all limits here enlarges the slice beyond the context (C6 spirit) |
