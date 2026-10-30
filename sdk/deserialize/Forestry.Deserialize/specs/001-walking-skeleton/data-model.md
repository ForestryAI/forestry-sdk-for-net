# Data Model: Walking skeleton

Entities follow [research.md](research.md); the R-item behind each is named. Names marked
*Deferred (implementation)* there are examples here.

## Options (existing `DeserializeOptions`, R6, R12)

Abstract; a media extends it (`DeserializeXmlOptions`) and supplies its `RowEnumerator` /
`RowAsyncEnumerator` subclasses and default converters. Owns and builds all definitions.

| Holds | Meaning |
|---|---|
| Row types | Added explicitly (only these are handed out by default), plus the row types their constructors need, added automatically (R12) |
| Row type lookup | Built when the options lock at first use: a fixed map from `Type` to type definition, per options, never global (R5) |
| Converters | By target type, with factories (R14) |
| Policies | By type, property, parameter, and options-wide (R7) |
| Generic settings | Limits come in a later slice (FR-014) |

Lists use `ObservableList<T>` while configuring; once used, the options are read-only and a change
throws (FR-013).

## Type definition (existing `TypeDefinition`, R5, R6)

| Field | Meaning |
|---|---|
| Type | The developer's class extending `Row` |
| Constructor | Chosen once: the one marked `[RowConstructor]` (public or non-public), else the single public one; built as a delegate |
| Parameter definitions | One per constructor parameter (none in this slice) |
| Property definitions | One per property or field of a built-in type |
| Policies | Resolved per pipeline slot when the definition is built (R7) |

Its job is construction only; it sets no properties. Validation when built (FR-008): not extending
`Row`, a non-built-in property, no constructor that can be chosen, more than one marked, or (this
slice) a chosen constructor with parameters, all fail before any byte is read.

## Property definition (existing `PropertyDefinition`, made generic: `PropertyDefinition<T>`, R5, R14)

| Field | Meaning |
|---|---|
| Member | The `PropertyInfo` or `FieldInfo`; its type is `T` |
| Converter | `Converter<T>`, resolved once by `T` from the options: UTF-8 text -> `T`, invariant culture; failure fails fast (FR-007) |
| Setter | `Action<Row, T>`, built once (setter delegate for a property, compiled expression for a field) |
| Field metadata | Media name, C# type, converter: always available, no cost per row (R4) |
| Policies | Resolved per slot (R7) |

A non-generic entry on the base dispatches into it, so the member pipeline needs no `T` and nothing
is boxed.

## Parameter definition (new, R5; used from spec 002/003)

| Field | Meaning |
|---|---|
| Parameter | Constructor parameter (name, type) |
| Value | A row of its type: first from the open-row stack (a partial ancestor, e.g. a `Stem`), then from retained completed rows, then the parameter's C# default value; else fail fast (S1) |
| Policies | Resolved per slot (R7) |

## Row and Field (new, R4)

| Member | Meaning |
|---|---|
| Row: typed members | The derived class's properties and fields, holding the values |
| Row: `Type`, `AsType<T>()`, `Is<T>()` | Derived class; checked cast; type check |
| Field | Metadata about one member, never a value; per-occurrence metadata only when a policy or option asks |

## Field context (new, R15)

`readonly ref struct`, handed over with each value: `Depth`, `Name`, `Path` (UTF-8 byte spans for
the text members). Nothing in it is kept unless a metadata policy copies it. Media dimensions (a
`Dimensions` member of delimited name-value pairs built from rented bytes) are Deferred.

## Deserialization state (new, R2)

One per deserialization, in the core:

| Holds | Meaning |
|---|---|
| Open-row stack | `Pipeline<T>()` pushes, `Pipeline()` pops the top; rows stay across read cycles until popped |
| Retained rows | Completed rows a constructor needs that are not open ancestors (spec 002/003); rows stored by key for key references (key-references slice) |
| Row to hand out | The row completed in the current call |

`Dispose` clears it; rows still open then are never handed out.

## Policy (new, R7)

| Field | Meaning |
|---|---|
| Pipeline and slot | Type pipeline (`Pipeline<T>()`, `Pipeline()`) or member pipeline (`Pipeline(member, utf8, context)`) |
| Target | Type, property or parameter definition, or options-wide |
| Source | Option or attribute; options win (precedence in R7) |

## Row lifecycle with built-in default policies (this slice)

```text
Pipeline<Log>()                 -> Constructed, pushed on the open-row stack
Pipeline(member, utf8, context) -> member converted and set on the top row (FR-004, FR-007)
data the media does not associate (no member) -> never handed over (FR-005)
Pipeline()                      -> Completed, handed out (becomes Current, FR-009), popped
                                   the core keeps no reference afterwards (C1)
document cut off inside a row   -> the media's reader throws; Dispose clears the open rows
```

Later slices add: rows nested on the stack (a `Log` above its partial `Stem`), parameters from open
or retained rows, retention of completed rows, early hand-out policies, and the fail-fast policy.
