# Data Model: Deserialization context

Entities follow [research.md](research.md); the R-item behind each is named. Names are examples,
Deferred (implementation). Entities from spec 001 not listed here are unchanged
([001 data model](../001-walking-skeleton/data-model.md)).

## Deserialization context (new, replaces `DeserializationState`, R20, R26)

One per enumeration, created by the `RowBuilder`, emptied when the enumeration ends (FR-001).

| Holds | Meaning |
|---|---|
| Open rows | Stack of context rows, innermost on top; `Start<T>()` pushes, `Complete()` pops |
| Kept rows | List of closed context rows a policy kept, oldest first; released rows are skipped by queries and compacted after the policy call |
| Next id | Sequence for context row ids |
| Limit | From the options: maximum kept rows (R28) |

Queries (FR-006, FR-008, FR-015): `Rows<TRow>()`, `Current`, `Ancestors(row)`, `Parent(row)`,
`Children(row)`, `Siblings(row)`. Actions: `Release(row)`.

## Context row (new, R21-R23)

| Field | Meaning |
|---|---|
| Row | The developer's row (the original, R25) |
| Definition | Its type definition (member names, indexes) |
| IsOpen | True until `Complete()` pops it |
| Set members | Bitmap by property index: `ulong`, plus `ulong[]` above 64 members (R22) |
| Actions taken | Flags: `Enumerated`, `Lifetime`, `Released` (R23, FR-003a) |
| Enumerated | The decision, once taken or defaulted |
| Lifetime | `Leave` (default), `UntilReleased`, `UntilEnd` |
| Container | Its `ContainerRelationship` |

Operations: `IsSet(memberName)`, `Set(memberName, value)` (sets and marks), `Enumerate(bool)`,
`Keep(Lifetime)`. A `Relationship<TRelationship>()` lookup comes with the keys spec, when a row can
have a second kind of relationship (R21).

## Container relationship (new, R21)

| Field | Meaning |
|---|---|
| Id | Per enumeration, assigned at construction |
| ParentId | Innermost open row's id at construction, or none (FR-014) |

## Lifetime (new enum, R23)

| Value | Meaning |
|---|---|
| `Leave` | Leaves the context at completion (default for containment) |
| `UntilReleased` | Kept until a policy releases it, at the latest the end of the enumeration |
| `UntilEnd` | Kept until the enumeration ends; `Release` throws |

## Type definition (changed, R22, R30)

| Adds | Meaning |
|---|---|
| Member index by name | C# member name to property index, for `IsSet` and `Set` |

## Options (changed, R26, R28)

| Change | Meaning |
|---|---|
| Context limit | Generic option, default 100, locked with the options |
| Overlap guard | Unchanged from 001 (FR-011): one enumeration at a time with enumeration policies |

## Row lifecycle (this spec)

```text
row node starts (even empty) -> Start<T>(): construct (Construct chain), new context row
                              (id, parent id), push
member node / named value  -> Set(member, utf8, context): convert, set, mark its bit
attribute / element text   -> Set(member, ...): the XML association maps them to members (R30)
node ends                  -> Complete(): pop (closed); Complete chain; defaults for
                              untaken decisions; kept -> kept list (limit checked);
                              enumerated -> handed out (a copy when also kept)
enumeration ends           -> Clear(context) once on every policy; context emptied
```

## State transitions of a context row

```text
Open --Complete()--> Closed --(not kept)--> gone (before hand-out)
                     Closed --(kept)------> Kept --Release()--> gone (after the current query)
                                            Kept --enumeration ends--> gone
```
