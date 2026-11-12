# Feature Specification: Deserialization context

**Feature Branch**: `deserialize-002-context`

**Created**: 2026-11-11

**Status**: Draft

**Input**: User description: "Deserialization context (short name: reading-context). Source: GitHub issue
ForestryAI/forestry-sdk-for-net#99, 'Specify phase input': US1 Contextual rows and a reading
context, US2 Several nested row types in one pass, US3 Attributes and own content as members.
The package stays generic: it knows nothing of any standard's names, keys or policies, nor of any
one media." Revised 2026-11-11: examples made neutral, being in the context separated from being
enumerated, US3 made media-neutral; the needs that drove this spec are under Driving needs.

## Clarifications

### Session 2026-11-12

- Q: When the same member node appears twice inside one row, what happens to the member? → A: The
  last value wins; a policy can detect the repeat through set members (FR-007).
- Q: Does a member set by a policy count as set, the same as one read from the document? → A: Yes;
  set means the member has a value, whatever set it.
- Q: When a row type has no policy, does its row come out and leave the context at completion? →
  A: Yes, for containment (the only group in this spec); keys and references, and graphs, may need
  other defaults, decided in their own specs.
- Q: When more than one policy applies to a row's completion, which decides whether it comes out
  and how long it stays? → A: Several policies may apply, but each action on a row (enumerate,
  set lifetime, release) is taken at most once; a second policy taking the same action on the
  same row throws. Actions taken are tracked per row. Setting members is not such an action (the
  last value wins).
- Q: Should there be a limit on how many rows the context may hold? → A: Yes, a generic limit
  option with a default (S1); going past it throws an exception that explains which limit was
  reached, how many rows were held and by which row types, and how to raise the limit. (Refined in
  plan review: only kept rows count, default 100; FR-011a.)

## User Scenarios & Testing *(mandatory)*

The user of this feature is a developer who adds several classes extending `Row` to the options of
a media, attaches their own policies, deserializes a document and gets rows back. Some rows carry
information other rows need: a parent still open around its children, a header that never comes
out, a row that came out earlier and is looked up later. Scenarios are tested with small
hand-written documents through a real media reader (constitution C3). Examples use a neutral
`Site > Order > Line > Price` shape; nothing in the package depends on these names or on how any
one standard uses them.

### User Story 1 - Contextual rows and a deserialization context (Priority: P1)

Each enumeration has a **deserialization context** that policies share. A row is **contextual** while it is
in the context. Every row is in the context from its construction until its completion (it is
**open**). At completion its policy makes two separate decisions:

- **Enumerated**: whether the row comes out of the enumeration.
- **Lifetime**: whether the row leaves the context now (default), stays until a policy releases
  it, or stays until the enumeration ends.

Any combination is valid: a row can come out and stay, stay without coming out, come out and
leave, or neither. Policies read the context: the rows open around the current one and the rows
kept in it. Where reading is in the media (names, paths) stays the media's; policies learn where
they are from rows (parent, ancestors).

**Why this priority**: Without a shared context, every policy keeps its own state, policies cannot
see each other's rows, and state outlives the enumeration it belongs to. Any
rule that relates rows to each other (a child taking a key from its parent, a row referring to one
read earlier) reads from it.

**Independent Test**: A document with a `Site` holding a `SiteKey` and two `Order`s; `Site` is
kept and not enumerated, and a policy on `Order` copies the site key from the context onto each
order.

**Acceptance Scenarios**:

1. **Given** a policy that does not enumerate `Site` and keeps it, **When** the document is
   deserialized, **Then** no `Site` row comes out and the `Order` rows do.
2. **Given** `Site` kept in the context, **When** a policy on `Order` reads the context while an
   order is built, **Then** it finds the site and its `SiteKey`, and the order rows carry it.
3. **Given** a `Contact` inside a `Site` whose policy copies its name onto the open `Site` row,
   does not enumerate it and does not keep it, **When** a later order's policy queries the
   context, **Then** the contact is no longer there and the site carries the name.
4. **Given** an `Order` that is open while its `Line`s complete, **When** a policy on `Line` reads
   the context, **Then** it finds the open order; **and when** the order completes, **Then** it
   comes out after its lines.
5. **Given** an `Order` that is both enumerated and kept, **When** a later row's policy queries the
   context, **Then** it finds that order, closed, although it already came out.
6. **Given** rows still kept when the document ends, an enumeration stopped early, or one that
   throws, **When** the enumeration ends, **Then** the context is empty and the policies'
   end-of-enumeration step ran once.
