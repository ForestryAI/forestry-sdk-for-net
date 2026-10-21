# Forestry Deserialize Constitution

Rules no design decision may override, for the deserialization silo (Forestry.Deserialize) only.
Each rule names the spec-kit area it covers (Architecture, Code quality, Security, Documentation,
Workflow). Terms are defined under Definitions.

## Core Principles

### C0. Media-agnostic deserialization

- **Covers**: Architecture
- **Rule**: The deserialization package and its tests MUST NOT reference any media reader package.
  Its input is an enumerable of Values (sync or async).
- **Rationale**: One storage model whatever the media: storage designed for analytics and high
  write throughput.
- **Checked by**: Project references of src and test.

### C1. Partial deserialization

- **Covers**: Architecture
- **Rule**: Deserialization MUST NOT require the whole document. Rows are enumerated from the
  Values, holding only what the type definitions still need. Data held by user choice (e.g.
  reference lookups) may grow.
- **Rationale**: Multi-GB StanForD documents.
- **Checked by**: A test with many fake Values and nothing held by choice shows memory doesn't
  grow with the number of Values.

### C2. Single pass shredding

- **Covers**: Architecture
- **Rule**: Deserialization MUST consume the Values once, forward only, from IEnumerable or
  IAsyncEnumerable. Type and property definitions are built once; each Value is then ignored,
  starts a row, or sets a property. Several type definitions (e.g. Stem and Log rows) come from the
  same pass, never from re-reading. Deserialization only goes from media to type definitions (no
  serialization back).
- **Rationale**: Re-reading multi-GB documents is not an option; the target is flat NoSQL rows,
  not a mirror of the media.
- **Checked by**: Tests enumerating several row types from one fake Value sequence.

### C3. Fake Values, real data later

- **Covers**: Code quality
- **Rule**: Tests MUST use fake Value enumerables (sync and async). Attributes on type definitions
  are matched against fake Value dimensions. Confirmation against real StanForD samples happens
  where a media reader is combined with deserialization. StanForD-specific behavior is an edge case
  (hook), never the general rule.
- **Rationale**: No media dependency (C0); assumptions about StanForD have been wrong before.
- **Checked by**: Test project references; StanForD confirmation in the combining project.

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
- **Rule**: Deserialization MUST only create the type definitions the developer passed in; it
  never creates a type named or chosen by the input.
- **Rationale**: Classic deserialization vulnerability (input-chosen types); the package is public.
- **Checked by**: Review; a test where input names an unknown type creates nothing.

### S1. Fail fast on wrong input

- **Covers**: Security
- **Rule**: Deserialization MUST fail fast, with an exception, when input can't match the type
  definitions: an unexpected root; no type definition matched within a limit (depth or number of
  Values); a Value larger than a limit (e.g. string length). Limits are generic options with
  defaults, defined by the deserialization package; media reader packages honor them (e.g. maximum
  Value length) so an oversized Value fails in the reader before it is fully buffered. The limit
  values are a plan decision.
- **Rationale**: Wrong or corrupt input, unintentional or intentional, must not keep consuming CPU
  and memory.
- **Checked by**: Tests at and just beyond each limit; a test with a wrong root fails on the first
  Values.

## Additional Constraints

- **A0** (Code quality): Targets net9 and net10.
- **A1** (Architecture): No third-party NuGet dependencies in the deserialization package.
- **A3** (Workflow): Builds follow the SDK's shared engineering (`Directory.Build.props`, `eng/`,
  `doc/dev/`), referenced, not copied, including CI/CD.

## Definitions

- **Value**: One name-value item of a document's data (an attribute value, an element's text, or
  null for an empty element), with dimensions (path, name, depth, ...). NOT the media's grammar or
  structure: no tags, delimiters or schema.
- **Media reader package**: A package that reads one media forward and produces Values (e.g.
  Forestry.Deserialize.Xml).
- **Type definition**: A user's flat class plus its property definitions, built once per
  deserialization.
- **Row**: The name-value pairs shredded for one type definition, convertible to an instance of
  its C# class.
- **Shredding**: Taking parts of one document into one or more flat type definitions (rows and
  columns), rather than mirroring the document's hierarchy.

## Governance

- **G0** (Workflow): The spec-kit markdown is the source of truth and MUST NOT diverge from the
  code. Board features, tasks and bugs (GitHub Project or Azure DevOps) are motivators for moving
  forward: they may diverge from the truth, the spec-kit markdown may not.
- **G1** (Workflow): Changing a principle is a deliberate amendment (versioned, dated, with a
  reason). Versions follow MAJOR (principle removed or redefined), MINOR (principle or section
  added), PATCH (wording only). A version takes effect when merged to `main`.
- **G2** (Workflow): Until a working `1.0.0-beta` exists, the developer is the only contributor.

**Version**: 1.0.0 | **Ratified**: 2026-10-20 | **Last Amended**: 2026-10-21
