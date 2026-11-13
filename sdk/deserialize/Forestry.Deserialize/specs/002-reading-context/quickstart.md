# Quickstart: Validating the deserialization context

How to prove spec 002 works end to end. Shapes are in [contracts/public-api.md](contracts/public-api.md)
and [data-model.md](data-model.md); scenario ids refer to [spec.md](spec.md).

## Prerequisites

- .NET 10 SDK; repository root `sdk/deserialize`.
- `Forestry.Xml` (sdk/xml) builds; it already reads attributes.

## Run

```bash
dotnet test Forestry.Deserialize.Xml/test
```

```bash
dotnet test Forestry.Deserialize/test
```

```bash
dotnet test Forestry.Deserialize.Xml/test -s Forestry.Deserialize.Xml/test/Performance.runsettings
```

## Scenarios

| # | Document (abridged) | Options | Expected | Spec |
|---|---|---|---|---|
| 1 | `<Site><SiteKey>7</SiteKey><Order>..</Order><Order>..</Order></Site>` | `Site` kept, not enumerated; `Order` policy copies `SiteKey` | two `Order` rows carrying 7; no `Site` row | US1-1, US1-2, SC-001 |
| 2 | `<Site><Contact><Name>A</Name></Contact><Order>..</Order></Site>` | `Contact` copies `Name` onto the open site, not enumerated, not kept | the order's policy finds no contact; the site carries `A` | US1-3 |
| 3 | `<Order><Line>..</Line><Line>..</Line></Order>` | defaults | `Line`, `Line`, `Order`; the line policy sees the open order | US1-4, US2-1 |
| 4 | same as 3, `Order` enumerated and kept | consumer changes its order; a later policy tries to update the context's | the context still has the document's value; the policy's update throws (a row that came out no longer changes) | US1-11, US1-12, FR-005, FR-021 |
| 5 | `<Site><Order>..</Order><SiteKey>7</SiteKey><Order>..</Order></Site>` | order policy checks `IsSet(SiteKey)` | false for the first order, true for the second | US1-8 |
| 6 | any, stopped early, cut off, or throwing in a policy | kept rows | `Clear` once; context empty afterwards | US1-6, SC-002 |
| 7 | two documents | one options instance, one after the other; then overlapping | same rows each time, nothing carried over; the overlapping start throws | US1-10, SC-003 |
| 8 | `<Order><Line/>..<OrderNumber>5</OrderNumber></Order>` | defaults | the empty line is a row like any other (enumerated, empty); 5 set on the order | FR-002, US2-2 |
| 9 | `<Order>` with three kept lines | line policy asks for siblings | the third finds the first two, parent the order | US2-5, US2-6 |
| 10 | `<Line><Price currency="EUR" kind="net">0.59</Price>..x3</Line>` | `Price` with two `[XmlAttributeValue]` members and an `[XmlTextValue]` member | three rows, each with both values and the price | US3-1, US3-2, SC-004 |
| 11 | `<Reference type="external" />` | `Reference` with an `[XmlAttributeValue]` and an `[XmlTextValue]` member | `type` set; content empty | US3-4 |
| 12 | two policies both calling `Enumerate` on one row | | the second throws, naming the action | FR-003a |
| 13 | more kept rows than `MaxContextRows` | small limit | throws with the limit, kept rows per type and how to raise it; `Clear` once, context emptied | FR-011a |
| 14 | many enumerated rows, a few kept | Performance trait | memory flat in the number of rows | SC-005 |
| 15 | `<Order><Details><OrderNumber>5</OrderNumber></Details></Order>` | defaults; then `Details` as a row type, not enumerated, copying `OrderNumber` onto its parent | not set (0) by default; 5 with the `Details` policy | FR-013, R29 |
| 16 | `<Line><Measurement unit="kg"><Amount>3</Amount></Measurement></Line>` | `Measurement` row type, not enumerated; its policy sets the parent | `Line.WeightAmount` = 3; no `Measurement` row | FR-013, FR-016, R29 |

## Walk-through (2026-11-13, T063)

