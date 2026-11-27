# Feature Specification: JSON Lines

**Feature Branch**: `serialization-005-json-lines`

**Created**: 2026-11-26

**Status**: Draft

**Input**: User description: "Spec 005 for Forestry.Serialization: the JSON Lines format package
(GitHub issue #138; constitution 4.0.1 on branch serialization-005-json-lines)." Source: GitHub
issue ForestryAI/forestry-sdk-for-net#138. Spec 004 (#137) built the writing side: converters that
write, the abstract `TelemetryWriter`, and serializing to a `Stream` or `PipeWriter`. No real
format writes yet, and some member types of flat telemetries have no built-in converter at all.
This spec adds the missing built-in converters, lets StanForD's enums be written, and adds the
first real writing format, JSON Lines, so a StanForD Harvesting document makes a round trip:
HPR XML → Stem and Log telemetries → JSON Lines, each line parsed back and compared. Like spec
003, it spans the core package and two format packages
(XML for reading enums, JSON Lines for writing). Partitions are out of scope: they belong to
the client's big data resource directive (#147, developer 2026-11-26).

## Clarifications

### Session 2026-11-26

- Q: How does the built-in enum converter represent a value? → A: As a name or a number. Each
  format documents its default (JSON Lines: names), and the user has the last word (developer).
  How the user overrides it was revised by the last clarification: by a format attribute on the
  telemetry member, not a serialize option.
- Q: How is a `double` that is NaN or infinite written as JSON Lines? → A: The format's writer for
  that type's value decides; JSON Lines refuses it with an exception. Letting the user override
  such "overflow" problems (a value the format cannot represent) may come later (developer).
- Q: The enum value's form cannot come from the token kind, as in System.Text.Json's
  `EnumConverter` (`String` or `Number`), since XML has none. How is it known when reading? → A:
  As with `XmlBooleanConverter`: the format declares it. Forestry.Serialization.Xml gets an XML enum
  converter and an XML attribute saying whether the document writes the enum as a name or a
  number; names by default. In scope of this feature (developer). The attribute's place was revised
  by the last clarification: on the telemetry member, not the enum type.
- Q: Where do enum converters share their work? → A: Forestry.Serialization provides a base
  abstract enum converter that resolves the enum's fields once (names, values, lookups both ways),
  which the built-in, XML and StanForD enum converters build on (developer).
- Q: Which writer does the JSON Lines package write with? → A: System.Text.Json's `Utf8JsonWriter`,
  kept by the JSON Lines telemetry writer over its output: it writes names, values, commas and
  escaping, and refuses non-finite doubles. Since it buffers, values go only through the telemetry
  writer's value methods (`WriteValue`, `WriteRaw`); writing bytes directly through its
  `IBufferWriter<byte>` members is not supported by the JSON Lines writer and throws, which is
  documented (option b; making those members overridable can come later if a converter needs it)
  (developer).
- Q: How are strings escaped? → A: Relaxed escaping (System.Text.Json's
  `UnsafeRelaxedJsonEscaping`): non-ASCII text such as Swedish names stays plain UTF-8; only what
  JSON requires is escaped. The output is for blob storage and ADX, never embedded in HTML
  (developer).
- Q: How does the StanForD enum converter know the StanForD text to write for each member? → A: A
  general member-name attribute in Forestry.Serialization (e.g. `[EnumMemberName("Other manual")]`,
  name settled in the plan), resolved by the base enum converter for reading and writing; StanForD's
  enums declare their texts with it. A5 allows it: it names a value, not a part of a document or an
  output (developer, /speckit-clarify).
- Q: Should StanForD still register an enum converter of its own? → A: Yes: a StanForD enum
  converter built on the base enum converter, still reading texts with or without spaces (ignoring
  case), and always writing the StanForD text whatever a format's enum attribute says (developer,
  /speckit-clarify).
- Q: Does the XML package read `TimeSpan` as XML Schema's duration (`PT1H30M`) or as the core's
  constant form? → A: As `xs:duration`, through an XML `TimeSpan` converter registered beside
  `XmlBooleanConverter` (days, hours, minutes and seconds; years and months refused). Writing keeps
  the core's constant form (developer, /speckit-clarify).
- Q: Is the enum form one setting in the serialize options, or can it differ per enum? → A:
  Neither: no setting in the options. Each format has its own attribute, decorating the telemetry
  member as `XmlElementName` does, that overrides the format's default (names): the XML package's
  attribute says how documents write that member's enum (reading), a JSON Lines attribute says how
  lines write it (writing). Reading and writing stay independent, and two members of the same enum
  type can differ, so converters are chosen per member (developer, /speckit-clarify).

