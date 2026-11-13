# Feature Specification: Harvesting rows

**Feature Branch**: `stanford-001-us2`

**Created**: 2026-11-11

**Status**: Draft

**Input**: User description: "Spec 001 "Harvesting rows" for Forestry.StanForD, from feature GitHub #90 and its stories #91, #93-#98. User stories in order: US1 C# engineering (#91, done), US2 Identification (#93), US3 Timestamps (#94), US4 Conditional mapping (#95), US5 When Harvesting values are updated, an investigation (#96), US6 Collect and group queries, ongoing (#97), US7 Log and Stem row types (#98). Scope: StanForD Harvesting (hpr) XML only; rows are flat single values for a big data format (final format is the user's); identification, timestamps and conditional mapping are policies; performance is a later spec. Follow the constitution v1.0.0. Keep implementation detail out of the spec."

## Context

Every day harvesters cut timber, pulpwood and energy wood of large economic value, and report what
they cut in StanForD Harvesting documents. Production StanForD is big data, and its analysis is
grouped, usually by an actor (an operator, a machine, a contract and the parties behind it), over
time. This specification turns Harvesting documents into flat Stem and Log rows that are globally
identified, timestamped, and filled with the right measurement values, ready for a big data format
of the user's choice.

**Actors in this spec**:
- **Developer**: deserializes Harvesting documents into rows and writes them wherever they need
  (Parquet for ADX in this POC; JSON, CSV or an application are equally valid).
- **Analyst**: queries the rows, grouped by actor and over time.

## Clarifications

### Session 2026-11-11

- Q: Without a chosen option, what happens to an occurrence matching no condition? → A: Strict by
  default: it fails (FR-012).
- Q: Which Harvesting schema versions must be read? → A: First "the latest only"; revised during
  planning to the two latest versions, checked from the root's version attribute; back to the
  latest only for the POC after US5, with no version check (FR-019, 2026-11-13).
- Q: Which timestamp partitions rows? → A: The header's `CreationDate` in UTC, then the contract
  number; the contract start date is not carried (FR-007 to FR-009, 2026-11-13).
- Q: How is a row identified as one value? → A: A key alone or in combination is globally unique;
  an identification `{machine key}-{stem key}[-{log key}]` is set when the key is read (FR-001,
  FR-002).
- Q: Whose attributes may a condition use? → A: The element's or its enclosing element's, e.g. a
  log measurement's category for its diameters (FR-010).
- Q: Are extensions interpreted? → A: No; they belong to the sending program and are an add-on
  from packages like Biometria (FR-020).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - C# engineering (Priority: P1, done)

