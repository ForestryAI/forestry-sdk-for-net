# Implementation Plan: Big data orientation

**Branch**: `deserialize-003-big-data-orientation` (the setup script reports
`003-big-data-orientation`) | **Date**: 2026-11-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/003-big-data-orientation/spec.md` (clarified
2026-11-19, 3 questions), GitHub issue ForestryAI/forestry-sdk-for-net#125, constitution 3.1.0.

## Summary

Make flat rows a checked rule and settle the package's words, with no change to which rows come
out:

- **Flat rows (US1)**: when the options lock, every member of a row type must be a single value: a
  primitive, `string`, an enum, or a struct that formats to UTF-8 (`IUtf8SpanFormattable`) and
  parses from text (UTF-8 or chars), possibly nullable (R32). Each property definition checks its member
  (R32), then resolves its own converter, now `ResolvedConverter`; the type definition gathers
  every failure into one message (R33). Spec 001's deferred owned members are rejected (R34).
- **Document names in the format (US2)**:
  - `Field` goes; the property definition holds its converter (R35).
  - `FieldContext` becomes `ReadingInformation` in the top namespace (R36).
  - `[Row]` and `[Field]` become the XML package's `XmlElementName`; `XmlAttributeValue` and
    `XmlTextValue` become `XmlAttributeName` and `XmlElementText`, with an exclusive-source check
    (R37).
- **One vocabulary (US3)**:
  - `Media*` becomes `Format*` (R38) and `Row` moves to the top of `src/` (R39).
  - Docs say "format" and "flatten" (R41).
  - Forestry.StanForD gets the 12 mechanical attribute renames in the same change (R40).

All design decisions are in [research.md](research.md); the developer confirmed every one on
2026-11-19 (C5).

## Technical Context

**Language/Version**: C# on .NET 10 (`RequiredTargetFrameworks`, A0)

**Primary Dependencies**: None beyond the base class library (A1). `IUtf8SpanFormattable`,
`IUtf8SpanParsable<TSelf>` and `ISpanParsable<TSelf>` are BCL interfaces (R32).

**Storage**: N/A

**Testing**: xUnit.
- The flat check is tested in `Forestry.Deserialize.Tests`, since definitions need no document
  (C3).
- The XML attributes are tested in `Forestry.Deserialize.Xml.Tests` over small hand-written
  documents (C3, A4).
- The existing suites of all three packages, Forestry.StanForD's included, are the check for the
  renames (SC-004).
- Test shells come before code (C4), and each story's gate runs `Performance.runsettings` (R42).

**Target Platform**: Any .NET 10 runtime (library)

**Project Type**: Library (public SDK package), its XML format package, and one consumer
(Forestry.StanForD)

**Performance Goals**: No change per row. The flat check runs once per row type when the options
lock (R32, R33).

**Constraints**:
- Every package builds and its tests pass at every commit (R40).
- Single forward pass and partial reading unchanged (C1, C2).
- Core references no format (C0).
- No naming attributes in core (A5).
- `TreatWarningsAsErrors` SDK-wide.

**Scale/Scope**: Three stories and 16 functional requirements (with FR-006a and FR-006b). About
26 source files in the two packages, 12 attribute uses in StanForD, around 10 test fixtures, and
6 documentation files plus the SDK README (R41).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Checked against constitution 3.1.0.

| Rule | Pre-research | Post-design | Notes |
|---|---|---|---|
| C0 Format-agnostic | Pass | Pass | Core keeps no format names. `ReadingInformation` and the `Format*` enumerators are format-neutral (R36, R38). |
| C1 Partial deserialization | Pass | Pass | Unchanged. |
| C2 Single pass flattening | Pass | Pass | Unchanged. The words follow the constitution (R41). |
| C3 Real format reader | Pass | Pass | The flat check needs no document, so it's tested in core. XML attributes are tested through the XML reader (R42). |
| C4 Test-first | Pass | Pass | Shells per story before code; test review task on the board (tasks.md). |
| C5 Design ownership | Pass | Pass | R32-R43 confirmed by the developer 2026-11-19 (R34 as a rejection); several reshaped in review (R32 UTF-8 formatting, R33 property definition, R36 `ReadingInformation`, `Utf8Extensions`). |
| C6 Working example first | Pass | Pass | No performance work; the check runs at lock only. |
| C7 Flat rows | **Not yet met** | Met by design | Implemented by R32 and R33, with SC-001 tests. Today nothing enforces it in core. |
| D0 Public documentation | Pass | Pass | Renamed and new public types keep or get XML docs. The README defines a row (FR-004). |
| S0 Developer-chosen types | Pass | Pass | Unchanged. |
| S1 Fail fast | **Deviation (001)** | **Partly met** | The flat check adds a lock-time failure. 001's depth and value-length limits are still postponed. |
| A0 / A1 / A3 / A4 | Pass | Pass | net10, no third-party NuGet, shared build, A4 test layout. |
| A5 No document names in core | **Not yet met** | Met by design | `RowAttribute` and `FieldAttribute` move to the XML package as `XmlElementName` (R37). |
| G0-G2 | Pass | Pass | Spec, plan and tasks are the truth. The spec was corrected in planning (StanForD, `ElementText<TRow>`). |

Gate result: **passes**. C7 and A5 are what this spec implements, not violations, and it carries
001's justified S1 deviation.

## Project Structure

### Documentation (this feature)

```text
specs/003-big-data-orientation/
├── plan.md              # This file
├── research.md          # Phase 0: R32-R43
├── data-model.md        # Phase 1: row, single value, property definition, reading information, XML names
├── quickstart.md        # Phase 1: validation scenarios and the search check
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
│   ├── Row.cs                          # moved from Rows/ (R39)
│   ├── ReadingInformation.cs           # was Reading/FieldContext.cs (R36)
│   ├── Utf8Extensions.cs               # was Reading/FieldContextExtensions.cs (R36)
│   ├── Rows/                           # deleted, with Field.cs (R35, R39)
│   ├── Attributes/
│   │   ├── RowAttribute.cs             # deleted (R37)
│   │   └── FieldAttribute.cs           # deleted (R37)
│   ├── Definitions/
│   │   ├── TypeDefinition.cs           # inclusion only; gathers member failures (R33)
│   │   ├── PropertyDefinition.cs       # single-value check, Configure, ResolvedConverter; Field gone (R33, R35)
│   │   └── PropertyDefinition.OfType.cs  # ResolveConverter(options) for Converter<T>; information parameter (R33, R36)
│   ├── Reading/
│   │   ├── FormatRowEnumerator.cs      # was MediaRowEnumerator.cs (R38)
│   │   ├── FormatRowAsyncEnumerator.cs # was MediaRowAsyncEnumerator.cs (R38)
│   │   └── RowBuilder.cs               # information parameter (R36)
│   ├── DeserializeOptions*.cs          # FormatConverters, region, docs (R38)
│   ├── Throwing.cs, Formatting.Resource.cs  # MemberTypeIsNestedStructure, MemberTypeNotUtf8Formattable (R33); wording (R41)
│   └── (doc comments elsewhere)        # format and flatten (R41)
├── test/
│   ├── DefinitionValidationTests.cs    # flat check, one test per kind (R42)
│   ├── PublicSurfaceTests.cs           # new: SC-002 (R42)
│   └── Fixtures/                       # non-flat and flat row types
├── README.md, ARCHITECTURE.md, CHANGELOG.md   # R41, FR-014

