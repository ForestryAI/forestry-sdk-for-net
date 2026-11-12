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
| 10 | `<Line><Price currency="EUR" kind="net">0.59</Price>..x3</Line>` | `Price` with two attribute members and `[Content]` | three rows, each with both values and the price | US3-1, US3-2, SC-004 |
| 11 | `<Reference type="external" />` | `Reference` with `[Content]` | `type` set; content empty | US3-4 |
| 12 | two policies both calling `Enumerate` on one row | | the second throws, naming the action | FR-003a |
| 13 | more kept rows than `MaxContextRows` | small limit | throws with the limit, kept rows per type and how to raise it; `Clear` once, context emptied | FR-011a |
| 14 | many enumerated rows, a few kept | Performance trait | memory flat in the number of rows | SC-005 |
| 15 | `<Order><Details><OrderNumber>5</OrderNumber></Details></Order>` | defaults | 5 set on the order through the wrapper | FR-013, R29 |
| 16 | `<Line><Measurement unit="kg"><Amount>3</Amount></Measurement></Line>` | `Measurement` row type, not enumerated; its policy sets the parent | `Line.WeightAmount` = 3; no `Measurement` row | FR-013, FR-016, R29 |

## Done when

- Every scenario above passes, sync and async entry points alike (as 001's tests do).
- 001's tests pass after their policy fixtures move from `Enumerate` to `Complete`.
