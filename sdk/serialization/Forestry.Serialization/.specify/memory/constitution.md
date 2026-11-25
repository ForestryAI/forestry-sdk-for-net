# Forestry Serialization Constitution

Rules no design decision may override, for the serialization silo (Forestry.Serialization) only.
Each rule names the spec-kit area it covers (Architecture, Code quality, Security, Documentation,
Workflow). Terms are defined under Definitions.

## Core Principles

### C0. Format-agnostic serialization

- **Covers**: Architecture
- **Rule**: The serialization package MUST NOT reference any format package. It owns what is
  shared by every format, in both directions: the entry points (`TelemetrySerializer`), the
  abstract options (`DeserializeOptions`, `SerializeOptions`), the enumerators and buffering, type,
  property and parameter definitions, converters (`Read` and `Write`), policies, and `Telemetry`. A
  format package references it, extends the options, and supplies its own reader and a synchronous,
  resumable step that turns buffered bytes into telemetries (reading), its own writer and object
  converter (writing), or both, much as System.Text.Json's serializer owns buffering and runs its
  converters over `Utf8JsonReader` and `Utf8JsonWriter`. There is no format-neutral token or Value
  model in either direction: each format's tokens stay inside its own package, converters write
  plain bytes through `IBufferWriter<byte>`, and escaping, nulls and the writer belong to the
  format.
- **Rationale**: One storage model whatever the format: storage designed for analytics and high
  write throughput. Format grammars differ too much to share tokens (e.g. XML's end element, JSON's
  separate property names); what is shared is the flattening. For writing, the same split as
  System.Text.Json (`JsonConverter<T>.Write` against a writer the format owns), without a shared
  token model.
- **Checked by**: Project references of src and test (the reference goes format -> serialization,
  never back); no type from a format package in the serialization package's public API.

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
  Log telemetries) come from the same pass, never from re-reading. Writing telemetries back out is
  governed by C8; it never re-reads the document.
- **Rationale**: Re-reading multi-GB documents is not an option; the target is flat telemetries,
  not a mirror of the format. Writing is in scope (#137); the single pass for reading stays.
- **Checked by**: Tests enumerating several telemetry types from one document read once.

### C3. Tested through a real format package, real data later

- **Covers**: Code quality
- **Rule**: Reading: deserialization behavior MUST be tested through a real format package that
  reads (XML first, JSON later) over small hand-written documents, in that format package's tests.
  No fake format reader or fake token producer is built. The serialization package's own tests
  cover only reading that needs no document (e.g. definitions, policy precedence,
  `Telemetry.AsType<T>()`). Confirmation against real StanForD samples happens where a format
  package is combined with deserialization. StanForD-specific behavior is an edge case (hook),
  never the general rule. Writing: the serialization package's own tests MUST prove serializing
  through a simple test format, a string format written to a `Stream`, developing the overrides
  (write, flush, close, dispose) the writing side relies on. Each real format package that writes
  (JSON Lines first) tests its own writer in its own tests.
- **Rationale**: A fake format reader duplicates the effort of a real reader for every format
  (XML, then JSON) and can hide the reader's real behavior; assumptions about StanForD have been
  wrong before. Writing has no grammar to parse: a string over a `Stream` exercises the real
  contract (converters, getters, object converter, the output's write, flush and dispose) without
  imitating a reader, and the serialization package must stand on its own for writing before any
  format package exists.
- **Checked by**: Test project references (the serialization package's tests reference no format
  package); flattening tests live in the format package's tests; writing tests in the serialization
  package's test project, and each format package's tests cover its own writer; StanForD
  confirmation in the combining project.

### C4. Test-first

- **Covers**: Code quality
- **Rule**: Each assertion row MUST get a test shell before code is written. Shells are reviewed in
  a board task (test review); then the AI or the developer writes the code.
- **Rationale**: Same as the XML reader's way of working.
- **Checked by**: Every assertion row maps to a test; test review task closed before coding.

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
- **Rule**: A working end-to-end example MUST come before performance work. For reading:
  StanForD file -> telemetries, in a project combining a format package and deserialization. For
  writing: a round trip, StanForD file -> telemetries -> JSON Lines, in a project combining the XML
  and JSON Lines format packages, before performance work on writing. Performance is then measured,
  not assumed.
- **Rationale**: Performance matters, but a working example matters more right now. The round trip
  is the milestone of #137 and #138.
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

### C8. Streaming writes

- **Covers**: Architecture
- **Rule**: Serializing MUST write telemetries forward, one at a time, to an output it is handed. A
  value goes from a telemetry to its converter's `Write` without boxing (a composed getter per
  property definition). Serializing holds no state of its own: per-call state (a pooled buffer)
  lives and dies inside the call, and state across calls (sequence count, bytes, collection limits)
  lives in an opt-in collection writer owned by whoever collects. It writes to a plain `Stream` or
  `PipeWriter` and flushes it, but never disposes or completes one it is handed; completing a
  collection writer finishes the output properly (e.g. a footer). It works without a pipeline.
  Memory MUST NOT grow with the number of telemetries written, beyond what the format holds on
  purpose (e.g. an open Parquet row group).
- **Rationale**: Writing is the second half of the path to big data. Like C1 for reading, it must
  stream: inputs are multi-GB. Collecting is the caller's (#141), so serializing stays simple and
  testable.
- **Checked by**: A test that loops over deserialized telemetries (`foreach` over `Deserialize`,
  `Serialize` each into one stream, no pipeline) shows memory does not grow with their number and
  the sequence count matches; a test that completing a collection writer finishes it, and that a
  plain `Stream` or `PipeWriter` is left open.

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
  has not been read yet. Limits are generic options with defaults, defined by the serialization
  package; format packages honor them (e.g. maximum value length) so an oversized value fails in
  the reader before it is fully buffered. The limit values are a plan decision. Serializing MUST
  fail when the definitions are built, never part-way through an output, for a member it cannot
  write (no converter, or a type that only formats to chars).
- **Rationale**: Wrong or corrupt input, unintentional or intentional, must not keep consuming CPU
  and memory. Half-written outputs are worse than none.
- **Checked by**: Tests at and just beyond each limit; a test with a wrong root fails on the first
  element; a test per member kind that cannot be written.

## Additional Constraints

- **A0** (Code quality): Targets the SDK's supported target frameworks, as defined in the engineering
  constitution (`eng/.specify/memory/constitution.md`, Definitions).
- **A1** (Architecture): No third-party NuGet dependencies in the serialization package.
- **A3** (Workflow): Builds follow the SDK's shared engineering (`Directory.Build.props`, `eng/`,
  `doc/dev/`), referenced, not copied, including CI/CD.
