# Feature Specification: Writing telemetries

**Feature Branch**: `serialization-004-writing`

**Created**: 2026-11-26

**Status**: Draft

**Input**: User description: "Spec 004 for Forestry.Serialization: writing telemetries (GitHub
issue #137; constitution 4.0.0 on branch serialization-004-writing)." Source: GitHub issue
ForestryAI/forestry-sdk-for-net#137. The SDK turns telemetries from forestry devices into big data
ready collections. Reading (deserializing) a device document into flat telemetries works; nothing
writes them out yet. This spec adds the writing side: format-neutral building blocks that a
format package (JSON Lines first, #138) plugs into, and the opt-in collection writers that
monitoring (#141) uses to collect telemetries into collections.

## Clarifications

### Session 2026-11-26

- Q: How does a format, or a developer, decide how values are written? → A: Through an abstract
  `TelemetryWriter` (an `IBufferWriter<byte>`) that converters delegate to by the value's type
  (`WriteValue` overloads) and that frames each telemetry (start, names, nulls, end); a format or
  developer subclasses it, e.g. to customize strings, booleans or custom values (developer, after
  reviewing US1, replacing the object converter and member sections; constitution C0 revised before
  merge). The value methods are `WriteValue` overloads named for what the value is, not how a format
  represents it (text, `bool`, `int`, `long`, `double`, `decimal`, `Guid`, `DateTime`,
  `DateTimeOffset`, and a generic overload for the rest), since JSON quotes a `Guid` while Parquet
  stores dates as integers (developer, same day).
- Q: What do serialize options and deserialize options share? → A: One common abstract base that
  both derive from. Deserializing goes from one format to telemetries, and serializing from
  telemetries to another format; what they share is the type and property definitions, and the
  converters. Both options have converters: deserializing only uses `Read`, serializing only uses
  `Write`. Each options instance builds and locks its own definitions under the same rules.
- Q: What is the collection options type named? → A: `MonitoringCollectionOptions` (a custom
  answer): it names the role, monitoring collecting telemetries into big data ready collections,
  and is not read as .NET's `ICollection`. `TelemetryCollectionOptions` and `CollectionOptions` were
  the other options.
- Q: Where does the default collection options instance sit? → A: On `SerializeOptions`, used when
  a collection writer is created without its own; a format package can set its own default there.

## User Scenarios & Testing *(mandatory)*

The users of this feature are:

- a **developer** who has telemetries (deserialized from a device document, or built in code) and
  wants them written to a `Stream` or `PipeWriter` in a format
- a **format package author** (JSON Lines next, Parquet later) who supplies how a value and a whole
  telemetry are written, without the serialization package knowing the format
- a **monitoring formatter author** (the collecting directive in Forestry.Core, #141) who needs to
  know how many telemetries and bytes went into a collection, when it is full, and whether it
  finished properly
- a **reader** of the documentation who needs to know what serializing does and does not do

Writing is tested in this package's own tests through a simple string test format over a
`Stream` (C3, 4.0.0). Reading keeps being tested through the real XML format package. Nothing is
released, so breaking changes to converters are accepted.

### User Story 1 - Converters read and write (Priority: P1)

A converter turns a member's value into UTF-8 text and back. Today a converter only reads, through
a method named `Convert`. After this feature, `Convert` is named `Read`, and every converter also
has an overridable `Write` that formats a value into a buffer it is handed. The built-in converters
(primitives, `string`, `Guid`, `DateTimeOffset`, nullables, and every type that formats to UTF-8)
write the same text they read. A format package can register its own converters where it writes a
value differently, exactly as the XML format package already registers its own boolean converter
for reading.

**Why this priority**: Every other story writes values through converters. Renaming `Convert` is
the breaking change the constitution's MAJOR version records, so it lands first and every package
that has converters (Serialization, Serialization.Xml, StanForD) keeps building.

**Independent Test**: Write a value of each built-in kind through its resolved converter into a
buffer, read the bytes back through the same converter, and compare; register a custom converter
and see it used for writing.

**Acceptance Scenarios**:

1. **Given** a member of each built-in kind (e.g. `int`, `decimal`, `bool`, `string`, `Guid`,
   `DateTimeOffset`, `int?` with a value), **When** its resolved converter writes the value and
   reads the written bytes, **Then** the value read equals the value written.
2. **Given** a converter that needs more room than first offered (e.g. a long string), **When** it
   writes, **Then** it asks for a bigger buffer and the whole value is written; there is no
   "did not fit, try again" result.
3. **Given** a custom converter registered in the options for a type, **When** a member of that
   type is written, **Then** the custom converter writes it, not the built-in one.
4. **Given** the existing reading tests of Forestry.Serialization, Forestry.Serialization.Xml and
   Forestry.StanForD, **When** `Convert` is renamed `Read`, **Then** they all pass unchanged in
   behavior.

---

### User Story 2 - Serialize telemetries to a Stream or a PipeWriter (Priority: P1)

A developer hands telemetries and serialize options to `TelemetrySerializer` with a target: a
plain `Stream` (sync or async) or a plain `PipeWriter` (async only). Each telemetry is written
whole through the format's telemetry writer; each member's value comes from the telemetry without
boxing and is written by its resolved converter, which delegates to the writer. One telemetry or many can be serialized in one
call; many telemetries can come straight from deserializing, so a document is read and written in
one forward pass without holding all telemetries. The target belongs to the caller: serializing
writes and flushes, but never disposes a `Stream` or completes a `PipeWriter`, so a test can
rewind a `MemoryStream` and read back what was written.

**Why this priority**: This is the feature: without it nothing writes telemetries. It is proven in
this package through a string test format, so it stands on its own before JSON Lines (#138).

**Independent Test**: Serialize two telemetries with the string test format into a
`MemoryStream`, rewind it, read it back as a string and assert the text; do the same over a
`PipeWriter`; deserialize a small XML document and serialize its telemetries in a `foreach` loop
into one stream with no pipeline.

**Acceptance Scenarios**:

1. **Given** two telemetries and the string test format, **When** each is serialized into one
   `MemoryStream` (sync), **Then** reading the rewound stream gives the two telemetries' text in
   order, and the stream is still open.
2. **Given** the same telemetries, **When** they are serialized asynchronously into a `Stream`,
   and separately into a `PipeWriter`, **Then** the bytes are the same as the sync case, the
   `Stream` is not disposed and the `PipeWriter` is not completed.
3. **Given** a sequence of telemetries (sync) or an asynchronous sequence (async), **When** it is
   serialized in one call, **Then** every telemetry is written in order and telemetries are taken
   one at a time, not gathered first.
4. **Given** a small XML document of several telemetry types, **When** a `foreach` over
   `Deserialize` serializes each telemetry into one stream, with no pipeline, **Then** the stream
   holds every telemetry in document order.
5. **Given** a member whose value is null, **When** the telemetry is written, **Then** the format
   decides how null is written, and the converter is never asked to write a missing value.
6. **Given** a telemetry type with a member no converter can write (no converter, or a type that
   only formats to characters), **When** the serialize options lock, **Then** they fail naming the
   telemetry type and member, before anything is written.
7. **Given** a telemetry whose type was not added to the serialize options, **When** it is
   serialized, **Then** serializing fails naming the type, before anything of that telemetry is
   written.

---

### User Story 3 - Collect telemetries with a collection writer (Priority: P2)

Whoever collects telemetries into collections (the monitoring formatter in Forestry.Core, a
format package, a test) passes a collection writer instead of a plain target: a collection
stream or a collection `PipeWriter`, both defined in this package and both open to subclassing.
A collection writer is created with collection options (whether it leaves its inner target open,
a target size, an optional maximum number of telemetries). While telemetries are serialized into
it, it knows how many telemetries (its sequence) and how many bytes have gone in, and whether it
is full. Completing it finishes the output properly and tells a hook whether the collection is
ready to hand on, or abandoned after an error. A plain `Stream` or `PipeWriter` keeps working with
no sequence and no limits: nobody has to know about collections to serialize.

**Why this priority**: Collections are how monitoring collects telemetries for big data (#141),
but serializing works without them, so P1 can ship first.

**Independent Test**: Wrap a `MemoryStream` in a collection stream with a target size, serialize
telemetries until it reports full, complete it, and assert the text, the sequence, the bytes and
the "ready" hook; repeat with an exception for "abandoned", and with a collection `PipeWriter`.

**Acceptance Scenarios**:

1. **Given** a collection stream over a `MemoryStream`, **When** three telemetries are serialized
   into it, **Then** its sequence is 3, and its bytes equal the length of what was written.
2. **Given** a telemetry whose writing takes several buffer writes, **When** it is serialized into
   a collection writer, **Then** the sequence goes up by exactly one.
3. **Given** collection options with a target size or a maximum number of telemetries, **When**
   telemetries are serialized until the limit is passed, **Then** the collection writer reports
   full after the telemetry that passed it, never part-way through one.
4. **Given** a collection writer, **When** it is completed without an error, **Then** every byte
   has reached the inner target before the hook runs, and the hook is told the collection is
   ready; **When** it is completed with an error, **Then** the hook is told it is abandoned.
5. **Given** collection options that leave the inner target open, **When** the collection writer
   is completed, **Then** the inner `Stream` is not disposed (or the inner `PipeWriter` not
   completed); otherwise it is.
6. **Given** a collection `PipeWriter` over a caller's `PipeWriter`, **When** telemetries are
   serialized into it, **Then** converters write into the pipe's own buffers (no extra copy), and
   its sequence and bytes match the collection stream's for the same telemetries.
7. **Given** a subclass of a collection writer that overrides its writing or its hook, **When**
   telemetries are serialized into it, **Then** the overrides run, so a monitoring formatter can
   add its own behavior.

---

### Edge Cases

- No telemetries: serializing an empty sequence writes nothing (or only what the format writes for
  an empty output) and leaves a collection writer's sequence at 0.
- A telemetry of a type the string test format does not know fails with a message naming the type.
- A value larger than the buffer first offered (a long string) is written whole.
- Cancellation during async serializing stops before the next telemetry and leaves the caller's
  target open; a collection writer completed after a cancellation reports "abandoned".
- A caller's `Stream` that cannot write (read-only) fails before anything is written.
- Serialize options are locked on first use; changing them afterwards fails, as with deserialize
  options.
- A telemetry and its type's definition are read concurrently by several serialize calls on
  different targets: definitions are shared and read-only, targets are not shared.
- `Convert` callers in other packages (Serialization.Xml, StanForD) are renamed in the same change
  so every package keeps building.

## Requirements *(mandatory)*

### Functional Requirements

**Converters (US1)**

- **FR-001**: A converter's reading method MUST be named `Read` (was `Convert`), with unchanged
  behavior.
- **FR-002**: Every converter MUST have an overridable write method that writes a non-null value
  through the telemetry writer it is handed, delegating to the writer's `WriteValue` overload for
  the value's type or writing raw UTF-8 through it, asking for more room as
  needed, with no "did not fit" result.
- **FR-003**: The built-in converters MUST write the text they read, so that writing then reading
  any value of a built-in kind gives the same value.
- **FR-004**: A format package MUST be able to register converters that are used for writing in
  place of the built-in ones, by the same mechanism that registers converters for reading.
- **FR-005**: The renamed reading method MUST be applied in Forestry.Serialization.Xml and
  Forestry.StanForD in the same change, with all their existing tests passing.

**Definitions and options (US2)**

- **FR-006**: Every property definition MUST provide a way to get its member's value from a
  telemetry without boxing.
- **FR-007**: Serialize options MUST exist beside deserialize options. Telemetry types added to
  serialize options MUST be built into type and property definitions under the same rules as for
  reading: flat members only (C7), one resolved converter per member, and the options lock on
  first use.
- **FR-008**: Locking serialize options MUST fail, naming the telemetry type and member, for a
  member that cannot be written (no converter, or a type that only formats to characters) (S1).
- **FR-009**: Serialize options and deserialize options MUST derive from one common abstract base
  that holds what both directions share: the telemetry types, the type and property definitions
  built from them, and the converters. Deserializing uses only a converter's `Read`, serializing
  only its `Write`. Each options instance builds and locks its own definitions.

**Serializing (US2)**

- **FR-010**: `TelemetrySerializer` MUST serialize one telemetry, or a sequence of telemetries, to
  a plain `Stream` synchronously and asynchronously, and to a plain `PipeWriter` asynchronously.
- **FR-011**: The many-telemetries overloads MUST take telemetries one at a time from the sequence
  (a sequence for sync, an asynchronous sequence for async), so that serializing pairs with
  deserializing without gathering telemetries first.
- **FR-012**: Each telemetry MUST be written whole through the format's telemetry writer (start,
  member names, nulls, end), each member's value through its resolved converter; the writer decides
  how strings are escaped, how each kind of value looks and how null is written, and a developer can
  customize any of it by subclassing the writer.
- **FR-013**: The asynchronous path MUST only use the target's asynchronous write and flush, never
  the synchronous ones.
- **FR-014**: Serializing MUST NOT dispose a caller's `Stream` or complete a caller's `PipeWriter`.
- **FR-015**: For a `Stream` target, serializing MUST use a buffer that lives only for the call and
  is returned when the call ends; for a `PipeWriter` target, converters MUST write into the
  `PipeWriter`'s own buffers.
- **FR-016**: Serializing MUST hold no state between calls (C8): state that lasts across calls lives
  in a collection writer.
- **FR-017**: Serializing a telemetry whose type was not added to the serialize options MUST fail
  naming the type, before any of it is written.

**Collection writers (US3)**

- **FR-018**: The package MUST provide a collection stream (over an inner `Stream`) and a
  collection `PipeWriter` (decorating an inner `PipeWriter`), both public and open to
  subclassing, sharing one contract: their collection options, a sequence, bytes written, whether
  they are full, and a completion hook.
- **FR-019**: Serializing into a collection writer MUST raise its sequence by exactly one after each
  whole telemetry, whatever the number of writes the telemetry took.
- **FR-020**: Bytes written MUST count every byte that reached the collection writer.
- **FR-021**: A collection writer MUST report full once its sequence or bytes pass the limits in its
  collection options, checked only between telemetries.
- **FR-022**: Completing a collection writer MUST first get every byte to its inner target, then
  call the completion hook with whether the collection is ready (no error) or abandoned (an error).
- **FR-023**: A collection writer MUST dispose its inner `Stream` or complete its inner `PipeWriter`
  on completion unless its collection options say to leave it open.
- **FR-024**: Collection options MUST be one immutable type shared by both collection writers, read
  once when a collection writer is created: leave open, target size, an optional maximum number of
  telemetries, whether size counts bytes before or after compression, and buffering settings used
  when the collection `PipeWriter` creates its inner writer from a `Stream`.
- **FR-025**: The collection options type MUST be named `MonitoringCollectionOptions`.
- **FR-026**: Serialize options MUST carry a default `MonitoringCollectionOptions`, used when a
  collection writer is created without its own; a format package can set its own default there.

**Testing and documentation**

- **FR-027**: This package's tests MUST include a simple string test format that writes telemetries
  as text to a `Stream`, used to prove serializing and the collection writers (C3).
- **FR-028**: A test MUST loop over deserialized telemetries and serialize each into one stream, with
  no pipeline, showing memory does not grow with the number of telemetries and the sequence matches
  (C8); it lives where a real format reader is available (Forestry.Serialization.Xml's tests).
- **FR-029**: The package README and XML documentation MUST describe serializing, its targets, that
  the caller owns the target, and collection writers, with an example that serializes into a
  `MemoryStream` and reads the string back (D0).

### Key Entities

- **Telemetry**: a flat record of single values (C7), deserialized from a device document or
  built in code; what serializing writes.
- **Converter**: reads a member's value from UTF-8 and writes it; resolved once per property
  definition; a format package may register its own.
- **Object converter**: writes a whole telemetry in a format; owned by the format package (here,
  the string test format).
- **Serialize options**: how telemetries are written: telemetry types, converters, format, and a default `MonitoringCollectionOptions`; locked
  on first use.
- **Output / target**: a plain `Stream` or `PipeWriter` owned by the caller.
- **Collection writer**: a collection stream or collection `PipeWriter` that knows one
  collection's options, sequence, bytes, whether it is full, and how it completed.
- **Collection options** (`MonitoringCollectionOptions`): immutable settings of one collection: leave open, limits, buffering.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every built-in converter kind, writing a value and reading it back gives the same
  value: 100% of the kinds the package converts today.
- **SC-002**: 100% of the existing tests of Forestry.Serialization, Forestry.Serialization.Xml and
  Forestry.StanForD pass after the converter rename, with no change in reading behavior.
- **SC-003**: Serializing the same telemetries to a sync `Stream`, an async `Stream` and a
  `PipeWriter` gives byte-for-byte the same output: 3 of 3 targets.
- **SC-004**: After any serialize call, the caller's `Stream` is still open and its `PipeWriter`
  not completed: 0 targets closed by serializing.
- **SC-005**: Serializing ten times as many telemetries in one `foreach` over a deserialized
  document does not raise retained memory beyond a fixed allowance (memory does not grow with the
  number of telemetries), as C1 already shows for reading.
- **SC-006**: A collection writer's sequence equals the number of telemetries serialized into it,
  and its bytes equal the output's length: 100% across the collection writer tests, for both kinds.
- **SC-007**: A member that cannot be written fails when the options lock, before any output is
  written: 0 partially written outputs in the failure tests.
- **SC-008**: A reader of the README can serialize telemetries into a `MemoryStream` and read the
  string back by following its example alone.

## Driving needs

- Monitoring collects telemetries into big data ready collections (#141); collections need a
  sequence, a size and an end, without the serialization package knowing about pipelines or storage.
- The milestone with #138: a StanForD Harvesting document makes a round trip, XML → telemetries →
  JSON Lines (C6). This spec provides everything except the JSON Lines format.

## Assumptions

- Nothing is released, so renaming `Convert` to `Read` and changing converters is accepted (MAJOR
  4.0.0).
- The asynchronous per-telemetry serialize returns a value task and the asynchronous
  many-telemetries serialize returns a task (proposal in #137); the plan confirms or chooses a task
  for both until a benchmark shows the difference matters (C6).
- Writing is UTF-8 only, as reading is.
- One telemetry type per output is the caller's (or collector's) choice; this package does not
  partition.
- The string test format lives in the tests, not in the package's public API.
- Collection stores, the collecting directive, transmitting and partitions are #141; JSON Lines is
  #138; Parquet is later.
- There is no `string` target: a test reads a string back from a `MemoryStream`.
