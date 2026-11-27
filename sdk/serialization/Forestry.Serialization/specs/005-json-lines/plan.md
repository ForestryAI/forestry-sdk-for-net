# Implementation Plan: JSON Lines

**Branch**: `serialization-005-json-lines` | **Date**: 2026-11-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/005-json-lines/spec.md` (GitHub issue #138),
constitution 4.0.1.

## Summary

Complete the flat member types and add the first writing format. Forestry.Serialization gets
built-in converters for `DateOnly`, `TimeOnly`, `TimeSpan` and enums, a public base
`EnumConverter<TEnum>` that resolves an enum's fields once and reads and writes one `EnumForm`
(name or number), an `[EnumMemberName]` attribute, member-aware converter factories so a format
attribute on a telemetry member picks that member's converter, and new `TelemetryWriter` methods
(`WriteValue` for `DateOnly`, `TimeOnly`, `TimeSpan`; `WriteEnumName`). Forestry.Serialization.Xml
reads enums by `[XmlEnumForm]` and durations as `xs:duration`. Forestry.StanForD's enum converter
builds on the base, still tolerant, and writes the declared StanForD texts; a serialize-side
`AddHarvesting` adds `Stem` and `Log`. The new Forestry.Serialization.JsonLines writes one
telemetry per line through `Utf8JsonWriter` with relaxed escaping, refusing NaN and direct
`IBufferWriter` use. The round trip HPR → Stem/Log → JSON Lines → parsed and compared runs in
StanForD's tests. Decisions: [research.md](research.md) R58-R67.

## Technical Context

**Language/Version**: C# on .NET 10 (`RequiredTargetFrameworks`, A0)

**Primary Dependencies**: Base class library only (A1). JSON Lines uses `System.Text.Json`
(`Utf8JsonWriter`, `JsonEncodedText`, `JavaScriptEncoder`) from the shared framework; its tests and
StanForD's use `JsonDocument` to parse lines.

**Storage**: N/A (callers own their `Stream` or `PipeWriter`)

**Testing**: xUnit, test shells before code per story (C4), Arrange/Act/Assert comments and local
documents (A4).
- Core: converters, enum forms, member names, member-aware resolution, writer methods, through the
  string test format (C3).
- XML: `[XmlEnumForm]` and `xs:duration` over hand-written documents.
- JSON Lines: its own new test project.
- StanForD: enum texts and the round trip (fragment, and real documents when `STANFORD_HPR_FOLDER`
  is set).

**Target Platform**: Any .NET 10 runtime (library)

**Project Type**: Library: the core package, two format packages (XML, new JSON Lines) and one
consumer (Forestry.StanForD)

**Performance Goals**: No allocation per value on the writing path beyond what spec 004 has: enum
names pre-encoded once, member names pre-encoded per type definition, dates formatted into stack
buffers. No performance gate for JSON Lines yet (C6: the round trip first).

**Constraints**:
- Core references no format (C0); no output names in core (A5): `EnumForm` and `[EnumMemberName]`
  describe values, the format attributes live in their packages.
- Every package builds and its tests pass at every commit.
- `TreatWarningsAsErrors` SDK-wide; XML docs on every public member (D0).
- A developer's registered converter always wins (FR-002e).

**Scale/Scope**: Five stories, about 25 functional requirements. Roughly 12 new or changed source
files in Forestry.Serialization, 4 in Forestry.Serialization.Xml, 4 in Forestry.StanForD, a new
package of about 6 source files, CI and release workflow entries, READMEs, changelogs and a how-to
page.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Check | Result |
|---|---|---|---|
| C0 | Format-agnostic serialization | Core gains converters, `EnumConverter<TEnum>`, `EnumForm`, `[EnumMemberName]`, member-aware factories and writer methods; formats subclass and register. JSON Lines references core only. Value kinds match 4.0.1 ("dates and times, enums"). | Pass |
| C1 | Partial deserialization | Reading path unchanged; new converters read one value. | Pass |
| C2 | Single pass flattening | Unchanged; the round trip reads once. | Pass |
| C3 | Tested through a real format package | Core writing through the string test format; XML reading through the XML package; JSON Lines tests its own writer. | Pass |
| C4 | Test-first | Shells per acceptance scenario before code, per story. | Pass (in tasks) |
| C5 | Design ownership | R58-R67 are proposals until confirmed. | Pass |
| C6 | Working example first | The round trip (US5) before any JSON Lines performance work; no performance matrix entry yet. | Pass |
| C7 | Flat telemetries | Enums, `DateOnly`, `TimeOnly`, `TimeSpan` are single values (already accepted by the flat check). | Pass |
| C8 | Streaming writes | One line per telemetry, flushed per telemetry; the `Utf8JsonWriter` is reset, not grown; outputs never closed. | Pass |
| D0 | Public documentation | XML docs; the JSON Lines README's "Writing JSON Lines" section is the how-to page (as spec 004's README section); READMEs and changelogs of the four packages. | Pass |
| S0 | Developer-chosen types only | Enum converters only for member types of added telemetries. | Pass |
| S1 | Fail fast | Duplicate member names fail at lock (R62); a member without a writing converter still fails at lock. Values a form cannot represent (undefined enum value by name, NaN) fail when written, named. | Pass |
| A1 | No third-party dependencies | System.Text.Json is in the shared framework. | Pass |
| A5 | No document or output names in core | `XmlEnumForm`/`JsonLinesEnumForm` in their packages; core attributes name values. | Pass |

Re-check after design: unchanged, all pass. No violations to justify.

## Project Structure

### Documentation (this feature)

```text
specs/005-json-lines/
├── plan.md              # This file
├── research.md          # R58-R67
├── data-model.md        # Types and relationships
├── quickstart.md        # Validation scenarios
├── contracts/
│   └── public-api.md    # Public surface added or changed
├── checklists/
│   └── requirements.md
└── tasks.md             # /speckit-tasks (not created here)
```

### Source Code (repository root: `sdk`)

```text
serialization/Forestry.Serialization/src/
├── Attributes/EnumMemberNameAttribute.cs              # new (R62)
├── Converters/
│   ├── ConverterFactory.cs                            # member-aware CreateConverter, UsesMember (R58)
│   ├── EnumConverter.OfType.cs                        # new: base, EnumForm (R59)
│   ├── EnumFields.OfType.cs                           # new, internal: fields resolved once per enum (R59)
│   └── Primitive/
│       ├── EnumConverterFactory.cs                    # new: EnumDefaultConverter<TEnum>, names (R59)
│       ├── DateOnlyConverter.cs / TimeOnlyConverter.cs / TimeSpanConverter.cs   # new (R61)
│       └── NullableConverter.OfType.cs                # member-aware (R58)
├── Definitions/PropertyDefinition.OfType.cs           # passes its member (R58)
├── Serialization/TelemetrySerializationOptions.Caching.cs   # ResolveConverter(type, member) (R58)
├── Writing/TelemetryWriter.cs                         # new methods (R60); virtual IBufferWriter members (R66)
└── Formatting.Resource.cs / Throwing.cs               # new messages

