# Quickstart: validating Harvesting rows

Spec: [spec.md](spec.md) · Contract: [contracts/public-api.md](contracts/public-api.md)

## Prerequisites

- .NET SDK per `global.json`.
- Forestry.Deserialize's deserialization context (spec 002, #99, merged via #104).
- For the end-to-end row only: real Harvesting documents in a local folder, named by the environment
  variable `STANFORD_HPR_FOLDER` (`*.hpr`, `*.hpr.xml`, `*.hpr.gz`; not committed: they contain
  personal data). Without it, that test is skipped.

## Run

```bash
dotnet test sdk/stanford/Forestry.StanForD/test/Forestry.StanForD.Tests.csproj
```

The test project's `Forestry.StanForD.Tests.runsettings` leaves `Category=Performance` out; the
performance gate itself is a later spec.

## Scenarios

Every scenario is a test (classes in `sdk/stanford/Forestry.StanForD/test/`); "fragment" means
`test/Fragments/MinimalHarvesting.xml` (and `MinimalHarvestingNext.xml` where noted), made-up values.
**Run 2026-11-15** (T068): 47 passed, 0 failed, 0 skipped on net10.0, with `STANFORD_HPR_FOLDER` set to
12 real Harvesting documents of one contract and one harvester.

| # | Story | Scenario | Expected | Test | Result |
|---|---|---|---|---|---|
| 1 | US2 | Fragment: one machine, two stems, three logs | Stems carry `MachineKey`, `StemKey`, `Id`; logs add `LogKey` | `IdentificationTests.Stem_ItShould_CarryMachineKeyStemKeyAndId`, `Log_ItShould_CarryMachineKeyStemKeyLogKeyAndId` | Passed |
| 2 | US2 | The fragment and a copy with another machine key, equal stem and log keys | No `Id` occurs twice (SC-001) | `IdentificationTests.TwoMachinesWithEqualKeys_ItShould_GiveNoIdTwice` | Passed |
| 3 | US2 | Log key 1 under two stems | Two different log `Id`s | `IdentificationTests.LogKeysRestartingPerStem_ItShould_GiveDifferentIds` | Passed |
| 4 | US2 | A stem in a machine with no `MachineKey` | Fails, naming the stem and `MachineKey` | `IdentificationTests.StemWithoutMachineKey_ItShould_Throw` | Passed |
| 5 | US2 | A stem or log referring to an undefined object, species group, operator or product | Fails, naming the stem, the key and its value | `IdentificationTests.ReferenceToUndefinedDefinition_ItShould_Throw` (4 cases) | Passed |
| 6 | US2 | The same document twice; the stem repeated in the next document | Same `Id`s (FR-004) | `IdentificationTests.SameDocumentTwice_ItShould_GiveSameIds`, `StemRepeatedInNextDocument_ItShould_HaveSameIds` | Passed |
| 7 | US2 | Fragment | Only Stem and Log rows come out | `IdentificationTests.HarvestingDocument_ItShould_GiveOnlyStemsAndLogs` | Passed |
| 8 | US3 | Header `CreationDate` 15:25:33+01:00 | Every stem and log has `HarvestingCreationTime` 14:25:33 UTC (offset zero) | `PartitioningTests.HarvestingDocument_ItShould_GiveEveryRowTheCreationTimeInUtc` | Passed |
| 9 | US3 | Fragment | Every stem and log has `ContractNumber` and `ObjectName` | `PartitioningTests.HarvestingDocument_ItShould_GiveEveryRowTheContractNumberAndObjectName` | Passed |
| 10 | US3 | The stem repeated in the next document | Same `Id`, the later creation time | `PartitioningTests.StemRepeatedInNextDocument_ItShould_CarryTheLaterCreationTime` | Passed |
| 11 | US3 | Creation date with seven fraction digits, as in real documents | Kept | `PartitioningTests.CreationDateWithSevenFractionDigits_ItShould_KeepThem` | Passed |
| 12 | US3 | No header `CreationDate`; a stem with no `ObjectKey` | Fails, naming the stem and what is missing | `PartitioningTests.HeaderWithoutCreationDate_ItShould_Throw`, `StemWithoutObjectKey_ItShould_Throw` | Passed |
| 13 | US4 | A log with three `LogVolume`s by category | Each volume in its own member | `ConditionalMappingTests.ThreeVolumes_ItShould_FillTheirOwnMembers` | Passed |
| 14 | US4 | Eight `LogDiameter`s under a "Machine" `LogMeasurement`, and the length | Eight diameters and `MachineLength` set | `ConditionalMappingTests.EightDiametersAndLength_ItShould_FillTheirOwnMembers` | Passed |
| 15 | US4 | A log measuring only some categories | The others stay 0, "not measured" | `ConditionalMappingTests.LogWithSomeMeasurements_ItShould_KeepZeroForTheOthers` | Passed |
| 16 | US4 | A volume matching the volume category but measured by "Other" | Sets nothing (with `Ignore`) | `ConditionalMappingTests.VolumeMatchingOneCategoryOnly_ItShould_SetNothing` | Passed |
| 17 | US4 | Unknown volume or diameter category, or diameters under an "Other" measurement, default options | Fails, naming element and categories | `ConditionalMappingTests.UnknownVolumeCategory_ItShould_ThrowByDefault`, `UnknownDiameterCategory_ItShould_ThrowByDefault`, `DiametersUnderAnotherMeasurement_ItShould_ThrowByDefault` | Passed |
| 18 | US4 | Unknown volume category, `Unmatched = Ignore` | Ignored; the other members set | `ConditionalMappingTests.UnknownCategoryWithIgnore_ItShould_SetTheOtherMembers` | Passed |
| 19 | US4 | Two `LogVolume`s with the same categories | Fails (FR-013) | `ConditionalMappingTests.TwoVolumesWithSameCategories_ItShould_Throw` | Passed |
| 20 | US4 | An operator (M2) volume beside the machine's | Ignored by default, machine members unchanged (#109) | `ConditionalMappingTests.OperatorVolume_ItShould_BeIgnoredByDefault` | Passed |
| 21 | US4 | Fragment | No measurement rows come out | `ConditionalMappingTests.HarvestingDocument_ItShould_GiveNoMeasurementRows` | Passed |
| 22 | US7 | Fragment | Stems carry stem number, object, sub-object, species group (4 identifiers) and operator dimensions | `DimensionTests.Stem_ItShould_CarryItsDimensions` | Passed |
| 23 | US7 | Fragment | Logs carry their stem's dimensions (no join) | `DimensionTests.Log_ItShould_CarryItsStemsDimensions` | Passed |
| 24 | US7 | Fragment | Logs carry product (4 identifiers) and destination | `DimensionTests.Log_ItShould_CarryItsProductAndDestination` | Passed |
| 25 | US7 | Fragment | Every row carries `MachineUserID` and `LoggingContractorBusinessID` | `DimensionTests.EveryRow_ItShould_CarryMachineUserIdAndLoggingContractor` | Passed |
| 26 | US7 | 12 real documents (`STANFORD_HPR_FOLDER`) | One Stem row per `<Stem>`, one Log row per `<Log>`, every row identified, no failure | `EndToEndTests.RealDocuments_ItShould_GiveOneRowPerStemAndLog` | Passed |
| 27 | US7 | Stem and Log row types | Every member a single value (C0) and named in data-model.md (C3) | `RowShapeTests.Row_ItShould_HaveOnlySingleValueMembers`, `Row_ItShould_HaveEveryMemberInTheDataModel` (Stem and Log) | Passed |
| 28 | US8 | North and east; south and west | Positive; negative coordinates, with altitude and coordinate reference system | `StemMetricTests.NorthEastCoordinates_ItShould_BePositive`, `SouthWestCoordinates_ItShould_BeNegative` | Passed |
| 29 | US8 | "Felling" and "Last log cut" boom positions | Each sets its own angle and extension | `StemMetricTests.BoomPositions_ItShould_SetTheirOwnMembers` | Passed |
| 30 | US8 | Single-tree processed stems | Each stem carries its DBH | `StemMetricTests.SingleTreeProcessedStem_ItShould_CarryDbh` | Passed |
| 31 | US8 | Unknown boom positioning or latitude category | Fails by default; with `Ignore`, the other metrics set | `StemMetricTests.UnknownBoomPositioningCategory_ItShould_ThrowByDefault`, `UnknownBoomPositioningCategoryWithIgnore_ItShould_SetTheOtherMetrics`, `UnknownLatitudeCategory_ItShould_ThrowByDefault` | Passed |
| 32 | US8 | A latitude in a species group's bark function | Ignored; the stem's latitude unchanged (#110) | `StemMetricTests.LatitudeOutsideStemCoordinates_ItShould_BeIgnored` | Passed |
| 33 | US8 | Fragment | No metric rows come out | `StemMetricTests.HarvestingDocument_ItShould_GiveNoMetricRows` | Passed |

US5 and US6 are validated by review, not tests: their findings (`findings-us5.md`) and the query
groups (`queries.md`) are documents.

*Changed 2026-11-15 (T068)*: the scenarios follow what was built. The earlier row 5 (object start and
modification dates on the rows) was dropped with US5's findings; US3 is partitioning by the header's
creation time. Dimensions (US7) and stem metrics (US8) were added.
