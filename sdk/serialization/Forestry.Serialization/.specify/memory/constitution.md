# Forestry Serialization Constitution

Rules no design decision may override, for the deserialization silo (Forestry.Serialization) only.
Each rule names the spec-kit area it covers (Architecture, Code quality, Security, Documentation,
Workflow). Terms are defined under Definitions.

## Core Principles

### C0. Format-agnostic deserialization

- **Covers**: Architecture
- **Rule**: The deserialization package MUST NOT reference any format reader package. It owns what
  is shared by every format: the entry points, the abstract options, the enumerators and buffering,
  type, property and parameter definitions, policies, and `Telemetry`. A format reader package references
  it, extends the options, and supplies its own reader and a synchronous, resumable step that turns
  buffered bytes into telemetries, much as System.Text.Json's serializer owns buffering and runs its
  converters over `Utf8JsonReader`. There is no format-neutral token or Value model between the two:
  each format's tokens stay inside its own package.
- **Rationale**: One storage model whatever the format: storage designed for analytics and high
  write throughput. Format grammars differ too much to share tokens (e.g. XML's end element, JSON's
  separate property names); what is shared is the flattening.
- **Checked by**: Project references of src and test (the reference goes format -> deserialization,
  never back).

### C1. Partial deserialization

- **Covers**: Architecture
- **Rule**: Deserialization MUST NOT require the whole document. Telemetries are enumerated as the
  document is read, holding only what the type definitions and their policies still need (e.g. an
  ancestor telemetry until its end). Data held by user choice (e.g.
  reference lookups) may grow.
- **Rationale**: Multi-GB StanForD documents.
- **Checked by**: A test reading a generated document with many telemetries and nothing held by choice
  shows memory doesn't grow with the number of telemetries.

### C2. Single pass flattening

- **Covers**: Architecture
- **Rule**: Deserialization MUST read the document once, forward only. Type, property and
  parameter definitions, with their policies, are built once; each part of the document is then
  ignored, starts a telemetry, or sets a typed member of a telemetry. Several type definitions (e.g. Stem and
  Log telemetries) come from the same pass, never from re-reading. Deserialization only goes from format to
  type definitions (no serialization back).
- **Rationale**: Re-reading multi-GB documents is not an option; the target is flat telemetries, not a
  mirror of the format.
- **Checked by**: Tests enumerating several telemetry types from one document read once.

### C3. Tested through a real format reader, real data later

- **Covers**: Code quality
- **Rule**: Deserialization behavior MUST be tested through a real format reader package (XML
  first, JSON later) over small hand-written documents, in that format package's tests. No fake
  format or fake token producer is built. The deserialization package's own tests cover only what
  needs no document (e.g. definitions, policy precedence, `Telemetry.AsType<T>()`). Confirmation against
  real StanForD samples happens where a format reader is combined with deserialization.
  StanForD-specific behavior is an edge case (hook), never the general rule.
- **Rationale**: A fake format duplicates the effort of a real reader for every format (XML, then
  JSON) and can hide the reader's real behavior; assumptions about StanForD have been wrong before.
- **Checked by**: Test project references (deserialization tests reference no format package);
  flattening tests live in the format package's tests; StanForD confirmation in the combining
  project.

### C4. Test-first

- **Covers**: Code quality
- **Rule**: Each assertion telemetry MUST get a test shell before code is written. Shells are reviewed in
  a board task (test review); then the AI or the developer writes the code.
- **Rationale**: Same as the XML reader's way of working.
- **Checked by**: Every assertion telemetry maps to a test; test review task closed before coding.

### C5. Design ownership

- **Covers**: Workflow
- **Rule**: The developer owns the constitution and all input to every spec-kit phase. AI
  decisions (e.g. in research.md) are proposals until the developer confirms them. Tasks are
  reflected on a board for confirmation (test or code review).
- **Rationale**: (a) This is a test of spec-driven development; (b) ideas still mainly come from
  humans.
- **Checked by**: Confirmed decisions; review tasks closed by the developer.

### C6. Working example first

- **Covers**: Workflow
- **Rule**: A working end-to-end example (StanForD file -> telemetries, in a project combining a format
  reader and deserialization) MUST come before performance work. Performance is then measured, not
  assumed.
- **Rationale**: Performance matters, but a working example matters more right now.
- **Checked by**: Plan/task order; performance tasks only after the example works.

### C7. Flat telemetries

- **Covers**: Architecture
- **Rule**: A type definition MUST be flat: every member is a single value, a primitive C# type or
  a single-value struct that parses from and formats to a byte span or string (e.g. `Guid`,
  `DateTimeOffset`). Never composite structs, nested objects or collections. Dimensions are copied
  onto the telemetries that need them, not nested or joined.
