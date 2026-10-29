# Feature Specification: Walking skeleton

**Feature Branch**: `spec-kit-startup`

**Created**: 2026-10-21

**Status**: Draft

**Input**: User description: "Walking skeleton (short name: walking-skeleton). Source: GitHub issue
ForestryAI/forestry-sdk-for-net#51, 'Specify phase input':
- Deserializing a sequence of fake Values (sync or async) against a C# class the developer passes in
  produces rows: one row per occurrence of the class in the Values, holding the class's property
  values by name
- A row can be converted into an instance of the class (row.AsType<T>())
- Classes are flat with properties of built-in types (e.g. string, integer)
- A Value has dimensions (e.g. path, name, depth) that delineate where a row starts, which property
  a Value sets, and where a row ends"

**Input amended 2026-10-26** (developer, design discussion): no media-neutral Value model; the
deserialization package owns entry points, options, enumerators, definitions, policies and `Row`,
and a media package supplies its own reader. A row is an instance of the developer's class, which
extends `Row`. Behavior is tested through the real XML reader, not fakes. Policies at row lifecycle
moments are part of the design now (constitution 2.0.0).

## User Scenarios & Testing *(mandatory)*

The user of this feature is a developer who registers a class extending `Row` in the options of a
media (XML in this feature), passes a document and those options to the deserialization package's
entry point, and gets rows back as the abstract `Row`. A row is an instance of the developer's
class whose typed members hold the values shredded for one occurrence of the class in the document.
Scenarios are tested with small hand-written XML documents through the real XML reader
(constitution C3).

### User Story 1 - Shred a flat class into rows (Priority: P1)

A developer has a flat class extending `Row` (e.g. `Log` with members `LogKey` and `LogVolume`) and
a document. They register the class in the options, deserialize the document and get one row per
occurrence of the class in the document, each holding its typed values.

**Why this priority**: This is the walking skeleton itself: the smallest end-to-end path from a
document to rows. Every later slice (several classes, ancestors, references, conditions) builds on
it.

**Independent Test**: A small hand-written XML document and one flat class, checking the rows that
come out.

**Acceptance Scenarios**:

1. **Given** a class `Log : Row { LogKey: integer, LogVolume: decimal number }` and a document with
   two `Log` occurrences (`LogKey` 1, `LogVolume` 0.59, then `LogKey` 2, `LogVolume` 0.50), **When**
   the document is deserialized with `Log` registered, **Then** exactly two rows come out, in document
   order, with those member values.
2. **Given** the same class and a document that also contains data outside any `Log` occurrence,
   **When** deserialized, **Then** that data is ignored and the same two rows come out.
3. **Given** a document with a root and no `Log` occurrence, **When** deserialized with `Log` registered,
   **Then** no rows come out and no error is raised.

---

### User Story 2 - Work with rows without knowing their class (Priority: P2)

Deserialization returns rows as the abstract `Row`. The developer asks a row which class it is and
converts it to that class.

**Why this priority**: Once several classes come out of one pass (later slices), callers handle a
stream of mixed rows; the conversion must exist from the start.

**Independent Test**: Take rows from Story 1, ask their type, convert them, compare member values.

**Acceptance Scenarios**:

1. **Given** a row from Story 1, **When** the developer asks its type, **Then** it reports `Log`.
2. **Given** that row, **When** it is converted with `AsType<Log>()`, **Then** the result is the
   `Log` with `LogKey` 1 and `LogVolume` 0.59.
3. **Given** that row, **When** it is converted to a different class, **Then** the conversion fails
   with an error naming both classes.
4. **Given** a `Log` occurrence without `LogVolume`, **When** deserialized, **Then** the row's
   `LogVolume` has its type's default (or null where the property allows null).

---

### User Story 3 - Synchronous or asynchronous input (Priority: P3)

In-memory input (a string or bytes) is read synchronously; a stream or a pipe is read
asynchronously. The developer gets rows the same way: synchronously for synchronous input,
asynchronously for asynchronous input.

**Why this priority**: Real input arrives both ways, but the shredding is identical to Story 1.

**Independent Test**: Read the document from Story 1 from a pipe and compare the rows with the
synchronous result.

**Acceptance Scenarios**:

1. **Given** the document from Story 1 read asynchronously, **When** deserialized with `Log` registered,
   **Then** the same rows come out, in the same order, as with synchronous input.
2. **Given** asynchronous input, **When** the first `Log` occurrence has ended, **Then** its row is
   available to the developer before the data of the second occurrence is read.
3. **Given** the document split into several buffers at arbitrary byte positions (including inside a
   name or a value), **When** deserialized, **Then** the same rows come out.

---

### User Story 4 - Change behavior with policies (Priority: P2)

Behavior at a row's lifecycle moments (construction, member set, end, hand-out, retention) is set by
policies. Built-in defaults give Story 1's behavior. The developer can attach a policy to a type or
a property, by attribute on the class or through the options; options win over attributes.

**Why this priority**: Document-specific behavior (e.g. StanForD) must be a policy, never hard-coded
(constitution C3); the policy seams must exist before the first special case lands.

**Independent Test**: Attach a test policy by attribute, by options, and by both, and check which one
acts.

**Acceptance Scenarios**:

