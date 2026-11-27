# Quickstart: validating JSON Lines

**Plan**: [plan.md](plan.md) | API: [contracts/public-api.md](contracts/public-api.md)

## Prerequisites

- .NET SDK from `global.json`, run from the repository root.
- Branch `serialization-005-json-lines`.
- Optional: `STANFORD_HPR_FOLDER` set to a folder of real Harvesting documents (never committed).

## Build and test

```powershell
dotnet test sdk/serialization/Forestry.Serialization/Forestry.Serialization.slnx
dotnet test sdk/serialization/Forestry.Serialization.Xml/Forestry.Serialization.Xml.slnx
dotnet test sdk/serialization/Forestry.Serialization.JsonLines/Forestry.Serialization.JsonLines.slnx
dotnet test sdk/stanford/Forestry.StanForD/Forestry.StanForD.slnx
```

Expected: all pass, including every test that existed before this spec (core 119, XML 188,
StanForD 46 + 1 skipped; SC-006).

## Scenarios

| # | Scenario | Where | Expected |
|---|---|---|---|
| 1 | A telemetry with enum, `DateOnly`, `TimeOnly`, `TimeSpan` members (and nullables), no converters registered | `BuiltInConverterTests` | Definitions build; each value writes and reads back the same, boundaries included (SC-001, SC-002) |
| 2 | A string test writer overriding `WriteValue(TimeSpan)` | `BuiltInConverterTests` | The override writes every `TimeSpan`, other kinds unchanged |
| 3 | An enum with `[EnumMemberName]`; a duplicate name; an undefined value by name; `[Flags]` | `EnumConverterTests` | Declared name both ways; lock fails naming the duplicate; undefined refused; flags joined with `", "` |
| 4 | Two members of one enum type, a test factory that `UsesMember` | `EnumConverterTests` | Each member gets its own converter; a registered converter wins |
| 5 | Each `CuttingReason`, `ProcessingCategory`, `CoordinateReferenceSystem` member written and read; "OtherManual", "other manual" | `StanForDEnumTests` | StanForD texts written; tolerant read (SC-003) |
| 6 | Documents with an enum member, no attribute / `[XmlEnumForm(Number)]`; durations `PT1H30M`, `P2DT3H`, `-PT0.5S`, `P1M` | `XmlEnumAndDurationTests` | Right member or format error; durations exact, `P1M` fails (SC-007) |
| 7 | Telemetries with every member kind, JSON-special text, nulls, `[JsonLinesEnumForm(Number)]` | `JsonLinesWriterTests` | One line each; `JsonDocument` parses each; kinds and values equal; non-ASCII as plain UTF-8 (SC-005) |
| 8 | NaN `double`; a converter calling `GetSpan` | `JsonLinesWriterTests` | `NotSupportedException` naming the member |
| 9 | Same telemetries: `Stream` sync, async, `PipeWriter`; into a `MonitoringCollectionStream` | `JsonLinesWriterTests` | Identical bytes; target left open; `Sequence` = lines |
| 10 | `MinimalHarvesting.xml` → Stem and Log → two JSON Lines outputs → parsed and compared | `RoundTripTests` | Every line equals its telemetry (SC-004) |
| 11 | Same as 10 over every real document | `RoundTripTests` (`[RealDocumentFact]`) | Every line equals; skipped without the folder |

## CI check

The pull request shows `build-test (Forestry.Serialization.JsonLines)` and
`pack (Forestry.Serialization.JsonLines)`. Before merging, the developer adds the `build-test`
check to the main ruleset's required checks.

## Documentation check

The JSON Lines README's "Writing JSON Lines" section serializes telemetries into a `MemoryStream`
and reads the lines back; following it alone works.
