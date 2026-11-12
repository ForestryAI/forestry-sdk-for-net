# Contract: Public API changes for the deserialization context

Changes to [spec 001's public API](../../001-walking-skeleton/contracts/public-api.md); shapes from
[research.md](../research.md). Names are examples, Deferred (implementation). Every public member
gets an XML doc comment (D0). Nothing is published yet, so breaking 001's surface is allowed.

## Enumeration policies (`EnumerationPolicy`, changed, R23, R26)

| Before (001, R19) | After | Spec |
|---|---|---|
| `Row Construct(TypeDefinition type, Func<Row> next)` | `Row Construct(TypeDefinition type, DeserializationContext context, Func<Row> next)` | FR-005 |
| `void Set<T>(Row row, PropertyDefinition<T> property, T value, Action<Row, T> next)` | `void Set<T>(ContextRow row, PropertyDefinition<T> property, T value, DeserializationContext context, Action<Row, T> next)` | FR-005, FR-007 |
| `Row? Enumerate(Row row, TypeDefinition type, Func<Row, Row?> next)` | `void Complete(ContextRow row, DeserializationContext context, Action next)`; decisions are actions on `row` | FR-003, FR-003a |
| `void Clear()` | `void Clear(DeserializationContext context)`, before the context is emptied | FR-001 |

Precedence, chains, attaching and member targets (`Set` only) are unchanged from R19, and so is
the one-enumeration-at-a-time guard (FR-011).

## `DeserializationContext` (new, R20, R26)

| Member | Behavior | Spec |
|---|---|---|
| `IEnumerable<ContextRow> Rows<TRow>() where TRow : Row` | Open and kept rows of that type (or deriving), oldest first; released rows skipped; a type never kept gives an empty sequence | FR-006, edge case |
| `ContextRow Current` | The row being constructed, set or completed | FR-008 |
| `IEnumerable<ContextRow> Ancestors(ContextRow row)` | Open rows around it, innermost first | FR-008 |
| `ContextRow? Parent(ContextRow row)` | Its parent if still in the context | FR-014, FR-015 |
| `IEnumerable<ContextRow> Children(ContextRow row)`, `Siblings(ContextRow row)` | By parent id, among rows in the context | FR-015 |
| `void Release(ContextRow row)` | Only on a kept, closed row; it leaves after the current query; twice, or on an open or `UntilEnd` row, throws | FR-005, FR-003a, FR-010 |

A query never sees the context change while it runs (FR-010).

## `ContextRow` (new, R21-R23)

| Member | Behavior | Spec |
|---|---|---|
| `Row Row` | The row (the context's original) | FR-006 |
| `bool IsOpen` | Until its completion | FR-006 |
| `bool IsSet(string memberName)` | Whether the member was set, by the document, an empty node or a policy through `Set`; an unknown name throws | FR-007 |
| `void Set<TValue>(string memberName, TValue value)` | Sets through the member's setter and marks it set; on a row already enumerated throws `InvalidOperationException` naming the row type and the member | FR-005, FR-007 |
| `void Enumerate(bool enumerated)` | Only on the row being completed, during its own completion; at most once; otherwise throws (e.g. on an open parent, or a row whose completion is over) | FR-003, FR-003a |
| `void Keep(Lifetime lifetime)` | Only on the row being completed, during its own completion; at most once; otherwise throws | FR-003, FR-003a |
| `ContainerRelationship Container` | Its container relationship | FR-014 |

Decisions not taken by any policy default to: enumerated when the type was added to the options,
lifetime `Leave` (containment default). A `Relationship<TRelationship>()` lookup comes with the
keys spec (R21).

## `ContainerRelationship` (new, R21)

| Member | Behavior | Spec |
|---|---|---|
| `long Id` | Per enumeration, assigned at construction | FR-014 |
| `long? ParentId` | The innermost open row's id at construction, or null | FR-014 |

## `Lifetime` (new enum)

`Leave` (default), `UntilReleased`, `UntilEnd`.

## Options (`DeserializeOptions`, changed, R28)

| Member | Behavior | Spec |
|---|---|---|
| `int MaxContextRows` | Maximum kept rows (open rows do not count); default 100; locked with the options | FR-011a |
| Own-value member by options, e.g. `Content<TRow>(memberName)` | Same as the attribute | FR-017 |

Going past the limit throws an exception giving the limit and value, the kept rows per row type,
and how to raise it; `Clear` runs once and the context is emptied.

## Attributes (new, R30)

| Attribute | On | Meaning |
|---|---|---|
| `[Content]` | Property or field | The member set from the node's own value; at most one per type (fails at lock) |

## Row builder (changed, media-facing, R24)

| Member | Change |
|---|---|
| `Start<T>()` | unchanged signature; creates the context row (parent id) |
| `Set(member, utf8, in FieldContext)` | unchanged; also marks the member set |
| `Row? Complete()` | unchanged signature; runs the completion steps of R24; returns the row to enumerate (a copy when kept) or null |

## XML media (behavior, R29, R30)

- A row element starts its row at its start tag, empty or not (its presence may be a flag).
- Attributes of a row element set members by name (C# name or `[Field]`), converted; unmatched
  attributes are ignored.
- Text directly inside a row element sets its `[Content]` member; the last text wins; with no text
  by completion it is set empty (and marked set).
- A non-row element at any depth sets the innermost open row that has a member of its name;
  wrappers that match nothing are passed through (R29). A repeated member element sets the member
  again (last wins). Wrappers that tell values apart are added as row types (e.g. `BoomPositioning`,
  `LogMeasurement`).