Every scenario has a test, run on both entry points, all passing on net10.0 (core 44, XML 180,
performance gate 4 of 4). Tests are in `Forestry.Deserialize.Xml/test/DeserializationContextTests.US1.cs`
(US1), `.US2.cs` (US2), `.US3.cs` (US3), `MemoryTests.cs` and spec 001's `WalkingSkeletonTests.US4.cs`.

| # | Covered by |
|---|---|
| 1 | US1 `Deserialize_ForASiteKeptAndNotEnumerated_ItShould_GiveOrdersCarryingItsKey` |
| 2 | US1 `Deserialize_ForAContactCopiedOntoItsSite_ItShould_LeaveTheContextAtItsCompletion` |
| 3 | US1 `Deserialize_ForLinesInsideAnOrder_ItShould_SeeTheOrderOpenAndGiveItAfterItsLines`; US2 `Deserialize_ForAnOrderWithTwoLines_ItShould_GiveTheLinesBeforeTheOrder` |
| 4 | US1 `Deserialize_ForAConsumerChangingAKeptOrder_ItShould_LeaveTheContextsOrderUnchanged`, `Deserialize_ForAPolicyUpdatingAnOrderThatCameOut_ItShould_Throw` |
| 5 | US1 `Deserialize_ForASiteKeyAfterTheFirstOrder_ItShould_BeSetOnlyForLaterOrders` |
| 6 | US1 `Deserialize_ToTheEnd_…`, `Deserialize_StoppedAfterTheFirstRow_…`, `Deserialize_ForAPolicyThatThrows_…`, `Deserialize_ForADocumentCutOffInsideAnOrder_ItShould_ClearOnceThenEmptyTheContext` |
| 7 | US1 `Deserialize_TwiceWithTheSameOptions_ItShould_CarryNothingOver`; spec 001 US4 `Deserialize_WhileAnotherEnumerationIsRunning_ItShould_Throw` |
| 8 | US2 `Deserialize_ForAnEmptyLine_ItShould_GiveARowWithNothingSet`, `Deserialize_ForAnOrderNumberAfterTheLines_ItShould_SetItOnTheOrder` (the row's two parts as two documents) |
| 9 | US2 `Deserialize_ForThreeKeptLines_ItShould_GiveTheThirdItsTwoSiblings`, `Deserialize_ForAnOrderWithKeptLines_ItShould_FindThemAsChildren`, `Deserialize_ForAnOrderWithLinesNotKept_ItShould_FindNoChildren` |
| 10 | US3 `Deserialize_ForALineWithThreePrices_ItShould_GiveEachItsAttributesAndAmount` |
| 11 | US3 `Deserialize_ForAnEmptyReferenceWithAnAttribute_ItShould_SetTheAttributeAndAnEmptyValue` |
| 12 | US1 `Deserialize_ForTwoPoliciesEnumeratingOneRow_ItShould_ThrowOnTheSecond` (and the other six FR-003a tests) |
| 13 | US1 `Deserialize_ForMoreKeptRowsThanTheLimit_ItShould_ThrowExplainingIt`, `Deserialize_ForMoreOpenRowsThanTheLimit_ItShould_NotThrow` |
| 14 | `MemoryTests.Deserialize_ForAMillionLinesAndTenKeptSites_ItShould_HoldNoMoreMemoryThanForAThousand` (`Category=Performance`) |
| 15 | US2 `Deserialize_ForAnOrderNumberInsideAWrapper_ItShould_NotSetItOnTheOrder`, `Deserialize_ForADetailsRowCopyingOntoItsOrder_ItShould_SetTheOrderNumber` |
| 16 | US2 `Deserialize_ForAMeasurementInKilograms_ItShould_SetTheLinesWeightAmount`; US3 `Deserialize_ForAMeasurementWithAUnitAttribute_ItShould_SetTheLinesWeightAmount` (the unit as an attribute) |

Spec 001's tests pass with their policy fixtures on `Complete` (T029), their records unchanged.

## Done when

- Every scenario above passes, sync and async entry points alike (as 001's tests do).
- 001's tests pass after their policy fixtures move from `Enumerate` to `Complete`.
