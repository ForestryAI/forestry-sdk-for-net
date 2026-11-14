# Queries: what Harvesting rows are asked (US6, #97)

Spec: [spec.md](spec.md) (User Story 6, FR-015) · Data model: [data-model.md](data-model.md) ·
Tasks: [tasks.md](tasks.md) (T013-T016)

Collected 2026-11-14 from what is offered to the Swedish forest industry today (#97). Ongoing: add
queries as the POC is shown to more people; a new query joins a group or starts one, in the same
format. Every Stem and Log member traces to a use here (constitution C3); US7 chooses the missing
members from the **hpr** groups.

## How the industry asks

Harvesting reports vary little. They are predefined and seen from one actor's perspective, and every
one filters the same way first:

1. **contract** (the harvesting contract, within its lifespan),
2. **a time period**,
3. then a few predefined filters, from the production plan (e.g. the receiving actor) or from
   StanForD (e.g. the machine).

## Sources

Not every report can be answered from Harvesting documents. Each group below names its source:

| Source | What it holds | In the POC |
|---|---|---|
| **hpr** | StanForD Harvesting documents: the machine, the common definitions (object, species groups, products, operator), stems and logs with their measurements; time only as the document's creation time | **Yes**: Stem and Log members come from here |
| **Biometria** | The commercial chain: initial agreements and later transactions, production plans, the actors' rights and perspectives (coordinator, receiver, contractor). Only by subscribing to entity events, not free, no REST interface | No: a later add-on (e.g. Forestry.Biometria) enriching rows |
| **MOM** | StanForD operational monitoring: work time, fuel, machine effectiveness, when things happened | No: another StanForD document family |

StanForD keeps production (hpr) apart from operational monitoring (MOM): stems and logs carry no
felling or cutting time, so time in the hpr groups is the document's creation time
(`HarvestingCreationTime`, findings-us5.md).

**Joining later**: the common definitions' **User Ids** are StanForD's external references
(`ObjectUserID`, `ProductUserID`, `SpeciesGroupUserID`, `MachineUserID`), the keys a Biometria
add-on would join its entities on. Carrying them on the rows costs a few strings read from the
document and keeps that door open; without them only `ContractNumber` links rows to the commercial
chain.

## Groups

Each query: plain language, who asks, filters (F), groupings (G), summaries (S). Members in
*italics* are not on the rows yet.

### G1. Harvested volumes per contract (coordinator's perspective)

| Query | Who asks | F | G | S | Source |
|---|---|---|---|---|---|
| How much has been harvested on this contract this month, per product? | Contract coordinator | `ContractNumber`, `HarvestingCreationTime` | `ProductName` | sum of `MachineVolumePrice`, `MachineVolumeUnderBark` | hpr |
| Same, per object and sub-object | Contract coordinator | as above | `ObjectName`, *`SubObjectName`* | sum of volumes | hpr |
| Same, per species group | Contract coordinator | as above | *species group on Log* (`SpeciesGroupName` is on Stem) | sum of volumes | hpr |
| Volumes against the production plan | Contract coordinator | contract | product | planned vs harvested | Biometria |

### G2. Harvested volumes per receiver (receiver's perspective)

| Query | Who asks | F | G | S | Source |
|---|---|---|---|---|---|
| What is coming to my site from this contract? | Receiving actor (e.g. sawmill) | contract, time | *product destination* | sum of volumes | hpr (destination from the product definition), rights from Biometria |
| Everything coming to me, across contracts | Receiving actor | receiving actor, time | contract, product | sum of volumes | Biometria |

### G3. Harvested volumes per contractor (harvesting contractor's perspective)

| Query | Who asks | F | G | S | Source |
|---|---|---|---|---|---|
| What has each machine produced this week? | Harvesting contractor | time | `MachineKey`, *`MachineUserID`* | sum of volumes, count of stems | hpr |
| What has each operator produced? | Harvesting contractor | time | `OperatorKey`, `OperatorBusinessName` (on Stem; *on Log*) | sum of volumes | hpr |
| Production per contractor across contracts | Harvesting contractor | contractor, time | contract | sum of volumes | hpr (*logging contractor*), rights from Biometria |
| How effective is each machine, and its fuel use? | Harvesting contractor | machine, time | machine | productive time, fuel | MOM |

### G4. Individual logs and stems (detail)

| Query | Who asks | F | G | S | Source |
|---|---|---|---|---|---|
| Show every log of this stem with its measurements | Any actor | `Id` or `StemKey` | — | volumes, diameters, length, `CuttingReason`, `ProductName` | hpr |
| Where was this stem felled, and by which machine? | Any actor | `Id` | — | `Latitude`, `Longitude`, `Altitude`, `MachineKey`, *`StemNumber`* | hpr |

### G5. Diameter distribution, stem defects and harvested stems

| Query | Who asks | F | G | S | Source |
|---|---|---|---|---|---|
| Diameter distribution of the logs on this contract | Coordinator, receiver | contract, time | diameter class of `MachineDiameterTopUnderBark` (or another category) | count of logs, sum of volume | hpr |
| Diameter distribution of the stems | Coordinator | contract, time | class of *`DBH`* | count of stems | hpr |
| Why were logs cut short or downgraded? | Coordinator | contract, time | `CuttingReason`, `TopSawing` | count, volume | hpr |
| Stem defects and grades | Coordinator | contract | *stem grade* | count | hpr (grades), later |
| How many stems per species group were harvested? | Coordinator | contract, time | `SpeciesGroupName` | count of stems | hpr |

### G6. Production by document creation time

| Query | Who asks | F | G | S | Source |
|---|---|---|---|---|---|
| Production per day or week on this contract | Coordinator, contractor | contract | `HarvestingCreationTime` by day or week | sum of volumes | hpr |
| Seasonal measurement quality, e.g. February (sap) against other months | Coordinator, receiver | time by month | product, species | under-bark against over-bark volume, diameter spread | hpr |

### G7. Historical results and revisions

| Query | Who asks | F | G | S | Source |
|---|---|---|---|---|---|
| Latest version of every stem and log (documents repeat the previous last stem) | Any actor | `Id` | — | latest by `HarvestingCreationTime` | hpr |
| Results under an earlier product definition | Coordinator | `ProductVersion` | product | sum of volumes | hpr |
| Revised results after price or plan changes | Coordinator | contract | — | — | Biometria |

## Members the hpr groups need

**On the rows today** (data-model.md): `Id`, `MachineKey`, `StemKey`, `LogKey`,
`HarvestingCreationTime`, `ContractNumber`, `ObjectName`, the three volumes, the eight diameters,
`MachineLength`, unit members, `ProductKey`, `ProductName`, `ProductNamespace`, `ProductVersion`,
`CuttingReason`, `TopSawing`, `FindButtEndFunction`; on Stem: coordinates, altitude, boom
positions, `ProcessingCategory`, `SpeciesGroupKey`, `SpeciesGroupName`, `OperatorKey`,
`OperatorBusinessName`.

**Missing, for US7** (all read from the hpr document):

| Member | Row | From StanForD | Groups |
|---|---|---|---|
| `StemNumber` | Stem | `Stem/StemNumber`, a sequence per object | G4 |
| `SubObjectKey`, `SubObjectName` | Stem, Log | `Stem/SubObjectKey`, `ObjectDefinition/SubObject` | G1 |
| species group on Log | Log | its stem's `SpeciesGroupName` (or a join to Stem by `Id`) | G1, G5, G6 |
| operator on Log | Log | its stem's `OperatorKey`, `OperatorBusinessName` (or a join) | G3 |
| product destination | Log | `ProductDefinition/.../ProductDestination/BusinessName` | G2 |
| `MachineUserID` | Stem, Log | `Machine/MachineUserID` | G3 |
| logging contractor | Stem, Log | `Machine/LoggingContractor` | G3 |
| `DBH` | Stem | `SingleTreeProcessedStem/DBH` | G5 |
| User Ids | Stem, Log | `ObjectUserID`, `ProductUserID`, `SpeciesGroupUserID`, `MachineUserID` | joining Biometria later |

**Consciously left out for now** (developer, 2026-11-14): single and multi-tree processing details,
stem grades and defects.

Business and actor names come from the document; no real names are written in this repository.
