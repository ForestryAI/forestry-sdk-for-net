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

## User Scenarios & Testing *(mandatory)*

The user of this feature is a developer calling the deserialization package. A Value is one
name-value item of a document's data with dimensions (path, name, depth, ...), as defined in the
constitution. In this feature the Values are fakes created by the developer or a test; no media
reader is involved.

### User Story 1 - Shred a flat class into rows (Priority: P1)

A developer has a flat class (e.g. `Log` with `LogKey` and `LogVolume`) and a sequence of Values.
They deserialize the Values against the class and get one row per occurrence of the class in the
Values. Each row holds the class's property values by name.

**Why this priority**: This is the walking skeleton itself: the smallest end-to-end path from Values
to rows. Every later slice (several classes, ancestors, references, conditions) builds on it.

**Independent Test**: Can be fully tested with a hand-written Value sequence and one flat class,
checking the rows that come out.

**Acceptance Scenarios**:

1. **Given** a class `Log { LogKey: integer, LogVolume: decimal number }` and Values describing two
   `Log` occurrences (`LogKey` 1, `LogVolume` 0.59, then `LogKey` 2, `LogVolume` 0.50), **When** the
   Values are deserialized against `Log`, **Then** exactly two rows come out, in document order:
   `{LogKey: 1, LogVolume: 0.59}` and `{LogKey: 2, LogVolume: 0.50}`.
2. **Given** the same class and a sequence that also contains Values outside any `Log` occurrence,
   **When** deserialized, **Then** those Values are ignored and the same two rows come out.
3. **Given** a row that came out, **When** the developer asks which class it represents, **Then** it
   reports `Log`.
4. **Given** an empty Value sequence, **When** deserialized against `Log`, **Then** no rows come out
   and no error is raised.

---

### User Story 2 - Convert a row into an instance (Priority: P2)

The developer either uses a row as it is (name-value pairs) or converts it into an instance of the
class it represents.

**Why this priority**: Rows are the storage-ready form; instances are what ordinary C# code works
with. Both must be possible, but conversion only matters once rows exist (Story 1).

**Independent Test**: Given rows from Story 1, convert each one and compare the instance's property
values with the row's values.

**Acceptance Scenarios**:

1. **Given** the row `{LogKey: 1, LogVolume: 0.59}` for `Log`, **When** it is converted to `Log`,
   **Then** the instance has `LogKey` 1 and `LogVolume` 0.59.
2. **Given** a row for `Log` that has no value for `LogVolume`, **When** it is converted, **Then**
   the instance's `LogVolume` has its type's default (or null where the property allows null).
3. **Given** a row for `Log`, **When** it is converted to a different class, **Then** the conversion
   fails with an error naming both classes.

---

### User Story 3 - Synchronous or asynchronous Value enumeration (Priority: P3)

Whether the Values are enumerated synchronously or asynchronously depends on what deserialization
reads from: in-memory input (e.g. a string or a byte span) gives a synchronous enumeration, while a
stream or a pipe gives an asynchronous one. The developer gets the rows the same way: synchronously
for a synchronous enumeration, asynchronously for an asynchronous one. In this slice both are
simulated with fake Values.

**Why this priority**: Real media input arrives both ways, but the shredding behavior is identical
to Story 1, so it follows once Story 1 works.

**Independent Test**: Enumerate the same fake Values as in Story 1 asynchronously and compare the
rows with the synchronous result.

**Acceptance Scenarios**:

1. **Given** the Values from Story 1 enumerated asynchronously, **When** deserialized against
   `Log`, **Then** the same two rows come out, in the same order, as with synchronous enumeration.
2. **Given** an asynchronous Value enumeration, **When** the first `Log` occurrence has ended,
   **Then** its row is available to the developer before the Values of the second occurrence are
   consumed.

---

### Edge Cases

- A Value inside a `Log` occurrence whose name matches no property of `Log`: it is ignored.
- A Value is null (empty element): the row holds the name with a null value.
- A Value's text cannot be converted to its property's type (e.g. `"abc"` for `LogKey`): deserialization
  fails fast with an error identifying the Value (name, path) and the property.