- **A4** (Code quality): Every test separates its steps with the comments `// Arrange`, `// Act` and
  `// Assert`, with one blank line between steps; steps done in one statement are labelled together
  (`// Arrange + Act`, `// Act + Assert`). A format test reads its own document, a local constant
  written one element per line, never one shared between tests.
- **A5** (Architecture): The serialization package has no attribute or option naming a part of a
  document or an output (e.g. an XML element, a JSON property name); those belong to the format
  package (e.g. Forestry.Serialization.Xml). Attributes in the serialization package are about
  definitions, converters and policies only.

## Definitions

- **Deserialize**: Read a document in a format into telemetries (flattening).
- **Serialize**: Write telemetries out in a format.
- **Serialization**: Both directions together: the package, this constitution and
  `TelemetrySerializer`.
- **Document**: The input: one file or stream in one format (e.g. an HPR file in XML).
- **Format**: The encoding a document is read from or telemetries are written to (XML and JSON
  Lines now, Parquet later), handled by a format package.
- **Format package**: A package for one format that reads it into telemetries, writes telemetries
  into it, or both, through the serialization package's abstractions, using its own reader, writer
  and tokens (e.g. Forestry.Serialization.Xml reads, Forestry.Serialization.JsonLines writes). Names
  of document or output parts (e.g. an XML element, a JSON property) are its own.
- **Output**: What serializing writes to: a `Stream`, or (async) a `PipeWriter`, handed in by the
  caller; never disposed or completed by serializing. One telemetry type per output. Collections,
  partitions and storage are decided outside the package (#141).
- **Collection**: Telemetries of one telemetry type and one partition, serialized together into one
  output, then sealed and transmitted (#141). Monitoring is two-fold: *collecting*, then later
  *analysing*; a collection is what is collected. Not a C# collection type: C7's "never
  collections" is about member types.
- **Collection writer**: An opt-in target, defined in this package, that knows about one
  collection: a collection stream (a `Stream` subclass) or a collection `PipeWriter` (decorating
  an inner `PipeWriter`), sharing one contract: its options, `Sequence` (bumped by serializing at
  each telemetry boundary), `BytesWritten`, `IsFull`, and a completion hook (ready or abandoned).
  Passed by whoever collects; never required by a serializing signature. Overridden by monitoring
  formatters.
- **Converter**: Reads a member's value from UTF-8 (`Read`) and writes it (`Write` through
  `IBufferWriter<byte>`). Resolved once per property definition from the options. A format package
  may register its own converters, e.g. for a different encoding.
- **Object converter**: Writes a whole telemetry in a format (e.g. one JSON Lines line, or each
  member appended to its Parquet column), owned by the format package.
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

- **4.0.0** (2026-11-26, agreed by the developer in #137, takes effect on merge to `main`). Reason:
  writing telemetries is added. A major break: `Convert` becomes `Read` because `Write` is added to
  drive the monitoring formatter, the writing side that serializes telemetries into collections.
  - C0 redefined: "Format-agnostic serialization" (was "... deserialization"); the package owns
    what both directions share (entry points, both options, converters with `Read` and `Write`);
    a format package reads, writes or both; no token model in either direction.
  - C2 redefined: "(no serialization back)" removed; writing is governed by C8.
  - C3 redefined: "Tested through a real format package, real data later" (was "... format
    reader ..."); writing is tested in this package through a string test format over a `Stream`.
  - C8 added: streaming writes.
  - C4 fixed: "assertion telemetry" back to "assertion row" (renamed by mistake in 3.1.1).
  - C6 reworded: the working example for writing is the round trip StanForD -> telemetries ->
    JSON Lines.
  - S1 extended: a member that cannot be written fails when definitions are built.
  - A1 and A5 reworded: "serialization package"; A5 covers output names; "format reader package"
    -> "format package".
  - Intro: "deserialization silo" -> "serialization silo".
  - Definitions: Deserialize, Serialize, Serialization, Output, Collection, Collection writer,
    Converter and Object converter added; Format reworded; Format reader package renamed Format
    package.

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

**Version**: 4.0.0 | **Ratified**: 2026-10-20 | **Last Amended**: 2026-11-26