- **Rationale**: The target is big data: a single-value member can become one column in any
  format, row-based or columnar (JSON Lines now, Parquet later). Without this rule, flat telemetries were
  only promised by Forestry.StanForD (its C0).
- **Checked by**: Building a type definition with a member that isn't a single value throws; a test
  per rejected kind (nested object, collection, composite struct).

### D0. Public documentation

- **Covers**: Documentation
- **Rule**: Public documentation in the style of Microsoft Learn's System.Text.Json docs: XML doc
  comments on every public member, plus conceptual how-to pages with examples.
- **Rationale**: The package is public on GitHub.
- **Checked by**: Build warns on missing XML docs; each feature adds or updates a how-to page.

### S0. Developer-chosen types only

- **Covers**: Security
- **Rule**: Deserialization MUST only create telemetries of the types the developer added through the
  options, and of the telemetry types their constructors need; it never creates a type named or chosen
  by the input, and never scans assemblies.
- **Rationale**: Classic deserialization vulnerability (input-chosen types); the package is public.
- **Checked by**: Review; a test where input names an unknown type creates nothing.

### S1. Fail fast on wrong input

- **Covers**: Security
- **Rule**: Deserialization MUST fail fast, with an exception, when input can't match the type
  definitions: an unexpected root; no type definition matched within a limit (depth or number of
  bytes); a value larger than a limit (e.g. string length); a telemetry needing ancestor data that
  has not been read yet. Limits are generic options with defaults, defined by the deserialization
  package; format reader packages honor them (e.g. maximum value length) so an oversized value
  fails in the reader before it is fully buffered. The limit values are a plan decision.
- **Rationale**: Wrong or corrupt input, unintentional or intentional, must not keep consuming CPU
  and memory.
- **Checked by**: Tests at and just beyond each limit; a test with a wrong root fails on the first
  element.

## Additional Constraints

- **A0** (Code quality): Targets the SDK's supported target frameworks, as defined in the engineering
  constitution (`eng/.specify/memory/constitution.md`, Definitions).
- **A1** (Architecture): No third-party NuGet dependencies in the deserialization package.
- **A3** (Workflow): Builds follow the SDK's shared engineering (`Directory.Build.props`, `eng/`,
  `doc/dev/`), referenced, not copied, including CI/CD.
- **A4** (Code quality): Every test separates its steps with the comments `// Arrange`, `// Act` and
  `// Assert`, with one blank line between steps; steps done in one statement are labelled together
  (`// Arrange + Act`, `// Act + Assert`). A format test reads its own document, a local constant
  written one element per line, never one shared between tests.
- **A5** (Architecture): The deserialization package has no attribute or option naming a part of a
  document (e.g. an element or attribute name); those belong to the format reader package (e.g.
  Forestry.Serialization.Xml). Attributes in the deserialization package are about definitions and
  policies only.

## Definitions

- **Document**: The input: one file or stream in one format (e.g. an HPR file in XML).
- **Format**: The encoding a document is written in (XML now, JSON later), read forward by a format
  reader package.
- **Format reader package**: A package that reads one format forward and turns it into telemetries through
  the deserialization package's abstractions, using its own reader and tokens (e.g.
  Forestry.Serialization.Xml). Names of document parts (e.g. an XML element or attribute) are its own.
- **Reading information**: Where a value is read in the document: its depth, name and path
  (`ReadingInformation`), reported by the format with each value. Generic, not specific to one
  format. Never a value, and never kept unless a policy or option asks for it.
- **Type definition**: A developer's flat class extending `Telemetry`, with its property definitions
  (typed members, with converters and setters) and parameter definitions (constructor parameters,
  e.g. an ancestor telemetry), built once per options and cached.
- **Telemetry**: Information a forestry device emits (*tele*, distant, and *metron*, measure:
  measuring at a distance), with no structure imposed by the term. Here, an instance of a
  developer's class extending the abstract `Telemetry`, whose members hold the values flattened for
  one occurrence in the document. Every member is a single value (C7). It reports its C# type and
  casts to its concrete class (`AsType<T>()`). One instance is *a telemetry*, many are
  *telemetries*. A format's own units keep their names (e.g. a Parquet row group).
- **Policy**: Behavior attached to a type, property or parameter definition at a telemetry lifecycle
  moment (construction, member set, child telemetry start, end, hand-out, retention), by attribute or by
  options; options win over attributes.