## User Scenarios & Testing *(mandatory)*

The users of this feature are:

- a **developer** who has flat telemetries with enum, date, time or duration members and wants
  them read and written without writing converters of their own
- a **developer** of the POC who wants StanForD Harvesting telemetries written as JSON Lines, one
  telemetry per line, for Azure Data Explorer external tables
- a **format package author** (Parquet later) who expects every flat member type to already have a
  built-in converter and a `TelemetryWriter` method to override
- a **reader** of the documentation who needs to know what the JSON Lines package writes and what
  it does not (no reading back, no options, no collecting, no partitions yet)

Reading keeps being tested through the real XML format package; the core's own writing tests keep
using the string test format (C3). The JSON Lines package tests its own writer in its own tests.
The round trip lives where the XML and JSON Lines packages are combined with StanForD (C6).
Nothing is released, so changes to the core's public API are accepted.

### User Story 1 - Every flat member type has a built-in converter (Priority: P1)

A flat telemetry's member can be an enum, a `DateOnly`, a `TimeOnly` or a `TimeSpan`, but today
none of these has a built-in converter: a developer must register one or the definitions refuse
the member. After this feature, the serialization package has built-in converters for all four,
each reading and writing, and the `TelemetryWriter` has a value method for each kind, so a format
(or a developer) can decide how dates, times, durations and enums are represented, just as it
already can for `DateTimeOffset` or `Guid`.

**Why this priority**: The JSON Lines writer and the StanForD round trip both need every member
type to be writable. Doing all four now means Parquet later starts with every flat member type
covered.

**Independent Test**: In the core's own tests, read a value of each new kind from its UTF-8 text,
write it through the string test format, and read the written text back; override one of the new
writer methods in a test writer and see it used.

**Acceptance Scenarios**:

1. **Given** a telemetry with an enum member, a `DateOnly` member, a `TimeOnly` member and a
   `TimeSpan` member, **When** its type definition is built with no converters registered,
   **Then** each member resolves a built-in converter instead of being refused.
2. **Given** a value of each new kind, **When** its built-in converter writes it and reads the
   written bytes, **Then** the value read equals the value written.
3. **Given** a test writer overriding the value method for one of the new kinds, **When** a member
   of that kind is serialized, **Then** the override writes it, and members of other kinds are
   unchanged.
4. **Given** a nullable member of a new kind (e.g. `DateOnly?`) with and without a value, **When**
   it is serialized, **Then** the value is written by the kind's converter, and the absence is
   written as the format's null.
5. **Given** a custom converter registered for one enum type, **When** a member of that enum type
   is read or written, **Then** the custom converter is used, not the built-in enum converter.
6. **Given** an enum member with no format attribute, **When** it is serialized with the string
   test format and read back, **Then** it is written as its member name and reads back to the same
   value; a base enum converter created for numbers writes and reads the number.

---

### User Story 2 - StanForD's enums can be written (Priority: P1)

StanForD Harvesting telemetries have enum members (`Stem.ProcessingCategory`,
`Stem.CoordinateReferenceSystem`, `Log.CuttingReason`). StanForD's own converter reads the
StanForD text (e.g. "Other manual" to `OtherManual`) but cannot write, so serialize options refuse
`Stem` and `Log` when definitions are built. After this feature, StanForD's enums declare their
texts with the serialization package's member-name attribute, so each member is written as the
StanForD text it was read from.

**Why this priority**: Without it, no StanForD telemetry can be serialized at all, in any format.

**Independent Test**: In StanForD's tests, read each member of each StanForD enum from its
StanForD text, write it, and compare the written text with the original.

**Acceptance Scenarios**:

1. **Given** each member of each StanForD enum, **When** it is written by the StanForD enum
   converter, **Then** the text written is the StanForD text that reads back to that member (e.g.
   `OtherManual` writes "Other manual", `WGS84` writes "WGS84").
2. **Given** serialize options built from StanForD's Harvesting options, **When** definitions for
   `Stem` and `Log` are built, **Then** no member is refused for lacking a write side.
3. **Given** a StanForD text written without its spaces or in another case (e.g. "OtherManual",
   "other manual"), **When** it is read, **Then** it reads to the member, as today.
