# Forestry Xml Constitution

Rules no design decision may override, for the reader silo (Forestry.Xml) only. Each rule names the
spec-kit area it covers (Architecture, Code quality, Security, Documentation, Workflow). Terms are
defined under Definitions. Agreed by the developer in #64 (2026-11-03).

**Scope**: the package `Forestry.Xml` in `sdk/xml/Forestry.Xml/` and its tests. What is specific to
reading is in `Forestry.Xml.Reading` (`src/Reading/`: `Utf8XmlReader`, `ReaderState`, `ReaderOptions`,
`ElementStack`, `CandidateNonTerminal`, `Utf8Reader` and the reader's throwing); what reading and a
later writer share is in `Forestry.Xml` (`EBNF`, `TokenType`, `XmlException`, formatting) (developer,
2026-11-03). A writer is not needed for the POC; it would go in `Forestry.Xml.Writing`. The XML media's
association and row reading (`Forestry.Deserialize.Xml`) are governed by the deserialization
constitution, not this one.

## Core Principles

### C0. The grammar is the reference

- **Covers**: Architecture
- **Rule**: The reader MUST follow the grammar's productions and well-formedness constraints as
  written. Every behaviour traces to a production or a WFC. A construct not supported yet MUST throw
  a clear "not supported" error, never be misread.
- **Rationale**: The reader's churn came from code that did not match the grammar (#28, the retired
  `BLOCKING.md`); the code is public.
- **Checked by**: Each production and WFC in scope has tests; the spec lists what is not supported
  yet.

### C1. Forward, synchronous, resumable

- **Covers**: Architecture
- **Rule**: Reading MUST be forward-only and synchronous, over a span or a sequence of segments. The
  reader never awaits; awaiting happens only in the buffering above it. When bytes are missing it
  halts without committing, and a new reader over more bytes continues from the reader state.
- **Rationale**: Pipes roll segments through, keeping CPU, cache and memory low and predictable for
  small and very large documents.
- **Checked by**: Tests reading a document split at every byte position, as two segments and as
  input arriving in parts, give identical tokens.

### C2. No allocation per value

