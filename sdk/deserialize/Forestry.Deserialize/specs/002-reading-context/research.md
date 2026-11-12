# Research: Deserialization context

Every item is a **proposal** (constitution C5) until the developer marks it **Confirmed**,
**Rejected** or **Deferred**. Numbering continues spec 001's research (R0-R19), so "R19" below is
001's policy surface and code comments that cite R-numbers stay unambiguous. Names in code are
examples, **Deferred (implementation)** and settled in code review, as in spec 001.

`/speckit-tasks` should not run until every item is Confirmed, Rejected or Deferred.

Statuses use spec 001's vocabulary: **Proposed**, **Confirmed**, **Rejected**, **Deferred (spec 00x)**,
**Deferred (implementation)**.

---

## R20. The deserialization context replaces `DeserializationState`

- **Question**: Where does the per-enumeration context (FR-001) live, and what becomes of 001's
  `DeserializationState` (an open-row stack, R2)?
- **Proposal**: A public `DeserializationContext`, one per enumeration, created by the `RowBuilder` (which
  is already one per enumeration) and replacing `DeserializationState`. It holds:
  - **open rows**: a stack, as today; `Start<T>()` pushes, `Complete()` pops (R7's fixed mechanics
    stay);
  - **kept rows**: a list of closed rows a policy kept (FR-003).
  Each row in it is a **context row** (R21). Two collections instead of one list with a closed
  flag: the open stack stays a pop at completion, and a row not kept never touches the list, so the
  common case (millions of enumerated, not kept rows) costs nothing more than today.