7. **Given** a row constructed but not yet completed, **When** a policy queries the context,
   **Then** it can tell that row is still open (some members may not be set yet).
8. **Given** an open `Site` whose `SiteKey` node comes after its first `Order`, **When** that
   order's policy reads the site, **Then** it can tell `SiteKey` is not set yet; **and when** a
   later order's policy reads it, **Then** it can tell `SiteKey` is set.
9. **Given** a closed row whose document had no node for one of its members, **When** a policy
   reads it, **Then** it can tell that member was never set, although the row is closed.
10. **Given** one options instance with enumeration policies and an enumeration running, **When**
   a second enumeration starts on it, **Then** it throws, telling the caller to use one options
   instance per concurrent deserialization (001's rule, kept).
11. **Given** an `Order` that is enumerated and kept, **When** the consumer changes a member of the
    order it received, **Then** a later line's policy reading that order from the context sees the
    value as read from the document (isolation).
12. **Given** an `Order` that is enumerated and kept, **When** a later policy updates that order in
    the context, **Then** the order the consumer received is unchanged (isolation).
13. **Given** a `Line` that is enumerated and not kept, **When** the consumer receives it, **Then**
    a query of the context no longer finds it.

---

### User Story 2 - Containment: parent-child and sibling relationships (Priority: P1)

A developer adds several row types whose nodes nest (`Site > Order > Line > Price`), as in XML,
JSON or YAML. The relationships between their rows come from **containment**, the document's
structure: a row's **parent** is the row open around it, its **children** are the rows inside it,
and its **siblings** are the rows with the same parent. Rows of all the types are open at the same
time; each completes at its own end, inner rows first, in one read of the document.

**Why this priority**: Containment is the first group of relationships to support, and the one a
reader gives for free: a parent is always open before its children, so nothing refers forward.
Rows that relate to each other (US1) in nested documents are mostly parents, children or siblings
of one another.

**Independent Test**: A document with one site, two orders, each with two lines; `Order` and
`Line` come out in completion order.

**Acceptance Scenarios**:

1. **Given** `Order` and `Line` added and an order with two lines, **When** deserialized, **Then**
   the two `Line` rows come out before their `Order` row, each with its own members.
2. **Given** an `Order` member node after its lines (e.g. `OrderNumber` after the last `Line`),
   **When** deserialized, **Then** it is set on the order, not on a line.
3. **Given** a `Price` that is not enumerated inside an enumerated `Line`, **When** deserialized,
   **Then** the line is still open in the context when the price completes.
4. **Given** a document read once, **When** several row types come out, **Then** the document was
   not read again (C2).
5. **Given** an order with three lines whose policy keeps them, **When** the third line's policy
   asks for its siblings, **Then** it finds the first two lines, closed, and their parent is the
   order.
6. **Given** an order whose lines are not kept, **When** the order's policy asks for its children
   at its completion, **Then** none are found; **and given** they are kept, **Then** all are.

---

### User Story 3 - A node's own values as members (Priority: P2)

A developer's row type takes members not only from child nodes but from values the node carries
itself: named values on the node, and the node's own value. How a media exposes these is the
media's (C0); in XML they are attributes and the element's text, e.g.
`<Price currency="EUR" kind="net">0.59</Price>` gives a `Price` row with both named values and the
value.

**Why this priority**: Many formats tell repeated nodes apart by values on the node itself;
without them, rows such as `Price` carry no condition for a policy to match on.

**Independent Test**: A `Price` row type with two named-value members and one own-value member over
a line with three prices.

**Acceptance Scenarios**:

1. **Given** a row type with members named like its node's named values, **When** deserialized,
   **Then** each value is converted and set on its member.
2. **Given** a row type with a member designated for the node's own value, **When** deserialized,
   **Then** that value is converted and set on it.
3. **Given** a node with a named value no member matches, **When** deserialized, **Then** it is
   ignored.
4. **Given** an empty node with named values (XML: `<Reference type="external" />`), **When**
   deserialized, **Then** the named-value members are set and the own-value member is empty.

---

### Edge Cases

- A policy queries the context for a row type it has never kept: the query is empty, not an error.
- A policy keeps a row and releases it later in the same enumeration: later queries no longer see
  it.
- A row type and a member of another row type read from the same node name: fails before any byte
  is read (as today for two row types or two members from one node).
- An empty or bare node of a row type (XML: `<Line />` or `<Line></Line>`): a row like any other,
  since its presence may carry meaning (e.g. a flag). It is constructed and in the context while
  open, and by default enumerated and leaves at completion; anything else (e.g. setting a member on
  its parent instead) is a policy's.
- The same member node twice in one row (e.g. two `Note` nodes in one `Line`): the last value
  wins; a policy can see the member is already set (FR-007) before the second value arrives.
- Two policies on one row both decide whether it comes out (or both set its lifetime, or both
  release it): the second throws (FR-003a).
- A row of the same type nested in itself (a node containing a node of the same name): each is its
  own row, completed inner first.
- A named value and a child node with the same name, both matching members of one row type: fails
  before any byte is read.
- A document cut off while rows are open or kept: the error is raised as today, and the context is
  emptied.
- Many rows kept (e.g. tens of lookup rows): held for the enumeration only; nothing stays after
  completion unless a policy keeps it (C1).
- More kept rows than the limit allows (e.g. a wrong document with millions of kept rows): throws
  with the limit, the kept rows per row type and how to raise it, and the context is emptied
  (FR-011a, FR-001).

## Requirements *(mandatory)*

### Functional Requirements

**The deserialization context and contextual rows (US1)**

- **FR-001**: Each enumeration MUST have its own deserialization context, created when it starts and emptied
  when it ends (end, early stop or throw), after the policies' end-of-enumeration step.
- **FR-002**: Every row MUST be in the context, open, from its construction until its completion,
  whether or not it will be enumerated, including rows of empty or bare nodes.
- **FR-003**: At a row's completion its policy MUST decide whether the row is enumerated, and
  separately its lifetime in the context: leave now (default), stay until a policy releases it, or
  stay until the enumeration ends. Neither decision depends on the other. A row type with no
  policy is enumerated and leaves the context at completion (the containment default).
- **FR-003a**: Several policies MAY apply to one row, but each action on that row (enumerate, set
  lifetime, release) MUST be taken at most once; a second policy taking an action already taken
  on the same row MUST throw. The context MUST track which actions were taken on each row.
  Setting members is not such an action (FR-013).
- **FR-004**: A row whose policy does not keep it at completion MUST leave the context then, before
  it is handed out if it is enumerated.
- **FR-005**: A policy MUST be able to release a kept row and update the members of a row in the
  context.
- **FR-006**: A policy MUST be able to query the context's rows by row type and by the values of
  one or more members, and tell open from closed rows.
- **FR-007**: A policy MUST be able to tell which members of a row in the context have been set,
  separately from whether the row is closed: a closed row may have members never set, and an open
  row may have all of them set. A member set by an empty node counts as set, and so does a member
  set by a policy (FR-005).
- **FR-008**: A policy MUST be able to see the rows open around the current one (its ancestors),
  innermost first.
- **FR-009**: (Removed 2026-11-12: a media position such as an XML path would tie policies to one
  media, FR-020; policies learn where they are from rows, FR-008 and FR-015.)
- **FR-010**: A query MUST never see the context change while it runs; a release during a query
  takes effect after it.
- **FR-011**: Options with enumeration policies MUST serve one enumeration at a time, as in spec
  001: a second one starting while another runs throws. Serving several at once is deferred until
  needed.
- **FR-011a**: The number of rows kept in the context MUST be limited by a generic option with a
  default (S1; 100, research R28); open rows do not count. Going past it MUST throw an exception
  that gives the limit, the kept rows per row type, and how to raise the limit; the enumeration then
  ends as on any throw (FR-001).

**Containment (US2)**

- **FR-012**: Rows of several added types MUST be open at the same time when their nodes nest, each
  completing at its own end, inner rows first, in one read of the document.
- **FR-013**: A member node MUST set the innermost open row that has that member; when the same
  member node appears again in that row, the last value wins.
- **FR-014**: Each row in the context MUST record its parent: the innermost open row when it is
  constructed (none for a row with no open row around it).
- **FR-015**: A policy MUST be able to find a row's parent, its children and its siblings (rows with
  the same parent) among the rows in the context, open or kept; rows that already left the context
  and rows not read yet are not found.

