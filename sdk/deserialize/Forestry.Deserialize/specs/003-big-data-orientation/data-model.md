# Data Model: Big data orientation

Spec 003. No new runtime data: this spec changes what a row type may hold, where document names
are declared and what things are called. Entities as the developer and a format package author see
them after the change.

## Row

- **What**: an instance of a developer's class extending the abstract `Row` (namespace
  `Forestry.Deserialize`, file `src/Row.cs`); the unit enumerated.
- **Members**: every member a definition policy includes is a single value (R32).
- **Validation**: when the options lock, each included member's property definition checks that
  its type is a single value, then resolves its converter; the type definition gathers the
  failures of all its members into one exception (R33).
- **Not**: a row in the sense of row-based vs columnar formats.

## Single value

Allowed member types, after unwrapping `Nullable<T>` (R32):

| Kind | Examples | Rule |
| :--- | :--- | :--- |
| Primitive | `bool`, `char`, `int`, `long`, `double` | `Type.IsPrimitive` |
| String | `string` | the type itself |
| Enum | a developer's enum | `Type.IsEnum` |
| Text-form struct | `decimal`, `Guid`, `DateTimeOffset`, `DateOnly`, `TimeSpan`, a developer's struct | value type implementing `IUtf8SpanFormattable`, and `IUtf8SpanParsable<TSelf>` or `ISpanParsable<TSelf>` |

Formatting to UTF-8 is required so writing rows (spec 004) needs no string per value; parsing may
be from UTF-8 or chars, since reading goes through converters either way. The interfaces are only
checked when the options lock.

Rejected: classes (including row types), arrays (including `byte[]`), collections, structs without
a text form, and structs that format only to chars (`ISpanFormattable` without
`IUtf8SpanFormattable`). A converter does not change the result (FR-003).

## Property definition

| Member | Change |
| :--- | :--- |
| `Name` | Unchanged |
| `Member` | Unchanged |
| `MemberType` | Unchanged (was also `Field.Type`) |
| `ResolvedConverter` | Renamed from `Converter`; resolved by the definition itself when configured, `ResolveConverter(options)` (R33); was forwarded from `Field.Converter` |
| `Field` | Removed with the `Field` type (R35) |

## Reading information

`ReadingInformation` (was `FieldContext`), a `readonly ref struct` in `Forestry.Deserialize` (R36):

| Member | Meaning |
| :--- | :--- |
| `Depth` | Depth of the value in the document |
| `Name` | Name of the node the value comes from, as UTF-8 |
| `Path` | Path to the node, as UTF-8 |

Created by a format for each value it hands over; never stored unless a policy or option asks for
it. Its helper `ToUtf8String` for `Name` and `Path` is in `Utf8Extensions` (was `FieldContextExtensions`).

## XML document names (Forestry.Deserialize.Xml)

| Attribute | On | Says | Replaces |
| :--- | :--- | :--- | :--- |
| `XmlElementName("…")` | row type | the element its occurrences have | `[Row("…")]` |
| `XmlElementName("…")` | member | the child element its value comes from | `[Field("…")]` |
| `XmlAttributeName("…")` | member | an attribute of the row's own element | `XmlAttributeValue("…")` |
| `XmlElementText` | member | the text of the row's own element | `XmlTextValue` |

Option: `DeserializeXmlOptions.ElementText<TRow>(memberName)` (was `TextValue<TRow>`), the options
counterpart of `XmlElementText`; options win over attributes.

**Validation** when the options lock (R37):

- `XmlElementName` and `XmlAttributeName` need a non-empty name.
- A member has at most one source: two or more of the three attributes (or `ElementText<TRow>`
  with `XmlElementName` or `XmlAttributeName`) fail.
- Two members with the same name from the same source fail, as today.
- Defaults are unchanged: without an attribute, a row type is the element named like its class,
  and a member is the child element named like the member.
