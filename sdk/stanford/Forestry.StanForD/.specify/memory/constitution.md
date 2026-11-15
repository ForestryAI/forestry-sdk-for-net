# Forestry StanForD Constitution

Rules no design decision may override, for the StanForD silo (Forestry.StanForD) only. Each rule
names the spec-kit area it covers (Architecture, Code quality, Security, Documentation, Workflow).
Terms are defined under Definitions. Agreed by the developer in #90 (2026-11-11).

**Scope**: the package `Forestry.StanForD` in `sdk/stanford/Forestry.StanForD/` and its tests:
Harvesting row types and the policies interpreting StanForD Harvesting (`hpr`) XML. Out of scope:
reading XML and building rows (governed by the reader and deserialization constitutions); other
StanForD document families; StanForD's non-XML format; writing rows to storage; SDK engineering
(governed by `eng/`).

## Core Principles

### C0. XML to a big data format

- **Covers**: Architecture
- **Rule**: Row types MUST be flat: every member is a single value, a primitive C# type or a
  single-value struct that parses from and formats to a byte span or string (e.g. `Guid`,
  `DateTimeOffset`). Never composite structs, nested objects or collections. No type mirrors
  StanForD's hierarchy.
- **Rationale**: Production StanForD is big data; a single value per member could become one column
  in a column-based format (Parquet read by ADX in this POC). The final format (Parquet, JSON, CSV,
  an application) is the user's.
- **Checked by**: A test over every row type's members.

### C1. Every row identified

- **Covers**: Architecture
- **Rule**: Every row type MUST carry a global identification: machine key plus the keys making the
  row unique (stem; stem and log; definition).
- **Rationale**: Rows from many machines are joined, deduplicated and counted; keys are only unique
  per machine.
- **Checked by**: Tests per row type over documents from two machines with equal keys.

### C2. Every row timestamped

- **Covers**: Architecture
- **Rule**: Every row type MUST carry at least one timestamp, traced to its source in the Harvesting
  document.
- **Rationale**: Big data stays historical; a subset of timestamps partitions the data.
- **Checked by**: Tests per row type; the spec names each timestamp's source.

### C3. Members serve query groups

- **Covers**: Architecture
- **Rule**: Every row member MUST trace to a use in at least one query group, usually by actor: a
  filter, a grouping, a summary or similar.
- **Rationale**: Analytics is by group; members chosen from the schema alone grow columns nobody
  queries.
- **Checked by**: The spec traces each member to its use(s) in its group(s).

### C4. Interpretation as policies

- **Covers**: Architecture
- **Rule**: StanForD interpretation (identification, timestamps, conditional mapping) MUST be
  policies on `Forestry.Deserialize`. The package never reads XML itself. Generic XML attributes
  creating such policies may replace them later.
- **Rationale**: One reader and one deserializer for every media; StanForD knowledge stays in its
  package.
- **Checked by**: Project references; no reader use in `src/`.

### C5. Size affects only time

- **Covers**: Architecture
- **Rule**: A policy's state MUST NOT grow with the number of stems or logs. It may hold the machine
  key, the current stem and the loaded common definitions.
- **Rationale**: Documents from live machines can be very large (bad settings, poor mobile
  coverage); CPU and memory per row must stay flat.
- **Checked by**: A performance test comparing a small and a large document.

### C6. Test-first

- **Covers**: Code quality
- **Rule**: Each assertion row MUST get a test shell before code is written. The developer reviews
  the shells in a test review task before any code.
- **Rationale**: Same as the other silos.
- **Checked by**: Every assertion row maps to a test; the review task is closed before coding.

### C7. Design ownership

- **Covers**: Workflow
- **Rule**: The developer owns the constitution and every spec-kit input. AI decisions are proposals
  until the developer confirms them.
- **Rationale**: Same as the other silos.
- **Checked by**: Confirmed decisions; review tasks closed by the developer.

### D0. Public documentation

- **Covers**: Documentation
- **Rule**: XML doc comments on every public member; README and ARCHITECTURE describe the rows,
  their identification, timestamps and policies.
- **Rationale**: The package is public on GitHub.
- **Checked by**: The build warns on missing XML docs; the pages match the code.

### S0. No silently wrong values

- **Covers**: Security
- **Rule**: A value MUST NOT be set on a member it does not belong to. Content that matches no
  condition is ignored or fails, as the policy's option says, never guessed.
- **Rationale**: A wrong volume or diameter in big data is worse than a missing one: it is invisible
  in aggregates.
- **Checked by**: Tests for unmatched and ambiguous occurrences, both options.

## Additional Constraints

- **A0** (Code quality): Targets the SDK's supported target frameworks, as defined in the engineering
  constitution (`eng/.specify/memory/constitution.md`, Definitions).
- **A1** (Architecture): References only `Forestry.Deserialize` and `Forestry.Deserialize.Xml` among
  SDK packages; no third-party NuGet dependencies.
- **A2** (Architecture): Rows and policies follow Skogforsk's published schemas in `schemas/`; a type
  built against an older schema version is checked, not assumed valid.
- **A3** (Workflow): Builds follow the SDK's shared engineering (`Directory.Build.props`, `eng/`,
  `doc/dev/`), referenced, not copied.
- **A4** (Code quality): Every test labels its steps `// Arrange`, `// Act`, `// Assert`. A test
  reads its own short Harvesting fragment; real Harvesting documents back the end-to-end and
  performance tests. Slow tests carry the trait `Category=Performance`, are left out of the default
  run by the test project's `.runsettings`, and gate the end of every user story.

## Definitions

- **Harvesting document**: A StanForD 2010 Harvesting (`hpr`) XML document.
- **Row type**: A class extending `Forestry.Deserialize.Row`, flat like a record, enumerated by
  deserialization.
- **Common definition**: An external entity loaded into the machine (contract/object, species group,
  product, operator, ...).
- **User Id**: A common definition's external reference.
- **Key**: A running number created by the machine for a stem, log or common definition; the machine
  key is a GUID.
- **Identification**: The members making a row globally unique: machine key plus the row's keys.
- **Timestamp**: A creation or modification time carried to a row from what it references.
- **Conditional mapping**: Setting a member from an element's content when the element's own
  attribute values meet a condition.
- **Single value**: A member value that is one value, not composed of others: a primitive C# type or
  a struct like `Guid` or `DateTimeOffset` that parses from and formats to a byte span or string.
- **Query group**: Likely queries grouped by kind, usually by actor; row members are chosen from
  their filters, groupings, summaries and similar uses.
- **Actor**: Who or what analysis is grouped by: an operator, a machine, a contract and the parties
  behind it.

## Governance

- **G0** (Workflow): The spec-kit markdown is the source of truth and MUST NOT diverge from the code.
  Board features, tasks and bugs are motivators for moving forward: they may diverge from the truth,
  the spec-kit markdown may not.
- **G1** (Workflow): Changing a principle is a deliberate amendment (versioned, dated, with a reason).
  Versions follow MAJOR (principle removed or redefined), MINOR (principle or section added), PATCH
  (wording only). A version takes effect when merged to `main`.
- **G2** (Workflow): Until a working `1.0.0-beta` exists, the developer is the only contributor.

## Amendments

- **1.0.0** (2026-11-11, agreed by the developer in #90, takes effect on merge to `main`). First
  version, from the assertion table in #90.

**Version**: 1.0.0 | **Ratified**: 2026-11-11 | **Last Amended**: 2026-11-11