**A node's own values (US3)**

- **FR-016**: A named value on a node MUST set the member of the row that node starts whose name
  matches the value's name (or the name given for it), converted like any member.
- **FR-017**: A row type MAY designate one member for its node's own value, converted like any
  member; a node with no own value sets it empty, as for empty member nodes today.
- **FR-018**: A row type whose named value and child node would set the same member MUST fail
  before any byte is read.

**Generic**

- **FR-019**: The deserialization package MUST NOT depend on any standard's names, keys or rules;
  the meaning of rows in the context and their lifetimes is the developer's policies'.
- **FR-020**: The deserialization context MUST NOT depend on any one media; how a media exposes a node's
  named values and own value is the media's (C0).

**Isolation (US1)**

- **FR-021**: An enumerated row MUST be isolated from the context (**isolation**): what the consumer
  does to the row it received never reaches the context or later rows, and what policies do to the
  context after the row is handed out never reaches the consumer's row.

### Key Entities

- **Deserialization context**: per enumeration; holds the open rows and the rows policies kept (closed).
- **Contextual row**: any row while it is in the context, open or kept; independent of whether it
  is enumerated.
- **Parent, children, siblings**: relationships from containment; a row's parent is the innermost
  open row when it is constructed, siblings share a parent.
- **Set members**: which members of a row have been set so far, by the document or by a policy;
  independent of open or closed.
