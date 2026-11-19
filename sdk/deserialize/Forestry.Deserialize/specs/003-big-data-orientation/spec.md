# Feature Specification: Big data orientation

**Feature Branch**: `deserialize-003-big-data-orientation`

**Created**: 2026-11-19

**Status**: Draft

**Input**: User description: "Spec 003, feature #125 'Big data orientation'. Use #125's Description,
Requirements and Architecture (constitution 3.1.0 already amended and committed)." Source: GitHub
issue ForestryAI/forestry-sdk-for-net#125. The SDK pipes formats from the forest industry into
formats for big data; deserialization is the pipe's mandatory step. Writing the README (#116)
showed that the package's words and some of its types say something else, so this spec fixes them
before the README is written.

## Clarifications

### Session 2026-11-19

- Q: What replaces `[Row("…")]` and `[Field("…")]` in the XML format package? → A: One attribute,
  `XmlElementName("…")`, on a row type (the element its occurrences have) and on a member (the
  child element its value comes from). Everything a format package declares carries the format's
  prefix. Names that look like System.Xml.Serialization's (`XmlElement`, `XmlAttribute`, `XmlText`,
  `XmlRoot`) are avoided, so the two are never mixed up in one file.
- Q: Do spec 002's XML attributes keep their names? → A: No; they join one family named after the
  part of the document a value comes from: `XmlAttributeValue("…")` becomes `XmlAttributeName("…")`
  and `XmlTextValue` becomes `XmlElementText` (the text of the row's own element, never a
  child's). Any two of `XmlElementName`, `XmlAttributeName` and `XmlElementText` on one member fail
  when the options lock. With none, the member name is read as a child element, never as an
  attribute, as today.
- Q: Where would a future converter attribute (like System.Text.Json's `JsonConverterAttribute`)
  live? → A: In the deserialization package, without a prefix: converters turn UTF-8 bytes into a
  value whatever the format, so the attribute is about definitions (A5 allows it). Not built in
  this spec.
- Q: What is `FieldContext` renamed? → A: `ReadingInformation`, not `FormatInformation` (decided
  during planning): depth, name and path are generic reading information, and after the POC the
  developer wants them known when the options lock, as definitions are built (research R36).

## User Scenarios & Testing *(mandatory)*

The users of this feature are:

- a **developer** who writes classes extending `Row`, adds them to the options of a format and
  gets rows back to write to a big data format (JSON Lines now, Parquet later)
- a **format package author** (today Forestry.Deserialize.Xml) who builds on the deserialization
  package
- a **reader** of the documentation (an architect or product owner) who needs to know what a row
  is and what the package does, without reading code

Behavior does not change: the same documents give the same rows. What changes is what a row type
may hold, where document names are declared, and the words used. These are breaking changes,
accepted because nothing is released yet. Scenarios are tested through the real XML format reader
over small hand-written documents (C3).

### User Story 1 - Rows are flat, and the package says so (Priority: P1)

A developer's row type can only hold single values: primitive types and single-value structs such
as `Guid` and `DateTimeOffset`. A row type with a nested object, a collection or a composite
struct as a member is rejected before any document is read, with a message that names the type,
the member and the rule. A row is the unit the package enumerates; the term has nothing to do
with row-based or columnar formats.

**Why this priority**: Flat rows are what make the output big-data ready: each member can become
one column in any format. Today only Forestry.StanForD promises it (its C0); a developer of
another standard gets no help and could build a nested row that no big data format accepts
(constitution C7).

**Independent Test**: Add a row type with a `List<int>` member to the options and lock them; the
lock fails, naming the type and the member. Add a row type with `int`, `string`, `Guid`,
`DateTimeOffset` and nullable members; the lock succeeds and a document gives its rows.

**Acceptance Scenarios**:

1. **Given** a row type whose members are all single values (including nullable ones and
   enums), **When** the options lock, **Then** they lock and deserialization gives the same rows
   as before this spec.
2. **Given** a row type with a member that is another class (including another row type),
   **When** the options lock, **Then** they fail with a message naming the row type, the member,
   its type and the flat-row rule.
3. **Given** a row type with a collection member (an array, a list, a dictionary), **When** the
   options lock, **Then** they fail the same way.
4. **Given** a row type with a struct member that is not a single value (e.g. a struct with two
   fields and no text form), **When** the options lock, **Then** they fail the same way.
5. **Given** a developer-registered converter for a composite type, **When** a row type has a
   member of that type, **Then** the options still fail: a converter does not make a member flat.
6. **Given** the package documentation, **When** a reader looks up "row", **Then** it says a row
   is the unit enumerated, its members are single values, and it has nothing to do with
   row-based vs columnar formats.

---

### User Story 2 - Document names belong to the format (Priority: P1)

A developer declares where a row type and its members are in the document with one family of
format package attributes, each named after the part of the document: `XmlElementName` (a row
type's element, or a member's child element), `XmlAttributeName` (an attribute of the row's own
element) and `XmlElementText` (the row's own element's text). The deserialization package has no attribute or option
naming a part of a document (A5), and no `Field` type: a member's type and resolved
converter belong to its property definition, and what a format reports with a value (depth, name, path) is
reading information.

**Why this priority**: `[Row]` and `[Field]` are XML names living in the generic package; a JSON or
CSV format would interpret them differently or not at all. `Field` mixes a member's converter with
format metadata, and its name suggests rows have fields, which they do not (constitution A5,
Definitions).

**Independent Test**: A document whose element names differ from the class and member names is
deserialized with names declared through the XML package's attributes, giving the same rows as
before; the deserialization package's public surface has no `RowAttribute`, `FieldAttribute` or
`Field`.

**Acceptance Scenarios**:

1. **Given** a row type whose class name differs from its element name, **When** the element
   name is declared with `XmlElementName`, **Then** its occurrences become rows as they did with
   `[Row("…")]`.
2. **Given** a member whose name differs from its child element's name, **When** the element name
   is declared with `XmlElementName`, **Then** the member is set as it was with `[Field("…")]`.
2a. **Given** members declared with `XmlAttributeName` and `XmlElementText`, **When** the document
   is read, **Then** they are set as they were with `XmlAttributeValue` and `XmlTextValue`.
2b. **Given** a member with two of `XmlElementName`, `XmlAttributeName` and `XmlElementText`,
   **When** the options lock, **Then** they fail, naming the member and both attributes.
3. **Given** no naming attribute, **When** the document is read, **Then** the class and member
   names are used, as today.
4. **Given** two members of one row type declared with the same element name, **When** the
   options lock, **Then** they fail with a message naming both members and the XML attribute to
   change (today's message names `[Field]`).
5. **Given** a property definition, **When** a developer or policy asks for the member's
   converter, **Then** the property definition gives it directly, with no `Field` in between.
6. **Given** a policy or format reading the position of a value, **When** it asks for the depth,
   name and path, **Then** it gets them as reading information, from the top namespace
   `Forestry.Deserialize`.

---

### User Story 3 - One vocabulary: format and flatten (Priority: P2)

Code, documentation and the constitution use the same words: **format** (never "media") for what
a document is written in, and **flatten** (never "shred") for turning a document into rows. Types
named after "media" are renamed after "format", and `Row` and reading information live at the top
of the package rather than in subfolders and sub-namespaces.

**Why this priority**: The words are what a README reader, a developer and the next spec all lean
on. "Media" collides with media types and streaming media; "shredding" means something else in
Parquet (nested records stored with levels) and in the README's own big data context. It comes
after US1 and US2 because it changes no behavior, but it must be done before #116 (the README).

**Independent Test**: A search of both packages' source, tests and documentation (outside the
history of specs 001 and 002, the constitution's amendment record and released changelog
entries) finds no "media" or "shred" in names or text, and all existing tests pass after the
renames.

**Acceptance Scenarios**:

1. **Given** the format package's enumerators, **When** a format package author extends the
   deserialization package, **Then** the base types and members they use are named after
   "format" (e.g. a format row enumerator), not "media".
2. **Given** the options, **When** a format package supplies its own converters, **Then** the
   member it overrides is named after "format".
3. **Given** `Row` and reading information, **When** a developer uses them, **Then** they are in
   the namespace `Forestry.Deserialize` and their files are at the top of the source folder; the
   `Rows` folder is gone.
4. **Given** the packages' README, ARCHITECTURE and CHANGELOG, and Forestry.Deserialize.Xml's,
   **When** a reader reads them, **Then** they say "format" and "flatten".
5. **Given** the existing tests of Forestry.Deserialize, Forestry.Deserialize.Xml and
   Forestry.StanForD, **When** the renames are done, **Then** they pass, changed only in names,
   namespaces and attributes.

---

### Edge Cases

- **`string`**: a single value, although a reference type; allowed.
- **Nullable members** (`int?`, `Guid?`): allowed when the underlying type is a single value.
- **Enums**: allowed; they are primitive values with a text form.
- **`byte[]` and other arrays**: rejected as collections, even though a byte array could be one
  binary column. A binary value can be supported later, deliberately.
- **A developer's own struct** (e.g. a unit-carrying measurement): allowed only if it is a single
  value with a text form (parses from and formats to text); otherwise rejected.
- **Members the type definition does not map** (whatever spec 001 leaves unmapped, e.g. members
  without a setter): not checked; only mapped members must be flat.
- **A row type deriving from another row type**: inherited members are checked like its own.
- **Several failing members in one row type**: the message names at least the first; naming all is
  preferred, decided in the plan.
- **Two of `XmlElementName`, `XmlAttributeName` and `XmlElementText` on one member**: fails when
  the options lock (FR-006a).
- **An attribute and a child element with the same name**, read into two different members (one
  with `XmlAttributeName`, one with `XmlElementName`): allowed; they are different parts of the
  document.
- **The old attributes used after the move**: code using `[Row]`, `[Field]`, `XmlAttributeValue`
  or `XmlTextValue` no longer compiles;
  there is no obsolete shim, since nothing is released.

## Requirements *(mandatory)*

### Functional Requirements

**Flat rows (US1)**

- **FR-001**: Locking the options MUST fail when any mapped member of any added row type (and of
  the row types their constructors need) is not a single value: a primitive type, `string`, an
  enum, a single-value struct with a text form (e.g. `Guid`, `DateTimeOffset`), or a nullable of
  one of these.
- **FR-002**: The failure MUST name the row type, the member, the member's type and the flat-row
  rule, and say how to fix it (e.g. copy the value onto the row as single values, or make it its
  own row type).
- **FR-003**: A converter registered for a type MUST NOT make a non-single-value member allowed.
- **FR-004**: The documentation of `Row` and of the package MUST define a row as the unit
  enumerated, with single-value members, and say it is unrelated to row-based vs columnar
  formats.

**Document names belong to the format (US2)**

- **FR-005**: The deserialization package MUST NOT have an attribute or option naming a part of a
  document. `RowAttribute` and `FieldAttribute` MUST be removed from it.
- **FR-006**: The XML format package MUST let a developer declare the element name of a row type
  and of a member, with the same defaults as today (the class or member name when not declared).
  Both are declared with one attribute, `XmlElementName("…")`, on the class or the member. Spec
  002's `XmlAttributeValue("…")` MUST be renamed `XmlAttributeName("…")` and `XmlTextValue`
  `XmlElementText`, with unchanged behavior; the XML options' `TextValue<TRow>(memberName)`, its
  options counterpart, MUST be renamed `ElementText<TRow>(memberName)` (added in plan research
  R37).
- **FR-006a**: A member with two or more of `XmlElementName`, `XmlAttributeName` and
  `XmlElementText` MUST fail when the options lock, since each says where the value comes from. A
  member with none is read from the child element named after the member.
- **FR-006b**: Every attribute and option a format package declares MUST carry the format's
  prefix (`Xml…`) and MUST NOT reuse a name from System.Xml.Serialization.
- **FR-007**: Name collisions within a row type MUST still fail when the options lock, with a
  message naming the XML attribute.
- **FR-008**: The `Field` type MUST be removed. A property definition MUST resolve and expose the
  member's converter itself, as `ResolvedConverter` (research R33); anything else `Field` carried
  (member, type) MUST be reachable from the property definition.
- **FR-009**: The per-value position a format reports (today `FieldContext`, with depth, name and
  path) MUST be named `ReadingInformation`, in the namespace `Forestry.Deserialize`. Its companion
  `FieldContextExtensions`, which extends a UTF-8 span rather than the position, MUST be named
  `Utf8Extensions`, in the same namespace (developer, 2026-11-19, research R36).

**One vocabulary (US3)**

- **FR-010**: Public and internal types and members named after "media" MUST be renamed after
  "format" (today `MediaRowEnumerator`, `MediaRowAsyncEnumerator`, `MediaConverters`, and the
  options' media region and its documentation).
- **FR-011**: Source documentation (XML doc comments), resource messages, README, ARCHITECTURE
  and CHANGELOG of both packages MUST use "format" and "flatten", not "media" and "shred".
  Released changelog entries and the history of specs 001 and 002 stay as written.
- **FR-012**: `Row` MUST move from the `Rows` folder to the top of the source folder, and the
  `Rows` folder MUST be removed. Its namespace is already `Forestry.Deserialize`.
- **FR-013**: Deserialization results MUST be unchanged: every document in the existing tests of
  both packages and of Forestry.StanForD gives the same rows with the same values.
- **FR-014**: Each breaking change MUST be recorded in the changelog of the package it breaks.

### Key Entities

- **Row**: an instance of a developer's class extending the abstract `Row`; the unit enumerated.
  Every member is a single value. Unrelated to row-based vs columnar formats.
- **Single value**: a primitive type, `string`, an enum, a single-value struct with a text form,
  or a nullable of one of these. What a big data format can store as one column.
- **Property definition**: one mapped member of a row type, with its type and resolved
  converter (no separate `Field`).
- **Format**: what a document is written in (XML now, JSON later), read by a format package.
- **Reading information**: what a format reports with each value: depth, name and path. Never a
  value, and not kept unless a policy or option asks for it.
- **Document name**: the name a row type or member has in a document; declared in the format
  package, defaulting to the class or member name.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every non-flat member kind listed in US1 (class, row type, array, list,
  dictionary, composite struct, composite type with a converter) fails before any document is
  read, and each failure names the row type and member: 7 of 7 kinds.
- **SC-002**: The deserialization package's public surface has 0 types or members named `Field`,
  `RowAttribute`, `FieldAttribute`, or containing "Media".
- **SC-003**: A search of both packages' source, tests and documentation (outside the excluded
  history) finds 0 occurrences of "media" or "shred".
- **SC-004**: 100% of the existing tests of Forestry.Deserialize, Forestry.Deserialize.Xml and
  Forestry.StanForD pass, with changes limited to names, namespaces and attributes, and the same
  rows and values out.
- **SC-005**: A reader of the package README can say what a row is, and that it is unrelated to
  columnar vs row-based formats, without reading code (checked in #116's review).

## Driving needs

For traceability only; nothing here is a requirement on the package.

| Blocked item | Need | Met by |
| --- | --- | --- |
| #116 Forestry.Deserialize README | Settled words: row, format, flatten | US1, US3 |
| #114 SDK README (follow-on) | "Through a media" and "XML media" replaced | US3 (wording follows) |
| #118 Forestry.StanForD README and constitution | Its "media" and "shreds" follow this spec's words | US3 |
| Spec 004, writing rows | Rows that are guaranteed flat when written as JSON Lines or Parquet | US1 |
| #103 Polish (post-POC) | Moving `[Row]` and `[Field]` to the format packages, deferred there by spec 002 | US2 |

## Assumptions

- Behavior is unchanged apart from the new flat-row failure; a row type that worked before this
  spec and is flat keeps working.
- How a single-value struct is recognized (e.g. by a text-parsing and text-formatting contract) is
  a plan decision; the rule is the constitution's C7.
- Forestry.StanForD's row types are already flat (its C0 and `RowShapeTests`), and it uses none of
  `Field`, `[Field]`, `[Row]` or `FieldContext`. It does use `XmlAttributeValue` and `XmlTextValue`
  (12 places), so their renames reach its source mechanically, with no behavior change; found
  during planning (research R40). Its documentation and constitution follow this spec's words
  through #118.
- The SDK README and `sdk/deserialize/CLAUDE.md` (a superseded design note) say "media"; the
  README's wording is updated as a documentation task of this spec, the design note stays as
  history.
- `DeserializerKind`, left from the design before spec 001 and unused, keeps its place; only its
  documentation's "shredding" changes. Whether it stays is decided in a later spec.
- No obsolete attributes or type forwards are kept: nothing has been released.
- A converter attribute will likely be needed later (see Clarifications); it is not in this spec.
  A converter only one format needs is still registered through that format's options.
- Forestry.Deserialize must not depend on Forestry.Turn (Forestry.Core); a client wraps
  deserialization as the pipe's step. Nothing in this spec changes that.