The package can be built, tested and shipped like the SDK's other packages, with a change log and a
read-me (#91).

**Why this priority**: Nothing else can be delivered without it.

**Independent Test**: The package builds and its tests run in the SDK's continuous integration; it
packs as a shipping library.

**Acceptance Scenarios**:

1. **Given** the repository, **When** continuous integration runs, **Then** the package builds,
   its tests run (performance tests left out by default) and it packs.

---

### User Story 2 - Identification (Priority: P1)

A developer deserializes Harvesting documents from many machines and every Stem and Log row carries
a global identification, so an analyst can join, deduplicate and count rows across machines (#93).

**Why this priority**: Without global identification, rows from different machines collide (their
keys are only unique per machine) and no cross-machine analysis is correct.

**Independent Test**: Deserialize two short Harvesting documents from two machines that use the
same stem and log keys; every row is identified, and no identification occurs twice.

**Acceptance Scenarios**:

1. **Given** a Harvesting document with a machine and its stems, **When** it is deserialized,
   **Then** every Stem row carries the machine key and its stem key.
2. **Given** a stem with several logs, **When** it is deserialized, **Then** every Log row carries
   the machine key, its stem key and its log key.
3. **Given** a stem referencing common definitions (contract, species group, product, operator),
   **When** it is deserialized, **Then** the row carries each reference as the machine key plus that
   definition's key.
4. **Given** two documents from two machines using equal stem and log keys, **When** both are
   deserialized, **Then** no two rows share an identification.

---

### User Story 3 - Timestamps (Priority: P2)

Every Stem and Log row carries a timestamp traced to its source in the document, so the data stays
historical, can be placed on a timeline and can be partitioned (#94).

**Why this priority**: Time is in almost every analysis (contract progress, seasonal effects), but
how stem and log rows get their time depends on the investigation in User Story 5.

**Independent Test**: Deserialize a short Harvesting document with a header creation date; every
Stem and Log row carries it in UTC, and its source is named in this spec.

**Acceptance Scenarios**:

1. **Given** a document whose header `CreationDate` is 2026-01-14T17:14:48+01:00, **When** it is
   deserialized, **Then** every Stem and Log row carries 2026-01-14T16:14:48Z as its harvesting
   creation time.
2. **Given** a stem referencing an object with a contract number, **When** it is deserialized,
   **Then** the Stem row and its Log rows carry that contract number next to the creation time,
   the two partition values.

*Changed 2026-11-13 after US5 ([findings-us5.md](findings-us5.md)): the contract start date is set
once per object and says nothing about when a stem was cut, so it is not carried; definition
creation and modification dates are not carried either.*

---

### User Story 4 - Conditional mapping (Priority: P1)

StanForD repeats elements by name and tells them apart by the element's own attributes. A developer
gets each occurrence's value in the right member, e.g. the machine-measured log volume over bark
only from the log volume whose volume category and measurement category both match (#95).

**Why this priority**: Volumes and diameters are the economic value of the harvest; a value in the
wrong member is invisible in aggregates and worse than a missing one.

**Independent Test**: Deserialize a short log with several volumes and diameters differing only by
their attributes; each value lands in its own member, and an occurrence matching no condition is
ignored or fails according to the chosen option.

**Acceptance Scenarios**:

1. **Given** a log with three volumes differing by volume category, **When** it is deserialized,
   **Then** each volume is in the member for its category.
2. **Given** a condition on two attributes (volume category and measurement category), **When**
   only one attribute matches, **Then** the member is not set from that occurrence.
3. **Given** an occurrence matching no condition and the option to ignore, **When** it is
   deserialized, **Then** it is ignored and every other member is unaffected.
4. **Given** an occurrence matching no condition and the option to fail, or no option chosen,
   **When** it is deserialized, **Then** deserialization fails, naming the element and its
   attributes.

---

### User Story 5 - When Harvesting values are updated (Priority: P2)

Some values in a Harvesting document change while the machine works. The developer learns which,
and when, starting with whether the contract start date is updated each time the machine starts
cutting (#96).

**Why this priority**: The answer decides how timestamps (User Story 3) are interpreted, but it is
an investigation, not a deliverable rows depend on to exist.

**Independent Test**: The findings are written down with their sources and reviewed by the
developer.

**Acceptance Scenarios**:

1. **Given** the Skogforsk documentation, the Harvesting schema and real documents from one
   contract over several days, **When** the investigation is done, **Then** the contract start date
   question is answered with sources.
2. **Given** other values found to be updated during work, **When** the findings are written,
   **Then** each lists when it is updated and whether it needs a rule or a story of its own.

---

### User Story 6 - Collect and group queries (Priority: P2, ongoing)

Likely queries on log and stem data are collected in plain language, with who would ask them, and
grouped by kind, so the Log and Stem rows carry the members those queries need (#97).

**Why this priority**: Row members are chosen from their uses in queries (constitution C3); without
groups, User Story 7 has no basis.

**Independent Test**: A first grouping exists in which every group lists its queries, its actor and
the filters, groupings and summaries it needs; it can be extended after this story closes.

**Acceptance Scenarios**:

1. **Given** collected queries, **When** they are grouped, **Then** each group names its actor and
   lists the members it filters, groups or summarizes by.
2. **Given** a new query after the story closes, **When** it is added, **Then** it joins an existing
   group or starts a new one without changing the format.

---

### User Story 7 - Log and Stem row types (Priority: P3)

The developer deserializes a real Harvesting document into Stem and Log rows whose members come
from the query groups, identified (User Story 2), timestamped (User Story 3) and filled by
conditional mapping (User Story 4) (#98).

**Why this priority**: It is the deliverable the POC shows, but it depends on the stories before it.

**Independent Test**: Deserialize a real Harvesting document; the number of Stem and Log rows equals
the stems and logs in the document, and every member traces to a use in a query group.

**Acceptance Scenarios**:

1. **Given** a real Harvesting document, **When** it is deserialized, **Then** there is one Stem row
   per stem and one Log row per log.
2. **Given** the Stem and Log row types, **When** their members are listed, **Then** each is a
   single value and traces to a use in at least one query group.

---

### Edge Cases

- A document with no machine key: deserialization fails; no row can be identified (S0).
- A stem referencing a common definition not present earlier in the document: deserialization
  fails, naming the stem and the missing key (S0).
- The same stem delivered in two documents from the same machine: both rows carry the same
  identification, so it can be deduplicated downstream.
- Log keys restart for every stem: two logs with the same log key under different stems never share
  an identification.
- An element repeated by name with identical attributes in one log: ambiguous; deserialization
  fails rather than choosing one (S0).
- A very large document: rows are enumerated as the document is read; what is held does not grow
  with the number of stems or logs (C5). Measuring this is a later performance spec.

## Requirements *(mandatory)*

### Functional Requirements

**Identification (User Story 2)**

- **FR-001**: Every Stem row MUST carry the machine key, its stem key, and an identification
  combining them (`{machine key}-{stem key}`).
- **FR-002**: Every Log row MUST carry the machine key, its stem key, its log key, and an
  identification combining them (`{machine key}-{stem key}-{log key}`).
- **FR-003**: Every reference from a row to a common definition MUST carry the machine key and that
  definition's key.
- **FR-004**: Identification MUST be the same every time the same stem or log is deserialized.
- **FR-005**: A row whose identification cannot be formed MUST fail deserialization, never be
  enumerated without it.

**Timestamps (User Story 3)**

- **FR-006**: Every Stem and Log row MUST carry at least one timestamp, and this spec MUST name each
  timestamp's source in the document.
- **FR-007**: Every Stem and Log row MUST carry the harvesting creation time: the header's
  `CreationDate`, when the machine created the document, in UTC (developer, 2026-11-13). Documents
  are incremental, so it is as close to the cut time as the document allows.
- **FR-008**: *Removed 2026-11-13*: rows do not carry the contract start date; US5 showed it is set
  once per object, not when cutting restarts. It may become a dimension later.
- **FR-009**: The partition values MUST be recorded: harvesting creation time, then contract number;
  partitioning itself is out of scope.

**Conditional mapping (User Story 4)**

- **FR-010**: A member MUST be set from an element's content only when the attribute values of the
  element, or of the element enclosing it (e.g. a log diameter's measurement category on its log
  measurement), meet the member's condition; a condition MAY combine several attributes.
- **FR-011**: Every log volume and log diameter category found in the real documents MUST map to
  its own member or be deliberately ignored.
- **FR-012**: An occurrence matching no condition MUST be ignored or fail, by an option chosen by
  the developer. Without a chosen option, conditional mapping is strict: the occurrence fails
  deserialization (developer, 2026-11-11).
- **FR-013**: Two occurrences matching the same member in one row MUST fail deserialization.

**Investigation and queries (User Stories 5 and 6)**

- **FR-014**: The contract start date question MUST be answered with sources before User Story 3
  closes. *Answered 2026-11-13 in [findings-us5.md](findings-us5.md): not updated when cutting
  restarts.*
- **FR-015**: Collected queries MUST be grouped, each group naming its actor and the filters,
  groupings and summaries it needs, in a form that can be extended after User Story 6 closes.

**Row types (User Story 7)**

- **FR-016**: There MUST be a Stem row type and a Log row type; every member MUST be a single value.
- **FR-017**: Every member MUST trace to a use in at least one query group.
- **FR-018**: Deserializing a document MUST give one Stem row per stem and one Log row per log.

**Scope**

- **FR-019**: For the POC, row types follow the latest StanForD 2010 schema version only, V4p1
  (developer, 2026-11-13); the root element's version is not checked, and types are checked
  against the real documents (version 3.5), not assumed valid (A2). A schema version window (e.g.
  the two latest versions, counted in versions since Skogforsk sets no upgrade time) and a policy
  checking it are left for after the POC.
- **FR-020**: Extension elements (earmarked for the sending program) are not interpreted by this
  package; another package (e.g. for Biometria) can add its own rules for them to the same
  deserialization.

### Key Entities

- **Machine**: the harvester producing the document; identified by a globally unique key.
- **Stem**: a harvested tree; identified per machine by a running number that never resets;
  references common definitions; parent of its logs.
- **Log**: a piece cut from a stem; identified within its stem; carries length, diameters and
  volumes told apart by their attributes.
- **Common definition**: an external entity loaded into the machine (contract, species group,
  product, operator); has a User Id (external reference), a machine-created key, and a creation or
  modification timestamp.
- **Contract (object)**: the common definition giving stems their contract number and object name;
  its start date is set once per object.
- **Query group**: likely queries of one kind, usually by actor, with the filters, groupings and
  summaries they need.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every real Harvesting document used, 100% of stems and logs give a row with an
  identification, and no identification occurs twice within or across machines.
- **SC-002**: 100% of Stem and Log rows carry at least one timestamp whose source is named in this
  spec.
- **SC-003**: Zero values are set on the wrong member across the real documents; every log volume
  and diameter occurrence is in its member or deliberately ignored.
- **SC-004**: 100% of Stem and Log members trace to a use in at least one query group.
- **SC-005**: The first query grouping covers at least the five kinds named in #97 (product value,
  contract progress over time, seasonal measurement quality, operator comparisons, machine
  comparisons).
- **SC-006**: The contract start date question is answered, with at least one cited source, before
  timestamps are finished. *Met 2026-11-13 (12 real documents, [findings-us5.md](findings-us5.md)).*
- **SC-007**: An analyst can answer a query from each group using only the Stem and Log rows,
  without returning to the XML.

## Assumptions

- Rows in this spec are Stem and Log rows only; common definitions are referenced from them, not
  enumerated as rows of their own.
- Identification, timestamps and conditional mapping are interpretation rules of this package
  (constitution C4); how they are built is decided in the plan.
- Real Harvesting documents are supplied by the developer; none are in the repository yet. Tests
  use short handwritten fragments; real documents back end-to-end tests (constitution A4).
- `schemas/` holds the StanForD 2010 schemas at the latest version only, V4p1, Harvesting's
  `HarvestedProduction` included (developer, 2026-11-13); older versions are not kept for the POC.
- The real documents found are version 3.5; the POC reads them against V4p1-based types (FR-019).
- Documents are incremental and repeat the previous document's last stem; the duplicate rows share
  an `Id` and are removed downstream, not by this package ([findings-us5.md](findings-us5.md)).
- User Story 6 closes with a first grouping good enough for User Story 7; collecting continues
  afterwards.
- Performance (document size affects only time) is a later spec; this spec only keeps what is held
  from growing with stems and logs (constitution C5).
- Writing rows to storage, partitioning and the final format are the user's and out of scope.
- Priorities follow the dependency order: identification and conditional mapping can start at once;
  timestamps wait on the investigation; row types wait on the query groups.
