# Quickstart: validating Harvesting rows

Spec: [spec.md](spec.md) · Contract: [contracts/public-api.md](contracts/public-api.md)

## Prerequisites

- .NET SDK per `global.json`.
- Forestry.Deserialize capabilities D1-D2 ([research.md](research.md) R1) for the stories that need
  them.
- For end-to-end and performance rows only: real Harvesting documents in a local folder, named by
  the environment variable `STANFORD_HPR_FOLDER` (not committed: they contain personal data).

## Run

```bash
dotnet test sdk/stanford/Forestry.StanForD/test/Forestry.StanForD.Tests.csproj
```

The test project's `Forestry.StanForD.Tests.runsettings` leaves `Category=Performance` out; the
performance gate itself is a later spec.

## Scenarios

| # | Story | Scenario (fragment unless noted) | Expected |
|---|---|---|---|
| 1 | US2 | One machine, two stems, two logs each | Stem rows carry machine and stem keys; Log rows add log keys |
| 2 | US2 | Two documents, two machines, equal stem and log keys | No identification occurs twice (SC-001) |
| 3 | US2 | Stem before any `MachineKey` | Fails, naming the stem |
| 4 | US2 | Stem referencing an `ObjectKey` not defined | Fails, naming stem and key |
| 5 | US3 | Object with `StartDate` and `ObjectModificationDate` | Stem and its logs carry both, plus the header creation date |
| 6 | US4 | Log with three `LogVolume`s by category | Each volume in its own member |
| 7 | US4 | `LogDiameter`s under `LogMeasurement logMeasurementCategory="Machine"` | Eight diameters in their members |
| 8 | US4 | Unknown `logVolumeCategory`, default options | Fails, naming element and attributes |
| 9 | US4 | Unknown `logVolumeCategory`, `Unmatched = Ignore` | Ignored; other members set |
| 10 | US4 | Two `LogVolume`s with the same categories | Fails (FR-013) |
| 11 | US7 | Real document (`STANFORD_HPR_FOLDER`) | One Stem row per `Stem`, one Log row per `Log` (e.g. 66 and 278) |
| 12 | US7 | Row types' members | All single values; each traced in data-model.md (SC-004) |

US5 and US6 are validated by review: their findings and the query groups are documents, not code.