- **Actions taken**: which actions (enumerate, set lifetime, release) policies have taken on a row;
  each at most once.
- **Isolation**: an enumerated row and the context never affect each other once the row is handed
  out.
- **Enumerated**: a policy's decision at completion that the row comes out.
- **Lifetime**: how long a row stays in the context after completion, decided by policies: none
  (default), until a policy releases it, or until the enumeration ends.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A document with a site, orders and lines gives exactly its orders and lines, each
  carrying the site's key, with no `Site` row out and the document read once.
- **SC-002**: After every enumeration end (natural, early stop, throw), the context holds zero rows.
- **SC-003**: The same options instance gives the same rows in two enumerations run one after the
  other: nothing kept in the context carries over.
- **SC-004**: A line with three prices told apart by named values gives three rows with both named
  values and the own value each.
- **SC-005**: Memory held during an enumeration grows only with open rows and rows policies keep,
  not with the number of enumerated rows (C1).

## Driving needs

This spec is generic; these are the needs that made it urgent, for traceability only. Nothing here
is a requirement on the package.

| Blocked item | Need | Met by |
| --- | --- | --- |
| StanForD spec 001 (#90), hpr only | Rows that relate to each other in one pass | US1, US2 |
| #93 Identification | A row carries keys of rows open around it (ancestors) and of definitions read earlier in the document | FR-002, FR-003, FR-006, FR-007, FR-008 |
| #94 Timestamps | A row carries a timestamp from a row it references, read earlier and not enumerated | FR-003, FR-006 |
| #95 Conditional mapping | Repeated nodes told apart by values on the node set different members | FR-016, FR-017, FR-005 |

At the plan gate, check that the generic requirements still cover these items.

## Assumptions

- For the POC, the context holds rows in a simple list queried with ordinary queries; a faster
  store comes after the working example (C6). Policies reach rows only through the context, so the
  store can change without changing them.
- A query by member values relies on the developer's own conditions over their typed rows; no
  generic key model is introduced.
- Relationships between rows come in three groups, by where they come from: **containment**
  (structure; XML, JSON, YAML), **keys and references** (matching values; CSV, SQL, Parquet, and
  also within nested documents) and **graphs** (a relationship is itself a row type). This spec
  covers containment only; keys and references come next and start from querying by member
  values (FR-006); graphs after. One format may use several groups. The deserialization context is shared
  by all groups; each adds its own way to find related rows, and may set its own defaults (e.g.
  whether a row with no policy comes out and how long it stays).
- Forward references (a row referring to one not read yet) are out of scope; containment has
  none.
- Ancestors as constructor parameters and key references (walking skeleton's later slices) are not
  in this spec; they will build on the context. Spec 001's guard stays: a row type whose chosen
  constructor has parameters fails when the options lock (001 FR-008), so no relationship can come
  from a constructor yet. Relationships are expressed with policies for now; later, policies may be
  created when type definitions are built (e.g. from constructor parameters or attributes).
- Conditions on named values are not a core feature: a developer's policy over rows in the context
  covers them.
- Today's rule that options with enumeration policies serve one enumeration at a time (R19) stays
  (FR-011, developer 2026-11-12); policies still get the context, so lifting the rule later needs
  no change to their shape.
- XML is the only media built in this spec; it exposes attributes as named values and an element's
  text as its own value. Other media map their own constructs when they are added.
