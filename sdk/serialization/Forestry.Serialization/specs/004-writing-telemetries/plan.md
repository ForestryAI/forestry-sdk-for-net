# Implementation Plan: Writing telemetries

**Branch**: `serialization-004-writing` | **Date**: 2026-11-26 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/004-writing-telemetries/spec.md` (GitHub issue #137),
constitution 4.0.0.

## Summary

Add the writing side to Forestry.Serialization. Converters gain `Write` next to `Read` (renamed
from `Convert`). A common options base, `TelemetrySerializationOptions`, holds the telemetry types,
converters and definitions that `DeserializeOptions` and a new `SerializeOptions` share.
`TelemetrySerialization.Serialize`/`SerializeAsync` write one telemetry or a sequence to a caller's
`Stream` (sync and async, through a per-call pooled buffer) or `PipeWriter` (async, no copy),
never disposing or completing it. A format supplies a `TelemetryWriter` that converters delegate to by kind; the
core walks the definitions with compiled getters. Opt-in collection writers,
`MonitoringCollectionStream` and `MonitoringCollectionPipeWriter`, share `ICollectionWriter`
(options, sequence, bytes, full, completion hook) and `MonitoringCollectionOptions`. Writing is
proven in this package through a string test format (C3); the pipeline-free `foreach` memory test
lives in the XML package's tests (C8). Decisions: [research.md](research.md) R44-R57.

## Technical Context

**Language/Version**: C# on .NET 10 (`RequiredTargetFrameworks`, A0)

**Primary Dependencies**: None beyond the base class library (A1): `IBufferWriter<byte>`,
`ArrayPool<byte>`, `MemoryPool<byte>`, `IUtf8SpanFormattable`, `System.IO.Pipelines` (already
referenced for `PipeReader`).

**Storage**: N/A (callers own their `Stream` or `PipeWriter`)

**Testing**: xUnit.
- Converters, getters, `Serialize` to `Stream` and `PipeWriter`, and collection writers in
  `Forestry.Serialization.Tests`, through the string test format (C3, R54).
- The `foreach` Deserialize → Serialize memory test in `Forestry.Serialization.Xml.Tests` (C8,
  R55).
- The existing suites of Forestry.Serialization, Forestry.Serialization.Xml and Forestry.StanForD
  check the `Convert` → `Read` rename (SC-002, R56).
- Test shells before code (C4); each story's gate runs `Performance.runsettings`.

**Target Platform**: Any .NET 10 runtime (library)

**Project Type**: Library (public SDK package), its XML format package and one consumer
(Forestry.StanForD)

**Performance Goals**: No allocation per value on the writing path (getters compiled, converters
generic, buffers pooled). Memory does not grow with the number of telemetries written (C8,
SC-005).

**Constraints**:
- Core references no format (C0); no output names in core (A5).
- Every package builds and its tests pass at every commit (R56).
- Never dispose a caller's `Stream` or complete a caller's `PipeWriter` (FR-014).
- `TreatWarningsAsErrors` SDK-wide; XML docs on every public member (D0).

**Scale/Scope**: Three stories, 29 functional requirements. About 15 new or changed source files
in Forestry.Serialization, 1 in Forestry.Serialization.Xml and 1 in Forestry.StanForD (the rename),
a string test format, and the package README.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Check | Result |
|---|---|---|---|
| C0 | Format-agnostic serialization | Core owns options base, definitions, converters, getters, `TelemetrySerialization`, collection writers; formats subclass the abstract `TelemetryWriter` (R49), which converters delegate to; C0 revised before merge to allow it. No format reference. | Pass |
| C1 | Partial deserialization | Reading unchanged. | Pass |
| C2 | Single pass flattening | Reading unchanged; writing never re-reads (R55's `foreach`). | Pass |
| C3 | Tested through a real format package | Writing tested in this package through the string test format over a `Stream` (R54); reading still through XML. | Pass |
| C4 | Test-first | Shells per acceptance scenario before code, per story. | Pass (in tasks) |
| C5 | Design ownership | R44-R57 are proposals until confirmed. | Pass |
| C6 | Working example first | The round trip to JSON Lines is #138; this spec's `foreach` example comes before any writing performance work. | Pass |
| C7 | Flat telemetries | Definitions built by the shared base enforce the flat rule both ways (R44). | Pass |
| C8 | Streaming writes | Per-call buffer only (R48); state across calls in opt-in collection writers (R51); `foreach` memory test (R55). | Pass |
| D0 | Public documentation | XML docs, README section with a `MemoryStream` example (FR-029). | Pass |
| S0 | Developer-chosen types only | Serializing only writes telemetries of types added to the options (FR-017). | Pass |
| S1 | Fail fast | A member that cannot be written fails at lock (R45). | Pass |
| A1 | No third-party dependencies | BCL only. | Pass |
| A5 | No document or output names in core | Output framing and names are the format's `TelemetryWriter`. | Pass |

Re-check after design: unchanged, all pass. No violations to justify.

## Project Structure

### Documentation (this feature)

```text
specs/004-writing-telemetries/
├── plan.md              # This file
├── research.md          # R44-R57
├── data-model.md        # Types and relationships
├── quickstart.md        # Validation scenarios
├── contracts/
│   └── public-api.md    # Public surface added or changed
├── checklists/
│   └── requirements.md
└── tasks.md             # /speckit-tasks (not created here)
```

### Source Code (repository root: `sdk/serialization`)

```text
Forestry.Serialization/src/
├── TelemetrySerializationOptions.cs       # new: common base, members and hooks (R44)
├── TelemetrySerializationOptions.Caching.cs # new: frozen definitions, converter resolution (R44)
├── DeserializeOptions.cs                  # derives from the base; reading-only members stay
├── SerializeOptions.cs                    # new: writing options, DefaultCollectionOptions
├── TelemetrySerialization.Serialize.cs       # new: Serialize/SerializeAsync overloads (R47, R48)
├── Converters/
│   ├── Converter.OfType.cs                # Convert -> Read; virtual Write (R45)
│   └── Primitive/*.cs                     # Write overrides
├── Definitions/
│   ├── TypeDefinition.cs                  # built from the base
│   └── PropertyDefinition.OfType.cs       # compiled getter, WriteMember (R46, R49)
├── Writing/
│   ├── TelemetryWriter.cs                 # done in US1: the format's writer (R49)
│   ├── PooledBufferWriter.cs              # new, internal: per-call buffer (R48)
│   ├── ICollectionWriter.cs               # new (R51)
│   ├── MonitoringCollectionStream.cs      # new (R51)
│   ├── MonitoringCollectionPipeWriter.cs  # new (R51)
│   └── MonitoringCollectionOptions.cs     # new (R53)
└── Formatting.Resource.cs / Throwing.cs   # new messages

Forestry.Serialization/test/
├── Fixtures/TestingSerializeOptions.cs    # string test format (R54)
├── ConverterWriteTests.cs                 # US1
├── SerializeTests.cs                      # US2 (Stream sync/async, PipeWriter)
└── CollectionWriterTests.cs               # US3

Forestry.Serialization.Xml/src/Converters/XmlBooleanConverter.cs   # Convert -> Read
Forestry.Serialization.Xml/test/SerializeMemoryTests.cs           # C8 foreach (R55)
../stanford/Forestry.StanForD/src/Converters/StanForDEnumConverter.cs # Convert -> Read
```

**Structure Decision**: Writing types live in a new `Writing` folder and namespace
`Forestry.Serialization.Writing`, mirroring `Reading`; `SerializeOptions` and the base sit next to
`DeserializeOptions` in the root namespace.

## Complexity Tracking

No constitution violations.