serialization/Forestry.Serialization/test/
├── Fixtures/TestingSerializeOptions.cs                # writer gains the new methods
├── BuiltInConverterTests.cs                           # US1: dates, times, durations, enums
└── EnumConverterTests.cs                              # US1: forms, member names, member-aware resolution

serialization/Forestry.Serialization.Xml/src/
├── Attributes/XmlEnumFormAttribute.cs                 # new (R64)
├── Converters/XmlEnumConverter.cs                     # new: factory + converter (R64)
├── Converters/XmlTimeSpanConverter.cs                 # new: xs:duration (R64)
└── DeserializeXmlOptions.cs                           # FormatConverters
serialization/Forestry.Serialization.Xml/test/XmlEnumAndDurationTests.cs   # US4

serialization/Forestry.Serialization.JsonLines/        # new package (R65)
├── Forestry.Serialization.JsonLines.slnx
├── Directory.Build.props / README.md / CHANGELOG.md / ARCHITECTURE.md
├── src/
│   ├── Forestry.Serialization.JsonLines.csproj
│   ├── SerializeJsonLinesOptions.cs
│   ├── JsonLinesTelemetryWriter.cs
│   ├── Attributes/JsonLinesEnumFormAttribute.cs
│   ├── Converters/JsonLinesEnumConverter.cs
│   └── Formatting.Resource.cs / Throwing.cs
└── test/
    ├── Forestry.Serialization.JsonLines.Tests.csproj
    └── JsonLinesWriterTests.cs                        # US3

stanford/Forestry.StanForD/src/
├── Converters/StanForDEnumConverter.cs                # on the base, tolerant read (R63)
├── CuttingReason.cs                                   # [EnumMemberName] texts
└── HarvestingOptions.cs                               # AddHarvesting(this SerializeOptions) (R63)
stanford/Forestry.StanForD/test/
├── Forestry.StanForD.Tests.csproj                     # references the JSON Lines package (test only)
├── StanForDEnumTests.cs                               # US2
└── RoundTripTests.cs                                  # US5

../.github/workflows/ci.yml, release.yml               # JSON Lines in build-test, pack, release (R67)
```

**Structure Decision**: The JSON Lines package mirrors `Forestry.Serialization.Xml` (its own folder,
solution, props, `src/`, `test/`), so CI, packing and releasing treat it like the other packages.
Core additions follow existing folders (`Attributes`, `Converters/Primitive`, `Writing`). The round
trip lives in StanForD's tests, the one project combining StanForD, XML and JSON Lines (C6); the
StanForD package itself does not reference JSON Lines.

## Story order

1. **US1** (core converters, base enum converter, member names, member-aware resolution, writer
   methods): everything else builds on it.
2. **US2** (StanForD enums) and **US4** (XML enum and duration converters): independent of each
   other, both need US1.
3. **US3** (JSON Lines package): needs US1.
4. **US5** (round trip): needs US2 and US3; then polish (READMEs, changelogs, how-to, CI, release).

## Complexity Tracking

No constitution violations.
