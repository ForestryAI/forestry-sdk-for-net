# Findings: When Harvesting values are updated (US5, #96)

Spec: [spec.md](spec.md) (User Story 5) · Tasks: [tasks.md](tasks.md) (T009-T012)

Checked on 2026-11-13 against 12 real Harvesting documents of one contract (`ContractNumber`
11002374) from one Rottne harvester ("Sender XC" program), created 12-15 January 2026, and the
StanForD 2010 V4p1 schemas in `schemas/`. The documents are not in the repository (they hold
personal data); they are named here by their creation time.

## 1. The contract start date is not updated when cutting restarts (T009)

| Document created | `StartDate` | `ObjectModificationDate` | Version | Stem keys |
|---|---|---|---|---|
| 2026-01-12 13:59 | 2026-01-12 12:06:41 | 2026-01-12 12:05:10 | 3.5 | 618729-618748 |
| 2026-01-12 17:59 | same | same | 3.5 | 618748-618866 |
| 2026-01-12 18:37 | same | same | 3.5 | 618866-618892 |
| 2026-01-13 13:28 | same | same | 3.5 | 618892-618977 |
| 2026-01-13 17:13 | same | same | 3.5 | 618977-619073 |
| 2026-01-14 07:25 | same | same | 3.5 | 619073-619074 |
| 2026-01-14 11:25 | same | same | 3.5 | 619074-619165 |
| 2026-01-14 15:25 | same | same | 3.5 | 619165-619255 |
| 2026-01-14 17:14 | same | same | 3.5 | 619255-619320 |
| 2026-01-15 09:20 | same | same | 3.5 | 619320-619445 |
| 2026-01-15 12:39 | same | same | 3.5 | 619445-619601 |

(The 2026-01-14 11:25 document exists twice, compressed and not; one row is shown.)

- `ObjectDefinition/StartDate` stays 2026-01-12 12:06:41 over four days, although cutting stopped
  overnight three times. For this machine it is **set once**, when the object starts, not when
  cutting restarts.
- `ObjectModificationDate` does not change either: the object was not modified during the work.
- **Consequence**: the start date says nothing about when a stem was cut. Rows use the header's
  `CreationDate` instead (section 3).

## 2. Documents are incremental, and each repeats the previous document's last stem

- Every document carries all common definitions again (machine, operator, species groups,
  products, object), unchanged between documents.
- Stems are incremental: each document holds the stems cut since the previous one. **The first
  stem of a document is the last stem of the previous one**, complete with the same logs (e.g. stem
  619255, `StemNumber` 527, logs 1-5, is in both the 14 January 15:25 and 17:14 documents).
- **Consequence**: the repeated stem gives Stem and Log rows twice, with the same `Id` but a
  different `HarvestingCreationTime`, so in two time partitions. Deserializing one document cannot
  see the other; duplicates are removed downstream by `Id` (the sink or the analysis store). No
  policy or story is needed in this package (issue #96, last requirement).

## 3. Timestamps per common definition (T010)

From the V4p1 schemas (by element name) and the real documents:

| Source | Date element | Kind | In real documents | Used by rows |
|---|---|---|---|---|
| Document header | `HarvestedProductionHeader/CreationDate` | when the machine created the document | yes | **`HarvestingCreationTime`** (UTC), the partition time |
| Document header | `HarvestedProductionHeader/ModificationDate` | document | yes | no |
| Object (contract) | `ObjectDefinition/StartDate` | set once when the object starts (section 1) | yes | no (a dimension later, maybe) |
| Object (contract) | `ObjectDefinition/ObjectModificationDate` | modified | yes, unchanged | no |
| Species group | `SpeciesGroupDefinition/SpeciesGroupModificationDate` | modified | yes, unchanged | no |
| Product | `ProductCreationDate` (schema), `ClassifiedProductDefinition/ModificationDate` (documents) | created once / modified | modification date only | no |
| Operator | none | — | — | — |

- **Decision (developer, 2026-11-13)**: Stem and Log rows are partitioned by
  `HarvestingCreationTime` (header `CreationDate`, in UTC) then `ContractNumber`. Documents are
  incremental, so the creation time is as close to the cut time as the document allows. Most
  reporting (e.g. in Biometria) filters by time, then contract.
- Constitution C2 (a timestamp traced to the document) is met by the header `CreationDate`.

## 4. Schema version (T011)

- All 12 documents declare `version="3.5"` (`HarvestedProduction_V3p5.xsd`); `schemas/` holds
  V4p1 only.
- **Decision (developer, 2026-11-13)**: the POC supports the latest schema version (V4p1) only.
  The version check on the root element (a schema version policy) is not part of the POC; types
  built from V4p1 are checked against the real 3.5 documents, not assumed valid (A2).

## 5. Other observations, for later stories

- `BoomPositioning/BoomExtension` is 65535 for every stem: the schema's "default" value meaning the
  normal extension, not a measurement.
- `ProcessingCategory` is `SingleTreeProcessing` for every stem in these documents; multi-tree
  processing and felling exist in other documents.
- `CuttingReason` values seen: "Automatic" and "Other manual"; StanForD's texts contain spaces, so
  an enum needs a converter (a Forestry.Deserialize follow-up).
- Units come from root attributes (`diameterUnit="mm"`, `lengthUnit="cm"`, `volumeUnit="m3"`);
  rows carry them without converting.
- Definition keys are not always first: `SpeciesGroupKey` comes after the grades, at the end of
  `SpeciesGroupDefinition`.

## Pulled forward into US5

While reading the documents, the row classes were drafted ahead of US2 and US7 (developer,
2026-11-13): `Log`, `Stem`, `Machine`, `ObjectDefinition`, `SpeciesGroupDefinition`,
`OperatorDefinition`, `ProductDefinition`, `CuttingReason`, `ProcessingCategory` and
`CoordinateReferenceSystem` in `src/`. Rows have four regions: identification, partitioning,
metrics and dimensions. They are placeholders for US2 (identification) and US7 (members from the
query groups); no policies exist yet.