- **Flattening**: Taking parts of one document into one or more flat type definitions (telemetries of
  single-value members), rather than mirroring the document's hierarchy. Not Parquet's (Dremel's)
  shredding, which stores nested records with levels.

## Governance

- **G0** (Workflow): The spec-kit markdown is the source of truth and MUST NOT diverge from the
  code. Board features, tasks and bugs (GitHub Project or Azure DevOps) are motivators for moving
  forward: they may diverge from the truth, the spec-kit markdown may not.
- **G1** (Workflow): Changing a principle is a deliberate amendment (versioned, dated, with a
  reason). Versions follow MAJOR (principle removed or redefined), MINOR (principle or section
  added), PATCH (wording only). A version takes effect when merged to `main`.
- **G2** (Workflow): Until a working `1.0.0-beta` exists, the developer is the only contributor.

## Amendments

- **3.1.1** (2026-11-25, agreed by the developer in #142, takes effect on merge to `main`). Reason:
  the package is renamed Forestry.Serialization, and *Row* is renamed *Telemetry*: what forestry
  devices emit, measured at a distance. Wording only (PATCH): no principle added, removed or
  redefined.
  - Title: "Forestry Deserialize Constitution" -> "Forestry Serialization Constitution"; package
    names Forestry.Deserialize(.Xml) -> Forestry.Serialization(.Xml).
  - "row" -> "telemetry" throughout, including C7's title ("Flat rows" -> "Flat telemetries").
  - Definitions: Row renamed Telemetry and reworded (the term's meaning, its plural, and that a
    format's own units keep their names); the line separating rows from row-based vs columnar
    formats removed, since the new name no longer invites that confusion.
  - Format reader package keeps its name until writing is added (#137, C2 amendment).

- **3.1.0** (2026-11-18, agreed by the developer in #125, takes effect on merge to `main`). Reason:
  big data orientation (#125): "format" and "flatten" settled as terms, flat rows made explicit,
  format-specific names leave the deserialization package.
  - C7 added: flat rows (from Forestry.StanForD's C0), checked when a type definition is built.
  - A5 added: no attribute or option naming a part of a document.
  - C0, C2, C3, S1 and A4 reworded: "media" -> "format", "shredding" -> "flattening", including the
    titles of C0, C2 and C3.
  - Definitions: Format and Reading information added; Media reader package renamed Format reader
    package; Shredding renamed Flattening; Row redefined (single-value members, not row-based vs
    columnar); Field removed (its member, type and resolved converter belong to the property
    definition, its per-value metadata is reading information); Document reworded.
  - Reworded 2026-11-19, after the 3.1.0 agreement, before merge (developer, during spec 003's
    plan): "Format information" (`FormatInformation`) renamed "Reading information"
    (`ReadingInformation`), since depth, name and path are generic; "effective converter" renamed
    "resolved converter" (spec 003 research R33, R36).
- **3.0.0** (2026-11-10, confirmed by the developer, takes effect on merge to `main`). Reason: net9.0
  left Microsoft support on 2026-11-10 and the SDK now targets net10.0 only (engineering spec 001,
  research R8).
  - A0 redefined: from naming net9 and net10 to the engineering silo's definition "Supported target
    frameworks", so a new .NET release needs no amendment here.
- **2.1.0** (2026-11-02, proposed and approved by the developer during the US1 code review, takes
  effect on merge to `main`). Reason: a shared document (`Document1`) used by throwing and non-throwing tests hid
  what each test reads, and unlabelled steps made tests slower to review (C4).
  - A4 added: Arrange / Act / Assert comments in every test, and one local document per media
    test, one element per line.
- **2.0.0** (2026-10-26, approved by the developer, takes effect on merge to `main`). Reason: a
  media-neutral Value with dimensions cannot harmonize media grammars (XML's end element has no
  JSON counterpart), and a fake media to test it duplicates work per media while the XML reader is
  being built anyway.
  - C0 redefined: the deserialization package owns entry points, options, enumerators, buffering,
    definitions, policies and `Row`; media packages supply their reader and a resumable step. No
    shared token or Value model.
  - C3 redefined: behavior is tested through the real XML reader (JSON later), not fake Values.
  - C1, C2 and S1 reworded to rows and their typed members; S1 adds missing ancestor data as a
    fail-fast case.
  - S0 reworded to rows of types added through the options and the row types their constructors
    need (the entry points take no type argument); reworded 2026-10-30, after the 2.0.0 approval,
    to drop assembly scanning (research R12); approved by the developer 2026-10-30.
  - Definitions: Value removed; Row redefined from "name-value pairs convertible to an instance"
    to "an instance of a class extending `Row`"; Document, Field and Policy added; Type definition
    gains parameter definitions.
  - Field redefined (2026-10-29, after the 2.0.0 approval, before merge; approved by the developer
    2026-10-29): from "one named, typed
    value of a row" to metadata about a member; values live on the row's typed members (research
    R4).

**Version**: 3.1.1 | **Ratified**: 2026-10-20 | **Last Amended**: 2026-11-25
