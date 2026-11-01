# Quickstart: Validating the walking skeleton

Prerequisites: .NET 10 and .NET 9 SDKs; branch `spec-kit-startup`. The shredding scenarios need the
XML reader's element reading (`Forestry.Deserialize.Xml/BLOCKING.md`, phase 2) before they can pass.

Shredding scenarios run in the XML package, from `sdk/deserialize/Forestry.Deserialize.Xml`:

```bash
dotnet test test/Forestry.Deserialize.Xml.Tests.csproj
```

Scenarios needing no document (marked "core") run from `sdk/deserialize/Forestry.Deserialize`:

```bash
dotnet test test/Forestry.Deserialize.Tests.csproj
```

## Scenarios (each maps to tests written before the code, C4)

`Log : Row { LogKey: int, LogVolume: decimal }`, added to `DeserializeXmlOptions` (e.g.
`options.Rows.Add<Log>()`, member name Deferred, R12), unless stated otherwise. XML scenarios run
through both entry points (sync and async) and read rows by a plain cast.

| # | Setup | Expected | Spec |
|---|---|---|---|
| 1 | `<Root><Log><LogKey>1</LogKey><LogVolume>0.59</LogVolume></Log><Log><LogKey>2</LogKey><LogVolume>0.50</LogVolume></Log></Root>` | Two rows in order, members as given | US1-1, FR-003 |
| 2 | As 1, plus elements outside any `Log` and an `<Unknown>` element inside one | Same two rows | US1-2, FR-005 |
| 3 | `<Root />` | No rows, no error | US1-3 |
| 4 | Any row from 1 | `Type` is `Log`; `AsType<Log>()` gives 1 / 0.59; `AsType<Other>()` throws naming both | US2, FR-006 |
| 5 | `Log` without `<LogVolume>` | `LogVolume` has its default | US2-4 |
| 6 | Document 1 through the pipe entry point | Identical rows; first row handed out at its end token, before the second `Log` is read | US3, FR-009, FR-010 |
| 7 | Document 1 split into buffers at every byte position | Identical rows | US3-3, FR-010 |
| 8 | No policies | Defaults: construct and push at start, convert and set members, hand out and pop at the media's end token, no reference kept | US4-1, FR-011 |
| 9 | Test policy by attribute on `Log` | Acts once per row, in order | US4-2, FR-012 |
| 10 | Test policy by attribute and by options on the same moment (core) | Only the options' policy acts | US4-3, FR-012 |
| 11 | Change options after first use (core) | Throws | US4-4, FR-013 |
| 12 | `<LogKey>abc</LogKey>` | Throws, naming the member, its place and `Log` | FR-007 |
| 13 | Added class with a nested-class property and no converter for it, not extending `Row` (`Rows.Add(typeof(NotARow))` throws when added), with constructor parameters, with no constructor that can be chosen, or with two marked `[RowConstructor]`; or no class added (core) | Throws before any byte is read | FR-008 |
| 14 | Document cut off inside an open `Log` | Throws (from the media's reader); the open row is never handed out | Spec edge case |
| 15 | Generated documents with 1,000 vs 1,000,000 `Log` rows, rows discarded | Memory does not grow with the count | SC-003, C1 |

No fake media (C3, R11); the core's tests reference no media package (C0).