- **Covers**: Architecture
- **Rule**: The reader MUST NOT decode to UTF-16 or allocate per token or per `Read()`. Allowed:
  copying a value that straddles segments (as `Utf8JsonReader`'s getters do), and getters that
  convert on request.
- **Rationale**: `System.Xml.XmlReader` decodes to UTF-16 and allocates a string per value; memory
  and GC must stay predictable whatever the document size.
- **Checked by**: A performance test measuring allocations over a read loop.

### C3. Read, skip and get contract

- **Covers**: Architecture
- **Rule**: `Read()` MUST either advance one token, halt reliably (nothing committed), or throw
  (everything unreliable). Skip, try-skip and the value getters follow #28; they are needed before
  the reader is POC-ready, but come after the first spec.
- **Rationale**: One clear contract ends the churn (#28). The reader is a package of its own, useful
  without deserialization to rows, and skipping can speed up row deserialization.
- **Checked by**: Tests per outcome of each operation.

### C4. Test-first

- **Covers**: Code quality
- **Rule**: Each assertion row MUST get a test shell before code is written. The developer reviews
  the shells in a test review task before any code.
- **Rationale**: Same as the deserialization silo.
- **Checked by**: Every assertion row maps to a test; the review task is closed before coding.

### C5. Design ownership

- **Covers**: Workflow
- **Rule**: The developer owns the constitution and every spec-kit input. AI decisions are proposals
  until the developer confirms them.
- **Rationale**: Same as the deserialization silo.
- **Checked by**: Confirmed decisions; review tasks closed by the developer.

### C6. A reader on its own

- **Covers**: Architecture
- **Rule**: The reader MUST be usable without the deserialization package: `Forestry.Xml` references
  nothing in `Forestry.Deserialize`, and `Forestry.Deserialize.Xml` references `Forestry.Xml`.
- **Rationale**: The reader is useful on its own (developer, 2026-11-03).
- **Checked by**: `Forestry.Xml`'s project references.

### D0. Public documentation

- **Covers**: Documentation
- **Rule**: XML doc comments on every public member, and one page explaining how a `Read()` cycle
  works (today `READER.md`).
- **Rationale**: The package is public on GitHub.
- **Checked by**: The build warns on missing XML docs; the page matches the code.

### S0. Fail fast on malformed input

- **Covers**: Security
- **Rule**: A document breaking a production or a WFC MUST throw an `XmlException` with its line and
  position, at the first byte where it is known. The reader never guesses or repairs.
- **Rationale**: Wrong or corrupt input must stop early, not produce wrong tokens.
- **Checked by**: Tests for each malformed case in scope.

### S1. Limits

- **Covers**: Security
- **Rule**: The reader MUST honour generic limits (e.g. value length, depth, name length) and fail
  before buffering beyond them. Names are compared in full, never silently truncated; a name longer
  than `ReaderOptions.MaxNameLength` (default 128 bytes, overridable, like `MaxDepth`) throws where it
  is first read (a start tag or an attribute name), so an end tag only ever fails on a mismatch.
- **Rationale**: Oversized input must not consume CPU and memory. Real StanForD has a 35-byte name
  (`ButtEndProfileExtrapolationFunction`).
- **Checked by**: Tests at and just beyond each limit.

### S2. No external resolution

- **Covers**: Security
- **Rule**: The reader MUST NOT process a DTD, resolve external entities or follow XInclude. A
  document type declaration is read as an opaque token. The five predefined entities and character
  references are not external and may be supported.
- **Rationale**: External resolution is a classic XML attack surface (XXE) with no StanForD use.
- **Checked by**: Tests that a DTD's entity declarations are never applied.

## Additional Constraints

- **A0** (Code quality): Targets net9 and net10.
- **A1** (Architecture): No third-party NuGet dependencies.
- **A3** (Workflow): Builds follow the SDK's shared engineering (`Directory.Build.props`, `eng/`,
  `doc/dev/`), referenced, not copied. CI/CD, packaging and publishing to NuGet are SDK-wide work,
  planned once the packages are POC-ready.
- **A4** (Code quality): Every test labels its steps `// Arrange`, `// Act`, `// Assert` (combined
  when one statement does two). A test reads its own document, written one element per line where it
  is short enough. Slow tests carry the trait `Category=Performance`, are left out of the default run
  by the test project's `.runsettings`, and gate the end of every user story
  (`dotnet test -s Performance.runsettings`).

## Definitions

- **Grammar**: The W3C XML 1.0 (Fifth Edition) EBNF productions and well-formedness constraints (WFC).
- **Segment**: The byte span currently being read; the input is one span or a sequence of segments.
- **Token**: One grammar unit a `Read()` produces (Declaration, DocumentType, Comment,
  ProcessingInstruction, Element, Attribute, Value, ElementEnd) with its value as bytes (a span, or a
  sequence when it straddles segments). A start tag's value is its name only (e.g. `Log`).
- **Halt**: `Read()` returns `false` because bytes are missing: nothing is committed, and the caller
  reads again with more bytes.
- **Reader state**: What survives from one reader to the next (tokens, element stack, line tracking,
  options); never positions within a segment.

## Governance

- **G0** (Workflow): The spec-kit markdown is the source of truth and MUST NOT diverge from the code.
  Board features, tasks and bugs are motivators for moving forward: they may diverge from the truth,
  the spec-kit markdown may not.
- **G1** (Workflow): Changing a principle is a deliberate amendment (versioned, dated, with a reason).
  Versions follow MAJOR (principle removed or redefined), MINOR (principle or section added), PATCH
  (wording only). A version takes effect when merged to `main`.
- **G2** (Workflow): Until a working `1.0.0-beta` exists, the developer is the only contributor.

## Amendments

- **1.0.0** (2026-11-03, agreed by the developer in #64, takes effect on merge to `main`). First
  version, from the reader rules moved out of the deserialization constitution (#50, "Moved to the
  reader silo") and the reader's own decisions in #64.

**Version**: 1.0.0 | **Ratified**: 2026-11-03 | **Last Amended**: 2026-11-03
