# Quickstart: Validating big data orientation

How to prove spec 003 works. Shapes are in [contracts/public-api.md](contracts/public-api.md) and
[data-model.md](data-model.md); scenario ids refer to [spec.md](spec.md).

## Prerequisites

- .NET 10 SDK; repository root `sdk/deserialize`.
- Forestry.StanForD (`sdk/stanford/Forestry.StanForD`) builds against the changed packages.

## Run

```bash
dotnet test Forestry.Deserialize/test
```

```bash
dotnet test Forestry.Deserialize.Xml/test
```

```bash
dotnet test ../stanford/Forestry.StanForD/test
```

```bash
dotnet test Forestry.Deserialize.Xml/test -s Forestry.Deserialize.Xml/test/Performance.runsettings
```

## Scenarios

| # | Row type or document | Expected | Spec |
|---|---|---|---|
| 1 | Row type with `int`, `string`, enum, `decimal`, `Guid`, `DateTimeOffset`, `DateOnly`, `int?` members | options lock; rows as before | US1-1 |
| 2 | Row type with a `Measurement` class member and a `List<int>` member | lock fails once, naming the type, both members and the flat-row rule | US1-2, US1-3, SC-001 |
| 3 | Row type with an `int[]`, a `Dictionary<string, int>`, a struct without a text form, and another row type as members | lock fails, naming each | US1-3, US1-4, SC-001 |
| 4 | Composite type with a registered converter, used as a member | lock still fails | US1-5, SC-001 |
| 5 | Row type with a `List<int>` member excluded by a definition policy | lock succeeds | R33 |
| 6 | `<Tree><Len>7</Len></Tree>` read into `[XmlElementName("Tree")] class Log { [XmlElementName("Len")] int Length }` | one `Log` with `Length` 7 | US2-1, US2-2 |
| 7 | `<Price currency="EUR">12</Price>` with `XmlAttributeName("currency")` and `XmlElementText` | one row with `EUR` and 12 | US2-2a |
| 8 | A member with `XmlElementName` and `XmlElementText` | lock fails, naming the member and both attributes | US2-2b, FR-006a |
| 9 | Two members with `XmlElementName("Len")` | lock fails, naming both members and `XmlElementName` | US2-4, FR-007 |
| 10 | Reflection over Forestry.Deserialize's exported types and members | no `Field`, `RowAttribute`, `FieldAttribute`, nothing containing "Media" | SC-002 |
| 11 | All existing tests of the three packages | pass, changed only in names, namespaces and attributes | US3-5, SC-004 |

## Search check (SC-003)

From `sdk/deserialize`, outside the exclusions in research R41 (specs 001 and 002, the
constitution's amendment record, `CLAUDE.md`, `.claude/`):

```bash
grep -rniE "media|shred" --include=*.cs --include=*.md --include=*.resx Forestry.Deserialize Forestry.Deserialize.Xml | grep -vE "/specs/00[12]-|/\.claude/|/obj/|/bin/|constitution\.md"
```

Expected: no output, apart from lines of this spec's own documents that name the old words on
purpose (e.g. the rename tables).
