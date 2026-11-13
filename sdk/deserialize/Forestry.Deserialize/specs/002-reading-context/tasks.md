---

description: "Task list for the deserialization context (spec 002)"
---

# Tasks: Deserialization context

**Input**: Design documents from `specs/002-reading-context/` (plan.md, spec.md, research.md R20-R31,
data-model.md, contracts/public-api.md, quickstart.md) and `.specify/memory/constitution.md` 3.0.0.

**Prerequisites**: every research item is Confirmed or Rejected (research.md, 2026-11-12).

**Tests**: Required (constitution C4), as in spec 001. In every story phase: test shells first, one
per acceptance scenario, quickstart row or edge case; then a **test review gate** (the developer
reviews the shells on the board before any code); then implementation. A story's shells may not
compile until its implementation declares the surface they call (accepted in 001).

- **XML shells are theories over both entry points** (`[InlineData(EntryPoint.Sync)]`,
  `[InlineData(EntryPoint.Async)]`) through `Forestry.Deserialize.Xml/test/EntryPoints.cs`, so sync
  and async agree.
- **Each test reads its own local document**, one element per line, with `// Arrange`, `// Act`,
  `// Assert` comments (A4).
- **Performance gate at the end of every story**: `dotnet test -s Performance.runsettings` on both
  test projects (001's convention).
- **Fixtures use neutral names** (`Site`, `Contact`, `Order`, `Line`, `Price`, `Reference`, ...):
  nothing in the package depends on a particular media (FR-020), and relationships between rows
  come only from containment, i.e. physical nesting, in this slice (US2, spec Assumptions; R31).

**Organization**: Phases by user story in priority order: US1 (P1), US2 (P1), US3 (P2). No
Foundational phase: C4 puts every story's shells before any of its code (as in 001). US1 replaces
001's `DeserializationState` and policy signatures, so US2 and US3 build on it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: The user story the task belongs to (US1, US2, US3)
- Paths are relative to `sdk/deserialize/`: the core is `Forestry.Deserialize/`, the media package
  is `Forestry.Deserialize.Xml/`

## External prerequisites

- None. `Forestry.Xml` already gives `Attribute` and `Value` tokens (research R30, checked
  2026-11-12).

## Not in this slice

- Keys and references, graphs, forward references, and the `Relationship<TRelationship>()` lookup
  (spec Assumptions, R21).
- Several enumerations at once on one options instance (FR-011: 001's guard stays).
- The reading position for policies (R27, Rejected; FR-009 removed).
- Ancestors as constructor parameters (spec Assumptions). 001's guard stays: a chosen constructor
  with parameters fails when the options lock (`Forestry.Deserialize/src/Definitions/TypeDefinition.cs`,
  test `Deserialize_ForAConstructorWithParameters_ItShould_ThrowBeforeAnyByteIsRead` in
  `Forestry.Deserialize/test/DefinitionValidationTests.cs`); 001's T023 note that it "becomes an
  accepted one" from spec 002/003 is deferred to the constructor-parameters slice. Relationships come
  from policies for now; later, policies may be created when type definitions are built.
- S1 limits other than kept rows (depth, value length), carried from 001 (plan Complexity Tracking).
- Names marked Deferred (implementation): `ContextRow`, `ContainerRelationship`, `Lifetime` values,
  `MaxContextRows`, the limit exception type, `[XmlAttributeValue]`, `[XmlTextValue]`, `TextValue<TRow>`.
  The names below are the research examples, confirmed in code review.

---

## Phase 1: Setup

None: the projects, test projects, runsettings and `EntryPoints` helper exist from spec 001.

---

## Phase 2: User Story 1 - Contextual rows and a deserialization context (Priority: P1) 🎯 MVP

**Goal**: Each enumeration has a public `DeserializationContext`; policies get it as a parameter,
decide at completion whether a row is enumerated and how long it stays (each action at most once),
query rows, see set members, and enumerated rows are isolated from the context.

**Independent Test**: A `Site` with a `SiteKey` and two `Order`s; `Site` kept and not enumerated; a
policy on `Order` copies the site key from the context onto each order (quickstart 1).

### Test shells for User Story 1 ⚠️

- [X] T001 [P] [US1] Fixtures in `Forestry.Deserialize.Xml/test/Fixtures/`: `Site.cs` (`int SiteKey`, `string? Name`, `string? ContactName`), `Contact.cs` (`string? Name`), `Order.cs` (`int OrderNumber`, `int SiteKey`, `string? Note`), `Line.cs` (`int LineNumber`, `int OrderNumber`, `string? Note`), `WideRow.cs` (70 `int` members `M00`-`M69`, for the bitmap above 64 members, "one `ulong` inline for up to 64 members, an extra `ulong[]` only for types with more"), and test policies in `ContextPolicies.cs` (keep and do not enumerate `Site` with `Lifetime.UntilEnd`; copy `SiteKey` from the kept site onto each `Order`; copy `Contact.Name` onto the open `Site` through `context.Ancestors(row)`, not enumerated, not kept; enumerate and keep `Order`; a configurable recording policy reporting what it saw)
- [X] T002 [US1] Test shell, US1-1 / US1-2 / SC-001 (quickstart 1): no `Site` row comes out, two `Order` rows carry `SiteKey` 7, document read once, in `Forestry.Deserialize.Xml/test/DeserializationContextTests.US1.cs`
- [X] T003 [US1] Test shell, US1-3 (quickstart 2): the contact's name is on the site; a later order's policy finds no `Contact` in `context.Rows<Contact>()`, in `DeserializationContextTests.US1.cs`
- [X] T004 [US1] Test shell, US1-4 / US1-7 (quickstart 3): a `Line` policy finds its `Order` open (`IsOpen` true) through `context.Ancestors(row)`; rows come out `Line`, `Line`, `Order`, in `DeserializationContextTests.US1.cs`
- [X] T005 [US1] Test shell, US1-5: an `Order` enumerated and kept is found closed by a later row's policy although it came out, in `DeserializationContextTests.US1.cs`
- [X] T006 [US1] Test shell, US1-6 / SC-002 (quickstart 6): with rows kept, after a natural end, an early stop, a throw in a policy, and a document cut off inside a row, `Clear(context)` ran exactly once per enumeration and the context held zero rows when it ran out (the recording policy records the count `Clear` saw before emptying, and a later enumeration on the same options finds none), in `DeserializationContextTests.US1.cs`
- [X] T007 [US1] Test shell, US1-8 / US1-9 (quickstart 5): `IsSet(nameof(Site.SiteKey))` is false for the first order's policy and true for the second when `SiteKey` comes after the first order; on a closed row with no node for a member it stays false; a member set by an empty node and one set by a policy through `row.Set(...)` count as set (FR-007, Q2), in `DeserializationContextTests.US1.cs`
- [X] T008 [US1] Test shell, FR-007 / R22: `WideRow` with members `M00`, `M63`, `M64` and `M69` in the document reports exactly those as set; `IsSet("NoSuchMember")` throws naming the member and the row type, in `DeserializationContextTests.US1.cs`
- [X] T009 [US1] Test shell, US1-11 / US1-12 / US1-13 / FR-021 (quickstart 4): the consumer changing its `Order` does not change the value a later `Line` policy reads from the context; a later policy's `row.Set(...)` on the kept order, already enumerated, throws `InvalidOperationException` naming `Order` and the member (FR-005); a `Line` enumerated and not kept is not in `context.Rows<Line>()` when the consumer receives it (checked from the next row's policy), in `DeserializationContextTests.US1.cs`
- [X] T010 [US1] Test shell, FR-003a (quickstart 12): two policies both calling `row.Enumerate(...)` on one row throw `InvalidOperationException` naming the row type and the action; likewise two `Keep`; `Enumerate` or `Keep` outside the row's own completion throws (during a set, on an open parent from a child's policy, on a closed kept row from a later policy); `Release` twice, on an open row, or on a row kept `UntilEnd` throws, in `DeserializationContextTests.US1.cs`
- [X] T011 [US1] Test shell, Q1 / FR-013: a repeated member node in one row leaves the last value, and a `Set` policy receiving the `ContextRow` sees `IsSet` true before the second value is set, in `DeserializationContextTests.US1.cs`
- [X] T012 [US1] Test shell, FR-003 defaults (Q3): a row type with no policy is enumerated and leaves the context; a policy that only calls `Keep(Lifetime.UntilReleased)` still gets the default "enumerated when its type was added", in `DeserializationContextTests.US1.cs`
- [X] T013 [US1] Test shells, edge cases / FR-005 / FR-010: `context.Rows<T>()` for a type never kept is empty (no throw); a row kept `UntilReleased` and released by a later policy is not seen by queries after it; a `Release` while iterating `Rows<T>()` does not change that iteration, only later ones, in `DeserializationContextTests.US1.cs`
- [X] T014 [US1] Test shell, FR-011a (quickstart 13): with `MaxContextRows = 3` and four kept rows the enumeration throws the limit exception, whose message contains the limit (3), the kept rows per row type and the words `MaxContextRows`; open rows do not count (a document nesting five rows deep with nothing kept passes); `Clear` ran once and the context is empty, in `DeserializationContextTests.US1.cs`
- [X] T015 [US1] Test shell, US1-10 / SC-003 (quickstart 7): two enumerations one after the other on one options instance give the same rows, the second seeing no rows kept by the first and reusing no row object from it (`Assert.NotSame`); 001's overlap tests (T091 in `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US4.cs`) stay as US1-10, updated only for the new `Clear(context)` signature, in `DeserializationContextTests.US1.cs`
- [X] T016 [P] [US1] Core test shells in `Forestry.Deserialize/test/DeserializeOptionsTests.cs`: `MaxContextRows` defaults to 100 ("Default **100**"); changing it after the options lock throws (FR-013 of 001); in `Forestry.Deserialize/test/DefinitionValidationTests.cs`: a type definition looks up a member index by C# member name, and an unknown name is not found
- [X] T017 [P] [US1] Performance test shell, SC-005 / C1 (quickstart 14): 1,000,000 `Line` rows enumerated and not kept plus 10 kept rows; memory sampled every 100,000 rows stays flat, trait `Category=Performance`, in `Forestry.Deserialize.Xml/test/MemoryTests.cs`
- [X] T018 [US1] **Test review gate**: the developer reviews T001-T017 on the board and closes the review task before T019 (C4) (developer, 2026-11-12: passed with changes: a row only has what has been read so far, in any forward-only read (spec Assumptions, T007); `ContextPolicy` marked test-only, captured state recorded on #103; setting a member of a row that came out throws (FR-005, T009 rewritten plus a shell for kept rows that did not come out); `Enumerate` and `Keep` only on the row being completed, in its own completion (three more T010 shells); T015 asserts no row object is reused)

### Implementation for User Story 1

- [X] T019 [P] [US1] Add `Lifetime` in `Forestry.Deserialize/src/Reading/Lifetime.cs`: values "`Leave` (default), `UntilReleased`, `UntilEnd`" (data-model.md) (depends on T018)
- [X] T020 [US1] Add `ContextRow` in `Forestry.Deserialize/src/Reading/ContextRow.cs`: `Row`, its `TypeDefinition`, `IsOpen`, set members as "`ulong`, plus `ulong[]` above 64 members" by property `Index`, actions-taken flags (`Enumerated`, `Lifetime`, `Released`), the enumerated decision and lifetime; `IsSet(string)` / `IsSet(PropertyDefinition)`, `Set<TValue>(string, TValue)` through the member's setter marking its bit, throwing on a row already enumerated (FR-005), `Enumerate(bool)` and `Keep(Lifetime)` valid only on the row being completed, during its own completion, and at most once, throwing otherwise (R21-R23) (depends on T019)
- [X] T021 [US1] Add `DeserializationContext` in `Forestry.Deserialize/src/Reading/DeserializationContext.cs`, replacing `Forestry.Deserialize/src/Reading/DeserializationState.cs` (delete it): open-row stack of `ContextRow`, kept list oldest first, `Rows<TRow>()` (open and kept, released skipped, iterating by index over the count taken at start), `Current`, `Ancestors(row)` innermost first, `Release(row)` marking and compacting after the policy call, kept-row limit check when a row is kept, `Clear()` emptying (R20, R26, R28) (depends on T020)
- [X] T022 [P] [US1] Add the limit exception and messages in `Forestry.Deserialize/src/Throwing.cs` and `Forestry.Deserialize/src/Formatting.Resource.cs`: limit and value, kept rows per row type, "Raise MaxContextRows on the options"; at-most-once and wrong-moment action messages naming row type and action; `IsSet` unknown member (R23, R28) (depends on T018)
- [X] T023 [P] [US1] Add the member name-to-index lookup to `Forestry.Deserialize/src/Definitions/TypeDefinition.cs` (R22) (depends on T018)
- [X] T024 [P] [US1] Add `MaxContextRows` (default 100, locked with the options) to `Forestry.Deserialize/src/DeserializeOptions.cs` (R28); keep `BeginEnumeration` / `EndEnumeration` and the overlap guard in `Forestry.Deserialize/src/DeserializeOptions.Caching.cs` (FR-011) (depends on T018)
- [X] T025 [US1] Change `Forestry.Deserialize/src/Policies/EnumerationPolicy.cs`: `Construct(TypeDefinition, DeserializationContext, Func<Row>)`, `Set<T>(ContextRow, PropertyDefinition<T>, T, DeserializationContext, Action<Row, T>)`, `Complete(ContextRow, DeserializationContext, Action)` replacing `Enumerate`, `Clear(DeserializationContext)`; update the override check and XML docs (remove the remark that `Enumerate` returns the row) (R23, R26) (depends on T021)
- [X] T026 [US1] Compose the new chains in `Forestry.Deserialize/src/Policies/PolicyResolver.cs` and `Forestry.Deserialize/src/Policies/TypeChains.cs`: `Complete` chain with a built-in `next` that does nothing; `Set` chain passing the `ContextRow` and context (depends on T025)
- [X] T027 [US1] Rework `Forestry.Deserialize/src/Reading/RowBuilder.cs`: `Start<T>()` creates and pushes a `ContextRow`; `Set` marks the member's bit (empty values included); `Complete()` follows R24 (pop; run the `Complete` chain; default untaken decisions to "enumerated when the type was added, lifetime `Leave`"; if kept, add to the kept list with the limit check; if enumerated, return it, a shallow copy when also kept; a row not kept has left before it is returned); `Clear()` calls every policy's `Clear(context)` once, then empties the context (R24, R25) (depends on T021, T026)
- [X] T028 [P] [US1] Add an internal shallow copy (`MemberwiseClone`) to `Forestry.Deserialize/src/Rows/Row.cs` (R25) (depends on T018)
- [X] T029 [US1] Update 001's policy fixtures and tests to the new signatures: `Forestry.Deserialize.Xml/test/Fixtures/RecordingPolicy.cs`, `RecordingSetPolicy.cs`, `Forestry.Deserialize/test/Fixtures/MarkerPolicies.cs`, `Forestry.Deserialize/test/PolicyPrecedenceTests.cs`, `Forestry.Deserialize/test/PolicyOptionsTests.cs`, `Forestry.Deserialize.Xml/test/WalkingSkeletonTests.US4.cs` (`Enumerate` → `Complete` with actions, `Clear()` → `Clear(context)`); records stay as before (depends on T027)
- [X] T030 [US1] XML doc comments (D0) on every new or changed public member: `DeserializationContext`, `ContextRow`, `Lifetime`, `MaxContextRows`, the limit exception, the `EnumerationPolicy` methods; `IsSet` documents that plain C# assignment is not tracked (depends on T027)
- [X] T031 [US1] Run `dotnet test` on both test projects: T002-T016 pass on both entry points, every spec 001 test still green; then the performance gate `dotnet test -s Performance.runsettings` on both test projects passes, T017 included (depends on T029, T030) (2026-11-12: passed on net10.0: core 44, XML 135 (US1 36 methods x 2 entry points), 0 build warnings; performance gate 4 of 4, sync and async; the core project has no performance tests)

**Checkpoint**: The deserialization context works end to end; MVP of this spec.

---

## Phase 3: User Story 2 - Containment: parent-child and sibling relationships (Priority: P1)

**Goal**: Nested row types complete inner first in one pass; each row records its parent
(`ContainerRelationship`); policies find parent, children and siblings; members are direct
children of their row's node, and values inside wrappers are reached through wrapper row types and
policies (R29, option A).

**Independent Test**: One site, two orders, each with two lines; `Order` and `Line` come out in
completion order, each line's parent is its order.

### Test shells for User Story 2 ⚠️

- [X] T032 [P] [US2] Fixtures in `Forestry.Deserialize.Xml/test/Fixtures/`: `Part.cs` (nested in itself, `int PartNumber`), `Measurement.cs` (`string? Unit` and `decimal Amount`, both child elements), `Line.cs` gains `decimal WeightAmount`; policies written in place with US1's test-only `ContextPolicy` (no `ContainmentPolicies.cs`, as in US1); the `Details` and `Extra` wrappers are plain elements in the documents
- [X] T033 [US2] Test shell, US2-1 (quickstart 3): two `Line` rows before their `Order`, each with its own members, in `Forestry.Deserialize.Xml/test/DeserializationContextTests.US2.cs`
- [X] T034 [US2] Test shell, US2-2 / FR-002 (quickstart 8): `OrderNumber` after the last line is set on the order; an empty `<Line />` is a row like any other (enumerated, its members unset), in `DeserializationContextTests.US2.cs`
- [X] T035 [US2] Test shell, US2-3: a `Price`-like row not enumerated inside an enumerated `Line` sees the line open when it completes (use `Measurement` until US3), in `DeserializationContextTests.US2.cs`
- [X] T036 [US2] Test shell, US2-4 / C2: several row types come out of one forward pass; the async entry point over a counting stream reads each byte once, in `DeserializationContextTests.US2.cs`
- [X] T037 [US2] Test shell, US2-5 / US2-6 (quickstart 9): the third kept line finds the first two as siblings, closed, with the order as parent; the order's completion policy finds all kept lines as children, and none when they are not kept, in `DeserializationContextTests.US2.cs`
- [X] T038 [US2] Test shell, FR-014 / FR-015: the outermost row has no parent (`ParentId` null); a kept line whose order was not kept finds no parent after the order completed, in `DeserializationContextTests.US2.cs`
- [X] T039 [US2] Test shell, edge case: a `Part` nested in a `Part` gives two rows, inner first, the inner one's parent the outer one, in `DeserializationContextTests.US2.cs`
- [X] T040 [US2] Test shells, R29 option A (quickstart 15, 16): `<Order><Details><OrderNumber>5</OrderNumber></Details></Order>` leaves `OrderNumber` unset (0) by default; with fixture `Details` (`int OrderNumber`) added as a row type, not enumerated, whose policy copies `OrderNumber` onto its parent, the order gets 5; `<Line><Measurement><Unit>kg</Unit><Amount>3</Amount></Measurement></Line>` gives `Line.WeightAmount` 3 and no `Measurement` row, in `DeserializationContextTests.US2.cs`
- [X] T041 [US2] **Test review gate**: the developer reviews T032-T040 on the board before T042 (C4) (developer, 2026-11-12: passed with one change: members stay direct children of their row's element, values inside wrappers are reached through wrapper row types and policies (R29 option A); T040 reworked, a `Details` fixture added, the two-wrappers test removed)

### Implementation for User Story 2

- [X] T042 [US2] Add `ContainerRelationship` (`long Id`, `long? ParentId`) in `Forestry.Deserialize/src/Reading/ContainerRelationship.cs` and `ContextRow.Container`; `RowBuilder.Start<T>()` assigns the next id and the innermost open row's id as parent (R21) (depends on T041) (2026-11-12: done; `ContainerRelationship` is a `readonly record struct (long Id, long? ParentId)`, the id being the construction order)
- [X] T043 [US2] Add `Parent(row)`, `Children(row)`, `Siblings(row)` to `Forestry.Deserialize/src/Reading/DeserializationContext.cs`, by parent id among open and kept rows; a row that left the context is not found (FR-015) (depends on T042) (2026-11-12: done; snapshots like `Rows<TRow>()`; rows with no parent are siblings of each other)
- [X] T044 [US2] Check `Forestry.Deserialize.Xml/src/XmlRowReading.cs` against R29 option A: members stay direct children of their row's element (001's depth check stays); an element that is neither a row nor a direct-child member sets nothing, and rows inside it are still found by name (as today); no code change expected, the T040 shells confirm it (depends on T041) (2026-11-12: checked, no code change: the depth check already keeps members to direct children and rows are found by name inside wrappers; T040 passes)
- [X] T045 [US2] XML doc comments (D0) for `ContainerRelationship`, `Container` and the new context methods (depends on T043)
- [X] T046 [US2] Run `dotnet test` on both test projects: T033-T040 pass on both entry points, US1 and spec 001 still green; then the performance gate passes (depends on T044, T045) (2026-11-12: passed on net10.0: core 44, XML 162, 0 build warnings; performance gate 4 of 4. One fix in the test helper only: T036's counting stream wraps a `MemoryStream` instead of deriving from one, whose async reads called its sync reads and counted each byte twice)

**Checkpoint**: Containment relationships work, wrappers through row types and policies; US1 unaffected.

---

## Phase 4: User Story 3 - A node's own values as members (Priority: P2)

**Goal**: A row type takes members from its element's attributes (named values) and its own text,
declared with the XML package's attributes (`[XmlAttributeValue]`, `[XmlTextValue]`); the core gets
no source vocabulary (R30).

**Independent Test**: A `Price` row type with two attribute members and one text member over
a line with three prices (quickstart 10).

### Test shells for User Story 3 ⚠️

- [X] T047 [P] [US3] Fixtures in `Forestry.Deserialize.Xml/test/Fixtures/`: `Price.cs` (`[XmlAttributeValue("currency")] string? Currency`, `[XmlAttributeValue("kind")] string? Kind`, `[XmlTextValue] decimal Amount`), `Reference.cs` (`[XmlAttributeValue("type")] string? Type`, `[XmlTextValue] string? Value`), `PriceWithDuplicateName.cs` (two `[XmlAttributeValue("currency")]` members), `PriceWithTwoTextValues.cs` (two `[XmlTextValue]` members), `PriceWithTextOption.cs` (`[XmlTextValue] decimal Amount`, `decimal Other`), `MeasurementByAttribute.cs` (`[Row("Measurement")]`, `[XmlAttributeValue("unit")] string? Unit`, `decimal Amount`), leaving US2's `Measurement.cs` unchanged
- [X] T048 [US3] Test shell, US3-1 / US3-2 / SC-004 (quickstart 10): a line with three `<Price currency=".." kind="..">..</Price>` gives three rows, each with both attributes converted and the amount, in `Forestry.Deserialize.Xml/test/DeserializationContextTests.US3.cs`
- [X] T049 [US3] Test shell, US3-3: an attribute no member matches, and a prefixed `xsi:` attribute, are ignored, in `DeserializationContextTests.US3.cs`
- [X] T050 [US3] Test shell, US3-4 / FR-017 (quickstart 11): `<Reference type="external" />` sets `Type`; `Value` is empty and `IsSet(nameof(Reference.Value))` is true; text around child elements leaves the last text, in `DeserializationContextTests.US3.cs`
- [X] T051 [US3] Test shells, FR-018: `PriceWithDuplicateName` fails when the options lock, naming both members; a child element `<currency>` does not set the attribute member `Currency`, which keeps the attribute's value, in `DeserializationContextTests.US3.cs`
- [X] T052 [US3] Test shell, R29 with attributes: `<Line><Measurement unit="kg"><Amount>3</Amount></Measurement></Line>` gives `Line.WeightAmount` 3 (the attribute is set when `Amount` arrives), in `DeserializationContextTests.US3.cs`
- [X] T053 [US3] Test shells, FR-018 / R30 (XML, not core: sources are the media's): `PriceWithTwoTextValues` fails when the options lock, naming the class; `options.TextValue<PriceWithTextOption>(nameof(Other))` wins over `[XmlTextValue]` on `Amount`, in `DeserializationContextTests.US3.cs`
- [X] T054 [US3] **Test review gate**: the developer reviews T047-T053 on the board before T055 (C4) (developer, 2026-11-12: passed with changes: last text wins for now, joining text around child elements deferred (R30); each member has one source, declared with the XML package's own attributes, not the core's (R30, FR-016-FR-018); T051 flipped, T053 moved to the XML tests; `[Row]`/`[Field]` debt recorded on #103)

### Implementation for User Story 3

- [X] T055 [P] [US3] Add `XmlAttributeValueAttribute` and `XmlTextValueAttribute` in `Forestry.Deserialize.Xml/src/` and `TextValue<TRow>(string memberName)` on `Forestry.Deserialize.Xml/src/DeserializeXmlOptions.cs` (locked with the options; options win over the attribute) (R30) (depends on T054) (2026-11-12: done, names as proposed)
- [X] T056 [US3] Extend `Forestry.Deserialize.Xml/src/XmlAssociation.cs`: per row type, child-element members (no marker, or `[Field]`), attribute members (`[XmlAttributeValue]`) and at most one text member, each looked up by name in its own map; duplicates per source and more than one text member fail when the options lock (R30, FR-018) (depends on T055) (2026-11-12: done; a type or member named in `TextValue` that the options do not have fails at lock too)
- [X] T057 [US3] Extend `Forestry.Deserialize.Xml/src/XmlRowReading.cs`: `Attribute` and `Value` tokens on a row's own element set its attribute members; text directly inside a row element sets its text member, last text wins; at the row's end, if no text arrived, set the text member empty (marked set) before `Complete()` (R30, FR-017) (depends on T056) (2026-11-12: done; spacing-only text is never a token (the reader skips it), so indentation does not set a text member)
- [X] T058 [US3] XML doc comments (D0) for the two XML attributes and `TextValue<TRow>` (depends on T055)
- [X] T059 [US3] Run `dotnet test` on both test projects: T048-T053 pass on both entry points, US1, US2 and spec 001 still green; then the performance gate passes (depends on T057, T058) (2026-11-12: passed on net10.0: core 44, XML 180 (US3 9 methods x 2 entry points), 0 build warnings; performance gate 4 of 4)

**Checkpoint**: All three stories work independently.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [X] T060 [P] How-to section "The deserialization context" in `Forestry.Deserialize/README.md` (D0): keeping and enumerating rows, set members, containment, wrappers as row types, attributes and element text with the XML package's attributes, the kept-row limit; update "Changing behavior with policies" for `Complete` and the context parameter (2026-11-13: done, section "Rows that need each other: the deserialization context" with an XML subsection for wrappers, attributes and text; policies section, intro, failing fast and status aligned)
- [X] T061 [P] Update `Forestry.Deserialize/CHANGELOG.md` and `Forestry.Deserialize.Xml/CHANGELOG.md` (breaking: `Enumerate` replaced by `Complete`, policy methods take the context) (2026-11-13: done; nothing was released, so both stay under 1.0.0-beta.1 (Unreleased): spec 002 under Features Added, and the reshaped policy surface under Other Changes rather than Breaking Changes)
- [ ] T062 [P] Mark spec 001's research R19 `Enumerate` and the one-row `DeserializationState` as superseded by this spec's R20, R23 and R26, and add "As implemented" notes to this spec's research.md for names settled in code review (G0)
- [ ] T063 Walk every row of `quickstart.md` against the tests (1-16) and set spec.md's status from Draft to Implemented (depends on T059)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none
- **US1 (Phase 2)**: shells, review gate, then implementation; replaces 001's state and policy
  signatures, so 001's policy tests turn red between T025 and T029
- **US2 (Phase 3)**: depends on US1 (the context and `ContextRow` it extends); its shells can be
  written while US1 is implemented
- **US3 (Phase 4)**: depends on US1; independent of US2 except T052, which uses R29's wrapper row type
  (`Measurement`)
- **Polish (Phase 5)**: after the stories it documents

### Within Each Story

- Test shells, then the **test review gate**, then implementation (C4)
- Core types before the builder, the builder before the media, then docs, then the test run and
  performance gate

### Parallel Opportunities

- US1: T001, T016, T017 (different projects and files); after the gate T019, T022, T023, T024, T028
- US2: T032 alongside US1's implementation; after the gate T042 and T044
- US3: T047; after the gate T055, then T056-T057 in order
- Polish: T060, T061, T062

## Parallel Example: User Story 1

```text
# Shells, in parallel:
T001 fixtures (Forestry.Deserialize.Xml/test/Fixtures/)
T016 core shells (Forestry.Deserialize/test/)
T017 performance shell (Forestry.Deserialize.Xml/test/MemoryTests.cs)

# After the review gate (T018), in parallel:
T019 Lifetime        T022 Throwing/messages     T023 member index lookup
T024 MaxContextRows  T028 Row shallow copy
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. US1 shells, review gate, implementation (T001-T031)
2. **Stop and validate**: quickstart 1-7 and 12-14; spec 001 still green
3. Unblocks StanForD #93 (identification) and #94 (timestamps) for rows kept at the document level

### Incremental Delivery

1. US1 → the context, actions, isolation, limit
2. US2 → containment, wrappers through row types and policies (unblocks #95's wrapper cases such as `BoomPositioning`)
3. US3 → attributes and element text, declared with the XML package's attributes (completes #95: `LogMeasurement` / `LogDiameter`)

## Notes

- [P] tasks touch different files and depend on no incomplete task
- Each review gate is a board task the developer closes (C4, C5); story issues on the board carry
  scope and design decisions in their Architecture section and are kept in sync with this file
- Names marked Deferred (implementation) are confirmed in code review
- Commit after each task or logical group