- **Alternatives**: one list with an open/closed flag per entry (the spec's POC wording): removing
  a completed row that is not kept is then a removal from the middle of the list whenever kept
  siblings were added after it started. Equivalent for queries; slower for the common case.
- **Name**: `DeserializationContext` (developer, 2026-11-12): "ReadingContext" is too close to the
  media's reader. The spec's concept is renamed to match: "deserialization context". The feature
  folder and branch keep `reading-context`.
- **Status**: Confirmed (developer, 2026-11-12)

## R21. Context rows: what the context holds per row

- **Decision**: A context row (`ContextRow`, name Deferred) wraps one row: what describes the row's
  life in the context, plus its relationships as separate typed parts (developer, 2026-11-12):

  | Holds | For |
  |---|---|
  | `Row` | the developer's row (the original; see isolation, R25) |
  | `IsOpen` | open until completion (FR-006) |
  | set members | a bitmap over the type's members (R22, FR-007) |
  | actions taken | flags: enumerated decided, lifetime decided, released (R24, FR-003a) |
  | lifetime | leave (default), until released, until the end (FR-003) |
  | `Container` | its `ContainerRelationship` (below) |

  `ContainerRelationship` (containment, US2):

  | Holds | For |
  |---|---|
  | `Id` | a sequence number per enumeration, assigned at construction |
  | `ParentId` | the id of the innermost open row at construction, or none (FR-014) |

  The context finds parent, children and siblings from it (`context.Parent(row)`, ...; R26).
- **Relationships by type, later**: each relationship group brings its own type, and a row can
  have several (a StanForD `Log` is contained in its `Stem` and references a `Product`). When the
  keys spec adds a second kind, `ContextRow` gets a lookup `Relationship<TRelationship>()`
  (e.g. `row.Relationship<KeyRelationship>()`); until then only the `Container` property exists.
- **Rejected**: one wrapper named `ContainerRelationship` (it holds more than the relationship, and
  key rows will share it); `ContextRow<TRelationship>` (one relationship per row only, and the
  context could not hold rows of different kinds in one collection).
- **Parent by id, not by reference**: a kept `Line` pointing at its `Order` object would keep the
  order in memory after it left the context, so memory would grow with rows nobody kept (C1,
  SC-005). With an id, a parent that left is simply not found (FR-015).
- **One object per row**: allocated at `Start<T>()`. Pooling them is performance work, after the
  working example (C6).
- **Status**: Confirmed (developer, 2026-11-12)

## R22. Set members as a bitmap

- **Proposal**: Each property definition already has an `Index` (its position in
  `TypeDefinition.Properties`, fixed when the options lock). A context row's set members are a
  bitmap over those indexes: one `ulong` inline for up to 64 members, an extra `ulong[]` only for
  types with more. A bit is set whenever a member is set: by the document, by an empty node
  (FR-007) or by a policy through the context (R26).
- **Asking**: `IsSet(string memberName)` by C# member name (`nameof(Site.SiteKey)`), resolved
  through the type definition; an overload by `PropertyDefinition` for policies that already have
  it. A name the type does not have throws.
- **Alternatives**: `BitArray` (an allocation per row); a `HashSet` of names (allocations and
  hashing per set).
- **Status**: Confirmed (developer, 2026-11-12; the developer's idea of 2026-11-11)

## R23. Completion: two decisions as actions, with defaults

- **Question**: 001's `Enumerate(row, type, next)` returns the row or null: one decision. FR-003
  needs two independent ones (enumerated, lifetime), FR-003a needs each taken at most once with
  several policies, and Q3 gives defaults for rows no policy decides.
- **Proposal**: Replace `Enumerate` with one completion method,
  `void Complete(ContextRow row, DeserializationContext context, Action next)`, chained as today (R19). A
  policy takes actions on the context row instead of returning a value:

  | Action | Meaning |
  |---|---|
  | `row.Enumerate(bool)` | comes out, or not |
  | `row.Keep(Lifetime)` | stays until released, or until the end |
  | `context.Release(row)` | a kept row leaves (at any later moment, R26) |

  After the chain, any decision no policy took gets the containment default (Q3): enumerated when
  its type was added to the options (`Enumerates(Type)`, as today), lifetime "leave". The built-in
  `next` does nothing; the defaults are applied by the builder, not by a policy, so they apply
  whether or not a policy calls `next`.
- **Each action at most once (FR-003a)**: the flags in R21; a second `Enumerate`, `Keep` or
  `Release` on the same row throws `InvalidOperationException` naming the row type, the action and
  that a policy already took it. `Enumerate` and `Keep` are only valid on the row being completed, during its own completion (not
  on an open parent from a child's policy, nor on a row whose completion is over; developer
  2026-11-12);
  `Release` only on a kept, closed row.
- **Alternatives**: return a small decision struct (two policies would silently overwrite each
  other, which FR-003a forbids); keep `Enumerate` returning `Row?` and add `Keep` (two methods for
  one moment; still no "at most once").
- **Status**: Confirmed (developer, 2026-11-12): each action at most once, a second one throws

## R24. Order of completion steps

- **Proposal**, in `Complete()`:
  1. pop the open row; it is now closed;
  2. run the completion chain (R23); apply defaults for untaken decisions;
  3. if kept: add it to the kept list (the limit is checked here, R28);
  4. if enumerated: hand it out; when it is also kept, hand out a copy (R25);
  5. if not kept: it has left the context before the consumer receives it (FR-004).
- **Status**: Confirmed (developer, 2026-11-12)

## R25. Isolation by a shallow copy

- **Question**: How is an enumerated row isolated from the context (FR-021)?
- **Proposal**: Only a row that is both enumerated and kept is shared, so only it needs isolation:
  the consumer gets a copy, the context keeps the original (policies may already hold it from
  queries). The copy is `MemberwiseClone()`, through an internal method on `Row`. Shallow is enough
  because members are flattened to primitives or primitive-like structs (developer, 2026-11-12),
  and strings are immutable. Rows enumerated and not kept are handed out as they are, with no
  copy (they left the context first, R24).
- **Alternatives** (from the spec review, 2026-11-12): read-only once enumerated (a frozen flag the
  setters check; cannot be enforced on plain auto-properties and leaves the context-to-consumer
  direction open); `init`-only members set by the package (constrains how rows are written, same
  gap); a documented contract only (the failure the developer wanted to avoid). Only the copy
  covers both directions without constraining row classes.
- **Policies no longer change rows that came out** (developer, 2026-11-12, R26): so the copy only
  has to cover the consumer-to-context direction; the other direction is a throw.
- **Status**: Confirmed (developer, 2026-11-12)

## R26. Policies reach the context through a parameter

- **Question**: How does a policy get the context (FR-005 to FR-010)?
- **Proposal**: Every `EnumerationPolicy` method gets the `DeserializationContext` as a parameter:
  `Construct(TypeDefinition, DeserializationContext, Func<Row>)`, `Set<T>(ContextRow,
  PropertyDefinition<T>, T, DeserializationContext, Action<Row, T>)` (a `ContextRow`, like `Complete`,
  so a set policy can ask `IsSet`, e.g. for a repeated member; developer 2026-11-12),
  `Complete(ContextRow, DeserializationContext, Action)`,
  `Clear(DeserializationContext)`. No ambient (`AsyncLocal`) context: explicit is testable, and lifting
  the one-enumeration rule later would need no change to policies.
- **Queries (FR-006, FR-008, FR-015)** on `DeserializationContext`:

  | Member | Returns |
  |---|---|
  | `Rows<TRow>()` | context rows of that type, open and kept, oldest first; LINQ over `row.Row.AsType<TRow>()` for member values (no key model) |
  | `Current` | the row being built or completed |
  | `Ancestors(ContextRow)` | open rows around it, innermost first |
  | `Parent(ContextRow)`, `Children(ContextRow)`, `Siblings(ContextRow)` | by parent id, among rows still in the context |

- **Updating members (FR-005)**: `row.Set(nameof(Site.ContactName), value)` (typed overload by
  `PropertyDefinition<T>` too) uses the member's setter and sets its bit (Q2). Plain C# assignment
  to the row still works but is not tracked as set; documented on `IsSet`. Setting a member of a
  row already enumerated throws `InvalidOperationException` naming the row type and the member: a
  row that came out no longer changes (developer, 2026-11-12, as 001 R7; no scenario found where a
  policy needs to change one, since the consumer would never see it). Open rows and kept rows that
  did not come out can be updated (e.g. totals on a kept `Machine`).
- **Queries never see the context change (FR-010)**: `Release` marks the row released; queries skip
  released rows, and the kept list is compacted after the policy call returns. Queries iterate by
  index over the count taken when they start.
- **Concurrent enumerations (FR-011)**: 001's guard stays (developer, 2026-11-12): options with
  enumeration policies serve one enumeration at a time, a second throws (R19). Several at once is
  deferred until needed; policies on one row keep running in their defined chain order.
- **Status**: Confirmed (developer, 2026-11-12)

### As implemented (US1, T019-T031, 2026-11-12)

For code review (names and shapes Deferred (implementation)):

- **`next` takes what the step needs**, so chains are composed once and allocate nothing per row:
  `Construct(TypeDefinition, DeserializationContext, Func<DeserializationContext, Row> next)`,
  `Set<T>(ContextRow, PropertyDefinition<T>, T, DeserializationContext, Action<ContextRow, T> next)`,
  `Complete(ContextRow, DeserializationContext, Action<ContextRow> next)`, `Clear(DeserializationContext)`.
  The contract's `Func<Row>`, `Action<Row, T>` and `Action` would have needed a closure per call.
- **`Rows<TRow>()` returns a snapshot** (a list sorted by construction order), so a release during a
  query cannot change it; `Release` therefore removes the row from the kept list at once.
- **`ContextRow`** also exposes `Definition` (its `TypeDefinition`); the built-in set step marks the
  member set; `Set<TValue>` uses the member's own setter (no set policies) and throws
  `ArgumentException` when `TValue` is not the member's type.
- **Limit exception**: `DeserializationContextLimitException : InvalidOperationException`, in
  `Forestry.Deserialize.Reading`.
- **Recording fixtures** keep 001's record words (`enumerate Log` at completion).

## R27. The reading position (Rejected)

- **Question**: FR-009 asked for the node name, path and depth during any policy call.
- **Proposed**: the builder's `Start<T>()` and `Complete()` take the position, exposed as
  `DeserializationContext.Position` (a `FieldContext` valid during the call).
- **Rejected (developer, 2026-11-12)**: a media path is the media's language; a policy reading it
  would have to translate it and would be tied to one media (FR-020). Policies learn where they
  are from rows (`Current`, `Ancestors`, `Parent`, FR-008, FR-015). The position stays internal to
  the media; FR-009 is removed from the spec; `Start<T>()` and `Complete()` keep their signatures.
  001's `FieldContext` on `Set` is unchanged (opt-in field metadata, R4, R15).
- **Status**: Rejected

## R28. Limit on kept rows

- **Decision basis**: Q5 (FR-011a, S1).
- **Decision**: A generic option on `DeserializeOptions`, e.g. `MaxContextRows` (name Deferred),
  counting **kept rows only** (developer, 2026-11-12); open rows do not count (their number is the
  nesting depth, a separate S1 limit carried from 001). Checked when a row is kept. Default **100**
  (developer, 2026-11-12): StanForD hpr needs the machine, products, species groups and contracts.
  Both local Rottne samples (checked 2026-11-12) keep about 16 rows: 1 machine, 9 product, 3-4
  species group, 1 object and 1 operator definitions. Locked with the options (FR-013 of 001).
- **Exception**: a dedicated exception type (name Deferred) whose message gives the limit and its
  value, the kept rows per row type, and that `MaxContextRows` on the options raises it, e.g.

  ```text
  The deserialization context keeps more rows than MaxContextRows allows (100).
  Kept: ProductDefinition (70), SpeciesGroupDefinition (30), Machine (1).
  Raise MaxContextRows on the options if the document needs more.
  ```

  The enumeration then ends as on any throw: `Clear` runs once and the context is emptied
  (FR-001; developer 2026-11-12).
- **Alternatives**: no default limit (S1 would not hold for kept rows); a byte budget (rows have
  no cheap size); counting open rows too (mixes nesting depth into a memory limit).
- **Status**: Confirmed (developer, 2026-11-12)

## R29. Containment in the XML media: member nodes, wrappers and bare nodes

- **Wrappers are common in real data** (hpr sample, checked 2026-11-12): `Stem > StemCoordinates >
  Latitude`, `Stem > SingleTreeProcessedStem > DBH`, `Log > LogMeasurement > LogDiameter`,
  `Stem > BoomPositioning > BoomAngle`. 001's rule (a member is a direct child of its row's
  element, R13 A) cannot reach them.
- **Proposal: members through wrappers (FR-013 as written)**: an element that is not a row, at any
  depth, is matched against the open rows' members from the innermost row outward; the first row
  that has the member gets it. Wrappers that match nothing are passed through (their children are
  still read). A repeated name falls under Q1 (last wins). Attributes count only on a row's own
  element (US3).
- **Wrappers with meaning become row types** (developer, 2026-11-12): when a wrapper tells repeated
  values apart, it is added as a row type that is not enumerated, and policies combine it with its
  children through the context:
  - `BoomPositioning` (attribute `boomPositioningCategory`, members `BoomAngle`, `BoomExtension`):
    being the innermost open row, it receives its own `BoomAngle`; its `Complete` policy sets the
    parent `Stem`'s `FellingBoomAngle` when the category is "Felling".
  - `LogMeasurement` (attribute `logMeasurementCategory`) around `LogDiameter` rows (attribute
    `logDiameterCategory` and own value): a `LogDiameter` policy reads its parent `LogMeasurement`
    (open, its attribute already set) and the `Log` above it, and sets e.g.
    `MachineOverBarkDiameter` from the combination.
  This is #95's conditional mapping, built from containment (US2), named and own values (US3) and
  policies (US1); no core feature for conditions.
- **Alternative**: keep direct children only; every wrapper, meaningful or not, becomes a row type
  (a `SingleTreeProcessedStem` row only to reach `DBH`).
- **Empty and bare nodes (FR-002, edge case; developer 2026-11-12)**: a row element starts its row
  at its start tag, as in 001, whether or not it turns out empty: its presence may be a flag.
- **Status**: Confirmed (developer, 2026-11-12)

## R30. Named values and own value (US3)

- **Named values**: in XML, attributes of a row's element. They match the row type's members by
  the same names as child elements (C# name, or `[Field("name")]`), so one name space per row
  type. FR-018 (a named value and a child node setting the same member fail at lock) is then 001's
  existing duplicate-name check: two members cannot share a name. A document carrying the same
  name as attribute and child sets the member twice, and the last wins (Q1). Attribute values go
  through the member's converter like any value; unmatched attributes are ignored (US3-3).
  Namespace-prefixed attributes (`xsi:...`) never match (names compare as written).
- **Own value**: one member per row type marked with a core attribute, e.g. `[Content]` (name
  Deferred), or through the options. More than one fails at lock. In XML, text directly inside the
  row's element; with mixed content (text around child elements) each text segment sets it and the
  last wins. If no text arrived by the row's completion, the own-value member is set empty and
  marked set, as for empty member nodes (US3-4, FR-017, FR-007; developer 2026-11-12).
- **Generic in the core** (FR-020): the core only knows "a member designated for the own value";
  where it comes from is the media's. Reader: `Forestry.Xml` already gives `Attribute` and `Value`
  tokens (checked 2026-11-12), so nothing waits on the XML silo.
- **Status**: Confirmed (developer, 2026-11-12)

## R31. Test placement and fixtures

- Shredding scenarios (US1-US3, edge cases) in `Forestry.Deserialize.Xml.Tests`, one file per
  story (`DeserializationContextTests.US1.cs`, ...), small documents written one element per line (C3,
  A4), with neutral fixtures `Site`, `Contact`, `Order`, `Line`, `Price`, `Reference`.
- Core-only tests in `Forestry.Deserialize.Tests`, only what needs no document (C3): the limit
  option's default (100) and lock, more than one own-value member failing at lock, the member
  name-to-index lookup. At-most-once actions, the bitmap over more than 64 members and `IsSet` with
  an unknown name run through the XML reader.
- Wrapper scenarios (R29): a member inside a wrapper with no meaning (`Order > Details >
  OrderNumber`), and a wrapper row type telling values apart and setting its parent's member
  (`Line > Measurement unit="kg" > Amount` sets `Line.WeightAmount`).
- Real StanForD confirmation stays in the combining project (C3), not in this spec.
- SC-005 extends 001's `MemoryTests` (trait `Category=Performance`): many enumerated rows, a few
  kept, memory flat.
- SC-003: two enumerations one after the other on one options instance, the second with no rows
  kept from the first; US1-10 is 001's T091 overlap test, kept.
- Existing tests touched: US4's recording policy (`Enumerate` becomes `Complete`, `Clear` takes
  the context).
- **Status**: Confirmed (developer, 2026-11-12)