1. **Given** no policies attached, **When** Story 1's document is deserialized, **Then** the built-in
   defaults apply: a row is constructed at its start, members are set as they are read, and the row
   is handed out at its end (the media's end token for it) and not kept by deserialization
   afterwards.
2. **Given** a policy attached to `Log` by attribute, **When** deserialized, **Then** it acts at its
   lifecycle moment for each `Log` row, in document order.
3. **Given** a policy attached to the same moment of the same type or property both by attribute and
   through the options, **When** deserialized, **Then** only the options' policy acts.
4. **Given** options already used for a deserialization, **When** a policy or other setting is
   changed on them, **Then** the change fails with an error.

---

### Edge Cases

- Data inside a `Log` occurrence whose name matches no member of `Log`: it is ignored.
- An empty element for a member: the member is null (or its type's default where null is not
  allowed).
- A member's text cannot be converted to its type (e.g. `"abc"` for `LogKey`): deserialization fails
  fast with an error identifying the member, its place in the document, and the class.
- The registered class has a property that is not a built-in type (e.g. a nested class), does not
  extend `Row`, has no constructor that can be chosen (several public ones and none marked, or more
  than one marked), or (in this slice) its chosen constructor has parameters: deserialization is
  rejected
  before any byte is read.
- No row type is registered: deserialization is rejected before any byte is read.
- Two consecutive `Log` occurrences: each becomes its own row.
- The document ends while an occurrence is still open: deserialization fails fast, and the open
  occurrence never comes out as a row.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The developer MUST be able to deserialize a document through the deserialization
  package's entry point, passing the options of a media in which classes extending `Row` are
  registered, or found in an assembly the developer names; from in-memory input (synchronous) or a stream or pipe (asynchronous). The entry point
  takes no type argument and returns the abstract `Row`.
- **FR-002**: Deserialization MUST only produce rows of the classes chosen through the options
  (registered, or found in an assembly the developer names); it never creates a class named or
  chosen by the document, and never scans assemblies the developer did not name.
- **FR-003**: Deserialization MUST produce exactly one row per occurrence of the class, in document
  order.
- **FR-004**: Data inside an occurrence MUST set the member with the same name, with a typed value.
- **FR-005**: Data outside any occurrence, and data whose name matches no member, MUST be ignored.
- **FR-006**: Rows MUST be returned as the abstract `Row`; a row MUST report its class and convert
  to it with `AsType<T>()`; converting to a different class MUST fail naming both classes.
- **FR-007**: A member whose text cannot be converted to its type MUST make deserialization fail fast
  with an error identifying the member, its place in the document and the class.
- **FR-008**: Before any byte is read, deserialization MUST reject: no registered class; a
  registered class with a property that is not a built-in type, that does not extend `Row`, for
  which no constructor can be chosen (several public constructors and none marked as the one to
  use, or more than one marked), or (in this slice) whose chosen constructor has parameters.
- **FR-009**: The document MUST be read once, forward only; each row MUST become available as soon
  as its policies hand it out (by default at its end, as the media reads it), without waiting for
  the rest of the document.
- **FR-010**: Synchronous and asynchronous input with the same content, however it is split into
  buffers, MUST produce the same rows.
- **FR-011**: Row lifecycle moments (construction, member set, child row start, end, hand-out,
  retention) MUST be policy points. Built-in default policies MUST give FR-003 to FR-005.
- **FR-012**: Policies MUST be attachable to type, property and parameter definitions both by
  attribute and through the options. For the same moment and target, the options MUST win over
  attributes.
- **FR-013**: Options MUST become read-only when first used for a deserialization; a later change
  MUST fail.
- **FR-014**: The constitution's S1 fail-fast limits (unexpected root, no match within a depth or
  number of bytes, oversized value) are NOT part of this slice; they come in a later slice. The S1
  case "a row needing ancestor data not yet read" comes with the ancestors slice.

### Key Entities

- **Document**: The input, in one media (XML in this feature).
- **Row**: The abstract base of the developer's classes. A row is an instance of such a class whose
  typed members hold the values shredded for one occurrence; it reports its class and casts to it.
- **Field**: Metadata about one member of a row (e.g. its media name or path); never a value.
  Per-occurrence metadata is kept only when a policy or option asks for it.
- **Policy**: Behavior at a row lifecycle moment, attached to a type or property by attribute or
  options.
- **Options**: The media's options (extending the deserialization package's abstract options),
  carrying the row classes (registered, or found in a named assembly), converters, policies and
  the generic shredding settings; read-only once used.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every acceptance scenario, synchronous and asynchronous input produce identical
  rows (100% agreement).
- **SC-002**: Every member value given in the acceptance scenarios' documents is found on the rows
  after `AsType<T>()` (100%).
- **SC-003**: Deserializing a document of 1,000,000 rows uses no more memory than one of 1,000 rows,
  apart from the rows the developer chooses to keep.
- **SC-004**: A developer gets from a class and a document to typed rows with one deserialize call
  and one conversion per row, without any other setup than creating the media's options and
  registering the class.

## Assumptions

- Matching in this slice is by name, exactly: an element named like the class (or the name given by
  the generic row attribute) starts an occurrence; a child element named like a property (or the
  name given by the generic field attribute) sets that member. Media-specific conditions (e.g. XML
  attribute conditions) belong to the media package and later slices.
- Built-in types for this slice: text, boolean, integer numbers, decimal numbers and date/time.
  Nested classes, collections and custom property types are out of scope.
- Only one class per deserialization. Several classes in one pass, ancestors (a child row's
  constructor taking its ancestor row, e.g. `Log(Stem stem)`), key references and conditions are
  later slices (specs 002-005). Parameter definitions are part of the design now but carry no
  ancestor or lookup in this slice: classes have a parameterless constructor. The ancestors slice
  specifies its own fail-fast checks on constructor parameters (research R5).
- The XML media package (Forestry.Deserialize.Xml) is the media for this slice; JSON is a later
  slice and must need no change to the deserialization package's abstractions.
- This slice depends on the XML reader reading elements and their text. That reading is not
  finished (Forestry.Deserialize.Xml `BLOCKING.md`, phase 2); the slice's acceptance tests cannot
  pass before it is.
- Converting text to typed values uses invariant culture.