4. **Given** a StanForD enum member decorated with a format's attribute for numbers, **When** it
   is written, **Then** it is still written as its StanForD text (StanForD's registered converter
   wins).

---

### User Story 3 - Telemetries written as JSON Lines (Priority: P1)

A new package, `Forestry.Serialization.JsonLines`, writes each telemetry as one JSON object on its
own line, through the serialization package's existing entry points (`TelemetrySerialization.Serialize`
and `SerializeAsync`, to a `Stream` or a `PipeWriter`). Member names are written as JSON property
names; text, `Guid`, dates, times, durations and enums as escaped JSON strings; numbers and
booleans unquoted; nulls as JSON `null`. Every line parses as JSON on its own.

**Why this priority**: It is the first real writing format and the format the POC uses for ADX
external tables.

**Independent Test**: In the JSON Lines package's own tests, serialize hand-built telemetries
covering every member kind to a `MemoryStream`, split the output into lines, and parse each line
with `System.Text.Json`, comparing every property with the telemetry's member.

**Acceptance Scenarios**:

1. **Given** several telemetries of one type, **When** they are serialized one after another to
   one output, **Then** the output has exactly one line per telemetry, each ending in a new line,
   each a complete JSON object.
2. **Given** a telemetry with a member of each kind (text, `bool`, `int`, `long`, `double`,
   `decimal`, `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, an enum, and
   nullable members with and without a value), **When** its line is parsed, **Then** each property
   has the JSON kind expected (string, number, `true`/`false`, `null`) and its value equals the
   member's.
3. **Given** text containing quotes, backslashes, control characters, new lines and non-ASCII
   characters, **When** it is written, **Then** the line stays one line, non-ASCII characters are
   written as plain UTF-8 (not `\u` escapes), and it parses back to the exact text.
4. **Given** the async entry point and a `PipeWriter`, **When** telemetries are serialized, **Then**
   the bytes written equal those of the `Stream` entry point, and the `PipeWriter` is left
   uncompleted (C8).
5. **Given** a collection writer (spec 004) wrapping the output, **When** JSON Lines telemetries are
   serialized into it, **Then** its sequence count equals the number of lines written.
6. **Given** an enum member with no JSON Lines attribute, **When** it is written, **Then** it is its
   member name as a JSON string; **Given** the member's JSON Lines attribute says numbers, **Then**
   it is its number, unquoted; **Given** two members of the same enum type, one with the attribute,
   **Then** each is written in its own form.
7. **Given** a `double` member that is NaN or infinite, **When** it is written, **Then** an
   exception names the value.
8. **Given** a custom converter writing bytes directly through the writer's `IBufferWriter<byte>`
   members, **When** it is used with the JSON Lines writer, **Then** an exception says to use the
   value methods instead.

---

### User Story 4 - XML documents read enums and durations as XML writes them (Priority: P2)

Reading cannot tell a name from a number by the token, since XML text has no string or number
kind. Like `XmlBooleanConverter`, the XML format package registers its own enum converter, which
reads an enum as a name or as a number depending on an XML attribute on the telemetry member;
without the attribute, names.

**Why this priority**: The milestone does not need it (StanForD registers its own enum converter),
but every XML document with enums written as numbers needs it, and it is the reading half of the
per-format enum attributes.

**Independent Test**: In the XML package's own tests, read hand-written documents with an enum
member with no attribute, the attribute set to names, and the attribute set to numbers.

**Acceptance Scenarios**:

1. **Given** an enum member with no XML attribute, **When** a document's text names a member
   (ignoring case), **Then** the member is read; a number fails with a format error.
2. **Given** an enum member whose XML attribute says numbers, **When** the document's text is the
   member's number, **Then** the member is read; a name fails with a format error.
3. **Given** a converter registered by the developer for that enum type, **When** it is read,
   **Then** the developer's converter wins over the XML enum converter.
4. **Given** a `TimeSpan` member and XML Schema duration text (e.g. `PT1H30M`, `P2DT3H`,
   `-PT0.5S`), **When** it is read, **Then** the duration equals the text's; a duration with years
   or months (e.g. `P1M`) fails with a format error.

---

### User Story 5 - A StanForD Harvesting document makes a round trip (Priority: P1)

The milestone of #137 and #138 (C6): a StanForD Harvesting document is read with the XML format
package into Stem and Log telemetries, each telemetry type is written as JSON Lines to its own
output, and each line is parsed back with `System.Text.Json` and compared with the telemetry it
came from: every member present, with dates, decimals, enums and nulls unchanged.

**Why this priority**: It is the working example that must exist before any performance work on
writing (C6), and it proves the converters, the StanForD enum write and the JSON Lines writer
together on real data.

**Independent Test**: In StanForD's tests, read a committed Harvesting fragment, serialize its
Stem and Log telemetries as JSON Lines, and compare each parsed line with its telemetry; the same
test over real documents runs when the real-documents folder is set.

**Acceptance Scenarios**:

1. **Given** a committed StanForD Harvesting fragment, **When** its Stem and Log telemetries are
   written as JSON Lines, one output per telemetry type, **Then** each output has one line per
   telemetry of that type and every parsed line equals its telemetry, member by member.
2. **Given** the real-documents folder is set, **When** every real Harvesting document in it makes
   the round trip, **Then** no line differs from its telemetry; when it is not set, the test is
   skipped, not failed.

---

### Edge Cases

- An enum value that is not a defined member (e.g. a cast integer), written in name form: refused
  when written, with an exception naming the value and the enum, as a value the form cannot
  represent (R59, confirmed 2026-11-27). In number form it is written as its number.
- An enum converter reads only the one form it was created with (name or number); it never
  guesses the form from the text.
- A `[Flags]` enum value combining members: written and read as the combination's names (see
  Assumptions).
- Enum text in a document differing only in case from a member name: read to the member.
- Text naming no enum member, or malformed date, time or duration text: reading fails with a format
  error, as for the existing converters.
- A `double` that is NaN or infinite has no JSON number form: the JSON Lines writer refuses it
  with an exception naming the value. The output is then part-written (the line was started), so
  whoever collects abandons that collection through its collection writer (spec 004). S1's "never
  part-way" covers members a format cannot write, decided when definitions are built; this is a
  value, known only when written.
- A `DateTime` with an unspecified kind, or a `DateTimeOffset` with an offset: written in the
  writer's existing round-trip format, so the parsed value equals the member.
- A `decimal` with more digits than a `double` holds: written unquoted with every digit, and
  compared after parsing as `decimal`, not `double`.
- An empty telemetry type (no members) writes `{}` and a new line.
- A member name needing escaping in JSON (not possible for C# member names today): names are
  written escaped all the same.

## Requirements *(mandatory)*

### Functional Requirements

**Built-in converters (Forestry.Serialization)**

- **FR-001**: The serialization package MUST provide built-in converters, each with `Read` and
  `Write`, for every enum type, `DateOnly`, `TimeOnly` and `TimeSpan`, resolved when definitions
  are built without anything registered, and for their nullable forms through the existing
  nullable handling.
- **FR-002**: An enum is read and written as its member name or as its underlying number. Each
  format package sets and documents its default (names for XML and JSON Lines), and provides an
  attribute on the telemetry member that overrides it for that member (FR-002b, FR-002d); there is
  no enum setting in the options. Without a format's enum converter, the built-in enum converter
  reads and writes names (ignoring case when reading). No enum converter guesses the form from the
  text. Converters are chosen per member, so two members of the same enum type can differ.
- **FR-002a**: The serialization package MUST provide a base abstract enum converter that resolves
  an enum type's fields once (member names, values, and lookups from name to value and value to
  name) and reads and writes a given form (name or number). A member's name is its C# name unless a
  member-name attribute (FR-002c) gives another. The built-in, XML and StanForD enum
  converters MUST build on it.
- **FR-002b**: The XML format package MUST register an XML enum converter, as it registers
  `XmlBooleanConverter`, and provide an XML attribute on a telemetry member declaring whether
  documents write that member's enum as a name or a number; without the attribute, names (A5: the
  attribute belongs to the format package).
- **FR-002c**: The serialization package MUST provide a member-name attribute for enum members that
  gives the name a member is read from and written as, in every format, in place of its C# name.
- **FR-002d**: The JSON Lines package MUST register a JSON Lines enum converter and provide a JSON
  Lines attribute on a telemetry member declaring whether lines write that member's enum as a name
  (a JSON string) or a number (unquoted); without the attribute, names.
- **FR-002e**: A converter registered for a type in the options MUST win over a format's enum
  converter and its member attributes (FR-005), as StanForD's does.
- **FR-003**: The `DateOnly`, `TimeOnly` and `TimeSpan` converters MUST write a culture-invariant
  text that round-trips exactly (ISO 8601 date, time with fractional seconds, and the constant
  duration form) and read that text back.
- **FR-003a**: The XML format package MUST register an XML `TimeSpan` converter, as it registers
  `XmlBooleanConverter`, reading XML Schema duration text (`xs:duration`: an optional sign, days,
  hours, minutes and seconds with fractions); a duration with years or months MUST fail with a
  format error, since they have no fixed length.
- **FR-004**: The `TelemetryWriter` MUST have an overridable value method for each new kind
  (`DateOnly`, `TimeOnly`, `TimeSpan`, and enums), with a plain UTF-8 default, and the new
  converters MUST write through them, so overriding one changes every member of that kind
  (constitution C0, 4.0.1).
- **FR-005**: A converter registered in the options for a specific type MUST win over the built-in
  converters for that type, as today.

**StanForD (Forestry.StanForD)**

- **FR-006**: StanForD's enums MUST declare each member's StanForD text with the member-name
  attribute (FR-002c). StanForD MUST keep registering its own enum converter, built on the base
  enum converter (FR-002a), which reads a text with or without its spaces, ignoring case (as
  today), and always writes the declared StanForD text (e.g. `OtherManual` as "Other manual"),
  whatever a format's enum attribute says (FR-002e).
- **FR-007**: Serialize options built from StanForD's Harvesting options MUST build definitions for
  `Stem` and `Log` without refusing any member.

**JSON Lines (Forestry.Serialization.JsonLines)**

- **FR-008**: A new package `Forestry.Serialization.JsonLines` MUST provide serialize options and a
  `TelemetryWriter` subclass so that `TelemetrySerialization.Serialize`/`SerializeAsync` write
  JSON Lines; it references the serialization package and nothing beyond what .NET ships (C0, A1).
- **FR-009**: Each telemetry MUST be written as one JSON object followed by one new line (`\n`),
  with no new line inside it, and members in their definition order.
- **FR-010**: Each member's name MUST be written as an escaped JSON property name, the member's C#
  name unchanged.
- **FR-011**: Text, `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan` and
  enum values MUST be written as JSON strings with relaxed escaping (only what JSON requires; non-ASCII
  text stays plain UTF-8);
  integer and decimal numbers and booleans MUST be written unquoted; a member without a value MUST
  be written as `null`.
- **FR-011a**: A value the format cannot represent MUST be refused by the format's writer for that
  value's type with an exception naming the value; for JSON Lines, a `double` that is NaN or
  infinite. No override for such values is added in this feature.
- **FR-011b**: The JSON Lines writer MUST accept values only through the telemetry writer's value
  methods (`WriteValue` overloads and `WriteRaw`, the latter given a complete JSON value); a converter
  writing bytes directly through the writer's `IBufferWriter<byte>` members MUST fail with an
  exception saying so, never write out of order.
- **FR-012**: The JSON Lines package MUST NOT read JSON Lines back into telemetries, and MUST NOT
  add options of its own (naming, null omission, number or date formats,
  compression, overriding unrepresentable values), collecting settings or partitions in this
  feature.
- **FR-013**: The JSON Lines package MUST work with spec 004's collection writers, flushing and
  never closing the output it is handed (C8).

**Milestone, build and documentation**

- **FR-014**: A test in StanForD's tests MUST make the round trip of US5 on a committed fragment,
  and over real documents when the real-documents folder is set.
- **FR-015**: The JSON Lines package MUST follow the XML package's layout (its own folder, solution,
  `src/` and `test/`) and the SDK's shared engineering, and be built, tested and packed by CI like
  the other packages.
- **FR-016**: Public members MUST have XML documentation, and the feature MUST add a how-to page
  for writing JSON Lines and update the READMEs and changelogs of the packages it changes (D0).

### Key Entities

- **Built-in converter (enum, `DateOnly`, `TimeOnly`, `TimeSpan`)**: reads a member's UTF-8 text
  and writes it back through the telemetry writer's value method for its kind.
- **Base enum converter**: the abstract converter in the serialization package that resolves an
  enum type's fields once and reads and writes one form, name or number.
- **XML `TimeSpan` converter**: the XML package's converter reading `xs:duration` text.
- **XML enum converter and attribute**: the XML package's enum converter, and the attribute on a
  telemetry member declaring the form XML documents use for it (names by default).
- **JSON Lines enum converter and attribute**: the JSON Lines package's enum converter, and the
  attribute on a telemetry member declaring the form lines use for it (names by default).
- **Telemetry writer value method (new kinds)**: the overridable method a format uses to decide how
  a date, time, duration or enum is represented (JSON: a quoted string).
- **Enum member-name attribute**: gives an enum member the name it is read from and written as
  (e.g. StanForD's "Other manual"), resolved by the base enum converter.
- **StanForD enum converter**: maps StanForD texts with spaces to enum members and, now, back,
  using the texts declared with the member-name attribute.
- **JSON Lines serialize options**: the options a developer passes to serialize as JSON Lines;
  creates the JSON Lines telemetry writer.
- **JSON Lines telemetry writer**: frames each telemetry as `{`, `"name":value` pairs separated by
  commas, `}` and a new line; quotes and escapes strings.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A telemetry with members of every flat kind (including enums, dates, times and
  durations, nullable or not) builds its definitions with no converter registered, for both reading
  and writing.
- **SC-002**: For every new built-in converter, writing a value and reading it back gives the same
  value, for at least the boundary values of each kind (minimum, maximum, zero, fractions).
- **SC-003**: Every member of every StanForD enum writes the StanForD text it is read from.
- **SC-004**: 100% of lines written for a committed StanForD Harvesting fragment parse as JSON and
  equal their telemetries member by member; the same holds for every real document when the
  folder is set.
- **SC-005**: Output written for text with every JSON-special character stays one line per
  telemetry and parses back to the exact text.
- **SC-006**: The new package's tests run in CI on every pull request, alongside the existing core
  (119), XML (188) and StanForD (46 + 1 skipped) tests, which keep passing.
- **SC-007**: An XML document writing an enum as a name, or, with the member's XML attribute,
  as a number, reads every member correctly; the wrong form fails with a format error, never a
  wrong member; XML Schema durations without years or months read to the exact `TimeSpan`.

## Driving needs

- #138: the first big data format, JSON Lines, for ADX external tables during the POC.
- **Why JSON Lines first** (developer, 2026-11-27, after US3): it is readable by people (one line of
  plain JSON per telemetry), takes very little code on top of the core (about 120 lines in the
  package, the telemetry writer about 75), and is easy to test (parse each line back). That makes it
  the reference for anyone writing their own format: the package README's "Writing your own format"
  walks through it.
- #137 (spec 004): writing building blocks the format plugs into; R57, value overloads come with
  their converters.
- Missing built-in converters noted after the README series: enums, `DateOnly`, `TimeOnly`,
  `TimeSpan`.
- Parquet later: every flat member type covered before a columnar format starts.

## Assumptions

- **Partitions are not part of this feature**: partitioning belongs to the client's big data
  resource directive in the pipe context (#147, client work, developer 2026-11-26). StanForD's
  `#region partitioning` stays as it is.
- **One telemetry type per output** (constitution, Output): the round trip writes Stem and Log to
  separate outputs.
- **Nulls are written as `null`**, never omitted, in this feature; omission is a later option.
- **Member names are the C# names** (e.g. `LogKey`); naming policies are a later option.
- **Text formats**: dates and times keep spec 004's round-trip format (`DateTime`,
  `DateTimeOffset`); `DateOnly` as `yyyy-MM-dd`, `TimeOnly` as `HH:mm:ss.fffffff`, `TimeSpan` in
  the constant form (`[-][d.]hh:mm:ss[.fffffff]`), which ADX also parses as a timespan.
- **`[Flags]` enums**: written as their combined member names (comma separated), as .NET formats
  them; StanForD has none today.
- **StanForD texts**: each StanForD enum member maps to exactly one StanForD text, declared on the
  member with the member-name attribute (e.g. "Grade break"); a member whose text is its C# name
  (e.g. `WGS84`, `Decay`) needs none.
- **Real documents** are never committed (they hold personal data); the real-document round trip
  runs locally when `STANFORD_HPR_FOLDER` is set, and is skipped in CI.
- **Required checks**: adding the package to the CI matrix adds a `build-test
  (Forestry.Serialization.JsonLines)` check, which the developer adds to the main ruleset's
  required checks.
- **No reader, no options, no collecting settings, no compression** in this
  feature (#138, Later). Overriding values a format cannot represent ("overflow", e.g. NaN in JSON)
  is a candidate for later.
- **StanForD's enums are not affected by format enum attributes**: StanForD registers its own
  converter, which wins and writes the StanForD text (FR-002e, FR-006).
