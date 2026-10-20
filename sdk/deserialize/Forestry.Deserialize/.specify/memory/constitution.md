# Forestry Deserialize Constitution

## Core Principles

### C0. Media-agnostic deserialization

- **Rule**: The deserialization package (Forestry.Deserialize) MUST NOT reference any media
  (XML, JSON, ...). Media syntax (paths, conditions, media-specific dimensions, attributes)
  lives in the media package.
- **Rationale**: The media is stripped out to unify the storage model for storage designed for
  analytics and high write throughput.
- **Checked by**: The deserialization project has no reference to a media package; review.

### C1. Low memory

- **Rule**: Users MUST be able to keep memory low by forward reading and piping media to storage.
  Memory held by user choice (e.g. reference lookups) may grow.
- **Rationale**: Multi-GB StanForD files.
- **Checked by**: A test reading a small and a large document, holding nothing by choice,
  compares memory.

### C2. Forward-only, async at the edge

- **Rule**: Reading MUST be forward-only. Awaiting happens only when buffering; the walk itself
  is synchronous.
- **Rationale**: Each Deserializer reads one document. Waiting for data never blocks a thread, so
  a host can read many documents at once without needing many threads. Readers work over byte
  sequences from a pipe (a single byte span isn't practical): segments roll through, keeping CPU,
  cache and memory use low and easing GC pressure, and performance is predictable for both small
  and very large media.
- **Checked by**: Review of the public API and the walk.

### C3. Real data

- **Rule**: Behavior MUST be verified against real StanForD samples, not assumed.
  StanForD-specific behavior is an edge case (hook), never the general rule.
- **Rationale**: Assumptions about StanForD have been wrong before.
- **Checked by**: At least one test per feature uses a real sample. Other tests use whichever
  media is easiest to execute (e.g. JSON via `Utf8JsonReader` until the XML reader has path and
  getters).

### C4. Tests as assertions

- **Rule**: Each feature's behavior MUST be stated as assertion rows, and each row MUST have a
  test.
- **Rationale**: Same as the XML reader's way of working.
- **Checked by**: Every row maps to a test.

### C5. Design ownership

- **Rule**: Design decisions are the developer's, made in discussion with the AI assistant. The
  AI assistant proposes options and consequences, and never decides alone.
- **Rationale**: (a) This is a test of spec-driven development; (b) ideas still mainly come from
  humans.
- **Checked by**: Design/clarification tasks are closed by the developer.

### C6. Working example first

- **Rule**: A working end-to-end example (StanForD file → rows) MUST come before performance
  work. Performance is then measured, not assumed.
- **Rationale**: Performance matters, but a working example matters more right now.
- **Checked by**: Plan/task order; performance tasks only after the example works.

## Additional Constraints

- **A0**: Targets net9 and net10.
- **A1**: No third-party NuGet dependencies in the deserialization package.
- **A2**: Readers MUST read forward over byte sequences without allocating per value, keeping
  CPU, GC pressure and memory low and predictable regardless of document size. System.Xml's
  `XmlReader` doesn't meet this: it decodes to UTF-16, allocates a string per value, and reads
  from a `Stream` that can block (C2). XML goes through the package's own reader.
- **A3**: Builds follow the SDK's shared engineering (`Directory.Build.props`, `eng/`,
  `doc/dev/`), referenced, not copied, including CI/CD.

## Governance

- **G0**: The spec-kit markdown is the source of truth and MUST NOT diverge from the code. Board
  features, tasks and bugs (GitHub Project or Azure DevOps) are motivators for moving forward:
  they may diverge from the truth, the spec-kit markdown may not.
- **G1**: Changing a principle is a deliberate amendment (versioned, dated, with a reason).
  Versions follow MAJOR (principle removed or redefined), MINOR (principle or section added),
  PATCH (wording only).

**Version**: 1.0.0 | **Ratified**: 2026-10-20 | **Last Amended**: 2026-10-20