- The class passed in has a property that is not a built-in type (e.g. a nested class): deserialization
  is rejected before any Value is consumed.
- No Value's dimensions start a structure that matches the class: no rows and no error (fail-fast
  limits come in a later slice, FR-012).
- Two consecutive occurrences of the class carry Values with identical paths: the end and start
  signals between them separate the two rows.
- The Values end while an occurrence is still open: deserialization fails fast (the structure never
  ended).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The developer MUST be able to deserialize a sequence of Values against a C# class they
  pass in, from either a synchronous or an asynchronous sequence.
- **FR-002**: Deserialization MUST only produce rows for the class the developer passed in; it never
  produces rows for a class named or chosen by the Values.
- **FR-003**: Deserialization MUST produce exactly one row per occurrence of the class in the Values,
  in the order the occurrences appear. Values carry dimensions signaling where structures start and
  end; a structure matching the class starts an occurrence, and its end closes the row.
- **FR-004**: A Value inside an occurrence MUST set the property whose name equals the Value's name.
- **FR-005**: Values outside any occurrence of the class, and Values whose name matches no property,
  MUST be ignored.
- **FR-006**: A row MUST hold columns whose names match the class's property names and whose values
  can be converted to the property types, and MUST know which class it represents.
- **FR-007**: A row MUST be convertible into an instance of its class; converting to a different
  class MUST fail.
- **FR-008**: A Value whose text cannot be converted to its property's type MUST make deserialization
  fail fast with an error identifying the Value and the property.
- **FR-009**: A class with a property that is not a built-in type MUST be rejected before any Value is
  consumed.
- **FR-010**: The Values MUST be consumed once, forward only; each row MUST become available as soon
  as its occurrence has ended, without waiting for the rest of the Values.
- **FR-011**: Synchronous and asynchronous Value enumeration with the same content MUST produce the
  same rows.
- **FR-012**: The constitution's S1 fail-fast limits (unexpected root, no match within a depth or
  number of Values, oversized Value) are NOT part of this slice; they come in a later slice.

### Key Entities

- **Class**: The developer's flat C# class; its properties have built-in types and are matched to
  Values by name.
- **Value**: One name-value item of the document's data, with dimensions (path, name, depth, ...).
  Fake in this feature.
- **Row**: The name-value pairs shredded for one occurrence of the class, knowing which class it
  represents and convertible to an instance of it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every acceptance scenario, the synchronous and asynchronous paths produce identical
  rows (100% agreement).
- **SC-002**: Converting each row back to its class reproduces every property value given in the
  Values (100% of properties in the acceptance scenarios).
- **SC-003**: Deserializing 1,000,000 Values uses no more memory than deserializing 1,000 Values,
  apart from the rows the developer chooses to keep.
- **SC-004**: A developer gets from a class and a Value sequence to instances with one deserialize
  call and one conversion per row, without any other setup.

## Assumptions

- Properties are matched to Values by name, exactly (case-sensitive); other matching rules (paths,
  conditions) belong to later slices and to media reader packages.
- Built-in types for this slice: text, boolean, integer numbers, decimal numbers and date/time.
  Nested classes, collections and custom property types are out of scope.
- Only one class per deserialization; several classes in one pass, ancestor data, key references
  and conditions are later slices (specs 002-005).
- Values are fakes; no media reader package is referenced (constitution C0, C3). Real media
  packages (Forestry.Deserialize.Xml, Forestry.Deserialize.Json) convert their tokens into these
  Values and dimensions; out of scope for this slice.
- Dimensions are facts about where a Value sits in the document (structure started or ended, path,
  name, depth); which structures become rows and which Values become columns is decided by the
  developer's class, never by the Values.
- Converting text to typed values uses invariant culture.
- Fail-fast limits on wrong input (constitution S1) are postponed to a later slice; this slice
  covers only the walking skeleton's behavior.