Forestry.Deserialize.Xml/
├── src/
│   ├── Attributes/                     # new folder (R43)
│   │   ├── XmlElementNameAttribute.cs  # new (R37)
│   │   ├── XmlAttributeNameAttribute.cs  # was XmlAttributeValueAttribute.cs (R37)
│   │   └── XmlElementTextAttribute.cs  # was XmlTextValueAttribute.cs (R37)
│   ├── Converters/                     # new folder (R43)
│   │   └── XmlBooleanConverter.cs      # moved; namespace Forestry.Deserialize.Xml.Converters
│   ├── XmlAssociation.cs               # reads XmlElementName; exclusive sources (R37)
│   ├── DeserializeXmlOptions.cs        # ElementText<TRow>, FormatConverters (R37, R38)
│   ├── XmlRow*Enumerator.cs, XmlRowReading.cs  # renamed bases, ReadingInformation (R36, R38)
│   └── Throwing.cs, Formatting.Resource.cs  # messages name the new attributes (R37)
├── test/                               # fixtures to new attributes; renamed Field fixtures (R42)
├── README.md, ARCHITECTURE.md, CHANGELOG.md   # R41, FR-014

../stanford/Forestry.StanForD/src/
├── LogMeasurements.cs, StemMetricParts.cs     # 12 attribute renames (R40)

../../README.md                         # "through a media", "XML media" (R41)
```

**Structure Decision**: The same two library and test project pairs as 001 and 002; no new
project. Forestry.StanForD changes only as a consumer.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| S1 limits other than the context's (depth, value length) still not implemented | Carried from 001 and 002; this spec adds only the flat check's lock-time failure | Adding all limits here would turn a term and rule cleanup into a feature (C6 spirit) |
| A change in Forestry.StanForD, another silo, under this spec | Its build breaks without the attribute renames (R40) | Renaming in a later StanForD spec leaves main unbuildable between the two |
