# Forestry Deserialize Constitution

Rules no design decision may override, for the deserialization silo (Forestry.Deserialize) only.
Each rule names the spec-kit area it covers (Architecture, Code quality, Security, Documentation,
Workflow). Terms are defined under Definitions.

## Core Principles

### C0. Media-agnostic deserialization

- **Covers**: Architecture
- **Rule**: The deserialization package MUST NOT reference any media reader package. It owns what
  is shared by every media: the entry points, the abstract options, the enumerators and buffering,
  type, property and parameter definitions, policies, and `Row`. A media reader package references
  it, extends the options, and supplies its own reader and a synchronous, resumable step that turns
  buffered bytes into rows, much as System.Text.Json's serializer owns buffering and runs its
  converters over `Utf8JsonReader`. There is no media-neutral token or Value model between the two:
  each media's tokens stay inside its own package.
- **Rationale**: One storage model whatever the media: storage designed for analytics and high
  write throughput. Media grammars differ too much to share tokens (e.g. XML's end element, JSON's
  separate property names); what is shared is the shredding.
- **Checked by**: Project references of src and test (the reference goes media -> deserialization,
  never back).

### C1. Partial deserialization

- **Covers**: Architecture
- **Rule**: Deserialization MUST NOT require the whole document. Rows are enumerated as the
  document is read, holding only what the type definitions and their policies still need (e.g. an
  ancestor row until its end). Data held by user choice (e.g.
  reference lookups) may grow.
- **Rationale**: Multi-GB StanForD documents.
- **Checked by**: A test reading a generated document with many rows and nothing held by choice
  shows memory doesn't grow with the number of rows.

### C2. Single pass shredding

- **Covers**: Architecture
- **Rule**: Deserialization MUST read the document once, forward only. Type, property and
  parameter definitions, with their policies, are built once; each part of the document is then
  ignored, starts a row, or sets a typed member of a row. Several type definitions (e.g. Stem and
  Log rows) come from the same pass, never from re-reading. Deserialization only goes from media to
  type definitions (no serialization back).
- **Rationale**: Re-reading multi-GB documents is not an option; the target is flat NoSQL rows,
  not a mirror of the media.
- **Checked by**: Tests enumerating several row types from one document read once.

### C3. Tested through a real media reader, real data later

- **Covers**: Code quality
- **Rule**: Deserialization behavior MUST be tested through a real media reader package (XML first,
  JSON later) over small hand-written documents, in that media package's tests. No fake media or
  fake token producer is built. The deserialization package's own tests cover only what needs no
  document (e.g. definitions, policy precedence, `Row.AsType<T>()`). Confirmation against real StanForD
  samples happens where a media reader is combined with deserialization. StanForD-specific behavior
  is an edge case (hook), never the general rule.
- **Rationale**: A fake media duplicates the effort of a real reader for every media (XML, then
  JSON) and can hide the reader's real behavior; assumptions about StanForD have been wrong before.
- **Checked by**: Test project references (deserialization tests reference no media package);
  shredding tests live in the media package's tests; StanForD confirmation in the combining
  project.

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
- **Rule**: A working end-to-end example (StanForD file -> rows, in a project combining a media
  reader and deserialization) MUST come before performance work. Performance is then measured, not
  assumed.
- **Rationale**: Performance matters, but a working example matters more right now.
- **Checked by**: Plan/task order; performance tasks only after the example works.

### D0. Public documentation

- **Covers**: Documentation
- **Rule**: Public documentation in the style of Microsoft Learn's System.Text.Json docs: XML doc
  comments on every public member, plus conceptual how-to pages with examples.
- **Rationale**: The package is public on GitHub.
- **Checked by**: Build warns on missing XML docs; each feature adds or updates a how-to page.

### S0. Developer-chosen types only

- **Covers**: Security
- **Rule**: Deserialization MUST only create rows of the types the developer chose through the
  options (registered, or found in an assembly the developer names); it never creates a type named
  or chosen by the input, and never scans assemblies the developer did not name.
- **Rationale**: Classic deserialization vulnerability (input-chosen types); the package is public.
- **Checked by**: Review; a test where input names an unknown type creates nothing.

### S1. Fail fast on wrong input

- **Covers**: Security
- **Rule**: Deserialization MUST fail fast, with an exception, when input can't match the type
  definitions: an unexpected root; no type definition matched within a limit (depth or number of
  bytes); a value larger than a limit (e.g. string length); a row needing ancestor data that
  has not been read yet. Limits are generic options with defaults, defined by the deserialization
  package; media reader packages honor them (e.g. maximum value length) so an oversized value fails
  in the reader before it is fully buffered. The limit
  values are a plan decision.
- **Rationale**: Wrong or corrupt input, unintentional or intentional, must not keep consuming CPU
  and memory.
- **Checked by**: Tests at and just beyond each limit; a test with a wrong root fails on the first
  element.

## Additional Constraints

- **A0** (Code quality): Targets net9 and net10.
- **A1** (Architecture): No third-party NuGet dependencies in the deserialization package.
- **A3** (Workflow): Builds follow the SDK's shared engineering (`Directory.Build.props`, `eng/`,
  `doc/dev/`), referenced, not copied, including CI/CD.

## Definitions

- **Document**: The input: one file or stream in one media (e.g. an HPR file in XML).
- **Media reader package**: A package that reads one media forward and turns it into rows through
  the deserialization package's abstractions, using its own reader and tokens (e.g.
  Forestry.Deserialize.Xml).
- **Type definition**: A developer's flat class extending `Row`, with its property definitions
  (typed members, with converters and setters) and parameter definitions (constructor parameters,
  e.g. an ancestor row), built once per options and cached.
- **Row**: An instance of a developer's class extending the abstract `Row`, whose typed members
  (properties or fields) hold the values shredded for one occurrence in the document. It reports
  its C# type and casts to its concrete class (`AsType<T>()`).
- **Field**: Metadata about one member of a row (e.g. its member info, media name, path, depth,
  media-specific dimensions); never a value and never cast. Per-occurrence metadata is kept only
  when a policy or option asks for it.
- **Policy**: Behavior attached to a type, property or parameter definition at a row lifecycle
  moment (construction, member set, child row start, end, emit, retention), by attribute or by
  options; options win over attributes.
- **Shredding**: Taking parts of one document into one or more flat type definitions (rows of
  typed members), rather than mirroring the document's hierarchy.

## Governance

- **G0** (Workflow): The spec-kit markdown is the source of truth and MUST NOT diverge from the
  code. Board features, tasks and bugs (GitHub Project or Azure DevOps) are motivators for moving
  forward: they may diverge from the truth, the spec-kit markdown may not.
- **G1** (Workflow): Changing a principle is a deliberate amendment (versioned, dated, with a
  reason). Versions follow MAJOR (principle removed or redefined), MINOR (principle or section
  added), PATCH (wording only). A version takes effect when merged to `main`.
- **G2** (Workflow): Until a working `1.0.0-beta` exists, the developer is the only contributor.

## Amendments

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
  - S0 reworded to rows of types chosen through the options, registered or found in a named
    assembly (the entry points take no type argument).
  - Definitions: Value removed; Row redefined from "name-value pairs convertible to an instance"
    to "an instance of a class extending `Row`"; Document, Field and Policy added; Type definition
    gains parameter definitions.
  - Field redefined (2026-10-29, after the 2.0.0 approval, before merge; approved by the developer
    2026-10-29): from "one named, typed
    value of a row" to metadata about a member; values live on the row's typed members (research
    R4).

**Version**: 2.0.0 | **Ratified**: 2026-10-20 | **Last Amended**: 2026-10-29
