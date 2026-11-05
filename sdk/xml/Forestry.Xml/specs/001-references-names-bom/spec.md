# Feature Specification: References, names compared in full, BOM

**Feature Branch**: `xml-reader-spec-kit`

**Created**: 2026-11-03

**Status**: Approved (2026-11-03)

**Issue**: #64 is this spec's feature; its stories are sub-issues: #65 (Story 1), #66 (Story 2)
and #67 (Story 3, which supersedes the earlier #31).

**Input**: User description: "Reader spec 001: references, names compared in full, BOM. Three user
stories, thin (scope agreed by the developer, 2026-11-03).
- P1 References: character references (`&#digits;`, `&#xhex;`) and the five predefined entity
  references (`amp`, `lt`, `gt`, `quot`, `apos`) in content and in attribute values; the value is
  returned raw (not resolved) with a flag that it contains references, like `Utf8JsonReader.ValueIsEscaped`;
  `Read()` never allocates for it; a reference cut off at a segment boundary halts without
  committing (C1); a bare `&`, a missing `;`, an undeclared entity (no DTD, S2) or an illegal
  character (e.g. `&#0;`) throws with line and position (S0).
- P2 Names compared in full: end tags checked against the whole start-tag name, not its first 32
  bytes; `ReaderOptions.MaxNameLength` (default 128 bytes, overridable like `MaxDepth`) throws where
  a name is first read (start tag or attribute name), so an end tag only fails on a mismatch; real
  StanForD has a 35-byte name (`ButtEndProfileExtrapolationFunction`).
- P3 BOM (#67): a UTF-8 BOM at the start is skipped (no line tracking); any other BOM, or a
  declaration naming an encoding other than UTF-8, throws not supported."

## User Scenarios & Testing *(mandatory)*

The user of this feature is a developer reading XML with `Utf8XmlReader`, directly or through
`Forestry.Deserialize.Xml`, over one span or a sequence of segments. Every scenario is tested with a
small hand-written document read whole and split at every byte position (C1), and every malformed
case throws an `XmlException` with its line and position at the first byte where it is known (S0).
`|` in an example marks a segment boundary.

### User Story 1 - Read references in text and attribute values (Priority: P1)

A developer reads a document whose text or attribute values contain character references or the
five predefined entity references (e.g. `<Comment>Spruce &amp; pine</Comment>`,
`<Species Name="Gran &#x26; tall"/>`). The reader gives the value as its raw bytes, references
included and not resolved, and flags that the value contains references so the caller knows it has
to unescape before using it. Today the reader stops at `&` and throws "unsupported".

**Why this priority**: References are well-formed XML any writer may produce (`&amp;` in a free-text
field is enough); a document using one cannot be read at all today. It is the reader's largest
remaining gap in what the grammar allows (C0).

**Independent Test**: Read documents with references in text and in attribute values, whole and
split at every byte, and compare tokens, values and the flag; read each malformed reference and
check the exception's line and position.

**Acceptance Scenarios**:

1. **Given** `<a>Spruce &amp; pine</a>`, **When** read, **Then** the tokens are Element `a`, one
   Value `Spruce &amp; pine` with the references flag set, and ElementEnd `a`.
2. **Given** `<a>Spruce and pine</a>`, **When** read, **Then** the Value `Spruce and pine` has the
   references flag cleared.
3. **Given** `<a b="&lt;&#60;&#x3C;"/>`, **When** read, **Then** the attribute's Value is
   `&lt;&#60;&#x3C;` with the references flag set.
4. **Given** each of `&amp;`, `&lt;`, `&gt;`, `&quot;`, `&apos;`, `&#65;`, `&#x41;`, `&#x1F332;` as
   the whole text of an element and as the whole of an attribute value, **When** read, **Then**
   each is one Value equal to the reference, flagged.
5. **Given** `<a>x &am|p; y</a>` (a segment ends inside a reference), **When** the first segment
   is read without the second, **Then** `Read()` halts with nothing committed, and a new reader over
   both segments continues to the same tokens as the whole document.
6. **Given** a bare `&` (`<a>x & y</a>`), a missing `;` (`<a>&amp</a>`, `<a b="&#65"/>`), an empty
   reference (`&;`, `&#;`, `&#x;`), a non-digit (`&#6A;`, `&#xG;`), an uppercase `X` (`&#X41;`), an
   entity other than the five predefined ones (`&nbsp;`), or a character reference to an illegal
   character (`&#0;`, `&#xD800;`, `&#xFFFE;`, `&#x110000;`), **When** read, **Then** an
   `XmlException` is thrown with the line and position of the first byte that shows the reference is
   malformed.
7. **Given** a document with a document type declaration (`<!DOCTYPE a SYSTEM "a.dtd"><a>&e;</a>`),
   **When** read, **Then** the declaration is not applied and `&e;` throws "not supported": the
   entity may be declared, but S2 never processes the DTD (developer, 2026-11-03). Without a
   document type declaration, `&e;` is malformed (scenario 6).
8. **Given** a document type declaration with an internal subset
   (`<!DOCTYPE a [<!ENTITY e "x">]><a/>`), **When** read, **Then** it throws "not supported" at the
   `[`: an internal subset is an opaque inside the opaque declaration, delimited by its own `[` and
   `]`, and delimiting it is later work (option A, developer, 2026-11-05). A `[` inside a quoted
   literal (`<!DOCTYPE a SYSTEM "a[1].dtd">`) is not an internal subset.

---

### User Story 2 - Compare names in full, within a limit (Priority: P2)

A developer reads a document with element names longer than 32 bytes, such as StanForD's
`ButtEndProfileExtrapolationFunction` (35 bytes). End tags are checked against the whole start-tag
name, so `<ButtEndProfileExtrapolationFunctionX>…</ButtEndProfileExtrapolationFunctionY>` throws
instead of passing on its first 32 bytes. A name longer than `ReaderOptions.MaxNameLength`
(default 128 bytes, overridable like `MaxDepth`) throws where it is first read.

**Why this priority**: Real StanForD needs names over 32 bytes, and a silently truncated
comparison accepts malformed documents (S1, C0). It is a correctness fix of what is already read,
smaller than Story 1.

**Independent Test**: Read documents whose names are 32, 33, 35, 128 and 129 bytes long, with
matching and mismatched end tags, with the default and an overridden limit.

**Acceptance Scenarios**:

1. **Given** `<ButtEndProfileExtrapolationFunction>1</ButtEndProfileExtrapolationFunction>`,
   **When** read, **Then** the tokens are Element, Value, ElementEnd, with the full 35-byte name.
2. **Given** two names identical on their first 32 bytes and differing after
   (`<ButtEndProfileExtrapolationFunctionX></ButtEndProfileExtrapolationFunctionY>`), **When**
   read, **Then** the end tag throws a mismatch at its line and position.
3. **Given** an end tag that is a prefix or an extension of the start-tag name (`<abc></ab>`,
   `<ab></abc>`), **When** read, **Then** the end tag throws a mismatch.
4. **Given** an element name or an attribute name of exactly `MaxNameLength` bytes, **When** read,
   **Then** it is read; **Given** one byte more, **Then** it throws at the first byte beyond the
   limit, before buffering more of the name.
5. **Given** `MaxNameLength` set to 16 and a 17-byte element name, **When** read, **Then** it
   throws; with the default, it is read.
6. **Given** an end tag longer than `MaxNameLength`, **When** read, **Then** it throws a mismatch
   (no start-tag name can be that long), not a name-length error.
7. **Given** a long name split across segments at every byte, **When** read, **Then** the tokens
   are the same as for the whole document.

---

### User Story 3 - Skip a UTF-8 BOM, refuse other encodings (Priority: P3)

A developer reads a file saved with a UTF-8 byte order mark (as many Windows tools write). The BOM
is skipped before the first token and is not counted in line positions. A document in another
encoding, recognised by its BOM or by its declaration's encoding name, throws "not supported"
instead of being misread (#67).

**Why this priority**: A BOM today reaches candidate None and throws a generic malformed error, so
such files cannot be read; the fix is small and self-contained.

**Independent Test**: Read the same document with and without a UTF-8 BOM, and with each other BOM
and encoding name.

**Acceptance Scenarios**:

1. **Given** `EF BB BF` followed by `<?xml version="1.0"?><a/>`, **When** read, **Then** the tokens
   are the same as without the BOM, and the declaration is still accepted as first.
2. **Given** `EF BB BF` followed by `<a>&</a>`, **When** read, **Then** the error reports the same
   line and position as without the BOM.
3. **Given** a BOM split across segments (`EF|BB BF<a/>`, `EF BB|BF<a/>`), **When** read, **Then**
   `Read()` halts until the BOM is complete, then the tokens are the same as without it.
4. **Given** a UTF-16 or UTF-32 BOM (`FE FF`, `FF FE`, `00 00 FE FF`, `FF FE 00 00`) at the start,
   **When** read, **Then** an `XmlException` "not supported" is thrown at line 1, position 1.
5. **Given** a declaration with `encoding="UTF-8"` (in any letter case), or no encoding, **When**
   read, **Then** it is read; **Given** `encoding="ISO-8859-1"` or `encoding="UTF-16"`, **Then**
   it throws "not supported" at the encoding name.

---

### Edge Cases

- A reference in text next to spacing (`<a> &amp; </a>`, `<a>\n  &amp;<b/>`) is text, not
  spacing-only character data: the Value keeps the spacing, as for any other text (READER.md rule
  18).
- Text and references form one Value up to `<`; the reader no longer ends character data at `&`.
- A value with references straddling segments is returned as a sequence with the flag set; the
  flag is the same however the input is split.
- Character references with leading zeros (`&#0065;`, `&#x0041;`, `&#x0000000041;`) are
  well-formed and read: leading zeros are insignificant, however many there are.
- A character reference with too many significant digits (`&#99999999999999999999;`) throws at
  the first digit beyond the limit (FR-005), never wraps around.
- `<` stays malformed in an attribute value, and `&lt;` is how it is written; `>` and the other
  quote stay plain bytes.
- A reference that runs to the end of input with reading completed (`<a>&amp`) throws incomplete.
- A UTF-8 BOM anywhere but the start is not skipped: in text it is plain bytes (≥ 0x80), elsewhere
  it starts no markup and throws as today.
- A document of only a BOM, or a BOM cut off with reading completed (`EF BB`), throws as an empty
  or incomplete document.
- Names in this spec stay ASCII only (READER.md rule 14).

## Requirements *(mandatory)*

### Functional Requirements

**References (Story 1)**

- **FR-001**: The reader MUST read character references (`'&#' [0-9]+ ';'` and
  `'&#x' [0-9a-fA-F]+ ';'`) and the entity references `&amp;`, `&lt;`, `&gt;`, `&quot;`, `&apos;`
  in character data and in attribute values (productions [66] CharRef, [68] EntityRef, [10]
  AttValue, [43] content).
- **FR-002**: A Value containing at least one reference MUST be returned as its raw bytes,
  references not resolved, with a flag on the reader (like `Utf8JsonReader.ValueIsEscaped`) that is
  set for that Value and cleared for every other token.
- **FR-003**: Reading references MUST NOT allocate: no per-token or per-`Read()` allocation is added
  (C2).
- **FR-004**: A reference cut off at a segment boundary MUST halt with nothing committed when
  reading is not completed, and throw incomplete when it is (C1, C3).
- **FR-005**: A malformed reference MUST throw an `XmlException` with line and position at the first
  byte where it is known (S0): a bare `&`; a missing `;`; an empty name or number; a non-digit;
  `&#X`; an entity name other than the five predefined ones (WFC: Entity Declared); a character
  reference to a character outside the grammar's Char production (WFC: Legal Character).
- **FR-005a**: A character reference names a code point by its number, so leading zeros are
  insignificant and skipped; hex digits are `0-9`, `a-f` and `A-F`. It MUST throw malformed (S0)
  when it has more than 6 significant hex digits or more than 7 significant decimal digits, when its
  value is above U+10FFFF, or when it is not a legal Char (e.g. `&#0;`, U+D800 to U+DFFF, U+FFFE,
  U+FFFF). It is not bounded by `MaxNameLength` and has no option of its own (developer,
  2026-11-03).
- **FR-006**: A document type declaration MUST stay an opaque token; entities it declares are never
  applied (S2). A declaration with an internal subset (a `[` outside its quoted literals) MUST throw
  "not supported" at the `[` before anything is committed (C0), since today's opaque read ends at
  the first `>`, which may be inside the subset.
- **FR-007**: The "unsupported" throw for `&` in character data and literals MUST be removed.

**Names (Story 2)**

- **FR-008**: An end tag MUST be compared with the whole name of the start tag it closes; any
  difference in length or bytes throws a mismatch.
- **FR-009**: `ReaderOptions.MaxNameLength` MUST limit names in bytes, default 128, overridable like
  `MaxDepth` (a zero or negative value is rejected when set, for both options, and `MaxNameLength`
  above the element stack's 2048 name bytes too, `MaxDepth` above its 64 levels; an option never
  set gets its default). It is carried by the reader state like the other options. `MaxDepth` is
  enforced: a start tag at a depth equal to `MaxDepth` throws a limit error at that start tag,
  before committing (S1) (developer, 2026-11-03).
- **FR-010**: Every name the reader reads MUST be bounded by `MaxNameLength`: element names (in a
  start tag), attribute names, processing instruction targets and entity names in references. A
  longer element, attribute or entity name throws at the first byte beyond the limit, before more
  of it is buffered (S1). A processing instruction target is checked against `MaxNameLength` once
  the instruction is delimited (after `?>` is found): the instruction's body is unbounded anyway
  until the later `MaxValueLength` spec, which is what bounds that buffering (developer,
  2026-11-03). The XML doc on `ReaderOptions.MaxNameLength` MUST list exactly which
  names it bounds, since the option's name alone is generic (D0).
- **FR-011**: An end tag MUST only fail on a mismatch: an end-tag name longer than any start-tag name
  can be is a mismatch, reported without buffering beyond the start-tag name plus one byte.

**BOM and encoding (Story 3)**

- **FR-012**: A UTF-8 BOM (`EF BB BF`) as the first bytes of the document MUST be skipped without
  advancing line or position, before the first token is chosen; a cut-off BOM halts like any other
  incomplete markup.
- **FR-013**: A UTF-16 or UTF-32 BOM at the start MUST throw an `XmlException` "not supported" at
  line 1, position 1.
- **FR-014**: A declaration whose encoding name is not `UTF-8` (compared without letter case, as XML
  1.0 §4.3.3 says) MUST throw "not supported" at the encoding name; a declaration without an
  encoding is read as UTF-8. The Declaration token's value stays the raw declaration.

**Across stories**

- **FR-015**: "Not supported" throws MUST be distinguishable from malformed throws (C0); their
  final wording is #49's.
- **FR-016**: Each production and WFC this spec touches MUST have tests, written as shells and
  reviewed by the developer before code (C4), labelled Arrange/Act/Assert (A4).

### Key Entities

- **Reference**: A character reference or one of the five predefined entity references, inside a
  Value; never a token of its own.
- **References flag**: Tells, for the current Value only, whether its raw bytes contain at least one
  reference. Lives for one `Read()`, like `Value` (READER.md rule 23).
- **MaxNameLength**: The reader option bounding names in bytes, default 128.
- **BOM**: The byte order mark at the start of a document; only the UTF-8 one is accepted.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every acceptance scenario's document gives identical tokens, values and references
  flags when read whole, as two segments split at every byte position, and as input arriving in
  parts (C1).
- **SC-002**: Every malformed and not-supported case listed in this spec throws with the expected
  line and position (100% of the listed cases).
- **SC-003**: A read loop over a document with references in text and attribute values allocates
  nothing beyond what the same loop allocates without references (C2).
- **SC-004**: A StanForD-shaped document with `ButtEndProfileExtrapolationFunction`, saved with a UTF-8 BOM
  and containing `&amp;` in text, reads to the end with the default options.
- **SC-005**: The performance gate (`dotnet test -s Performance.runsettings`) passes at the end of
  each story (A4).

## Assumptions

- The flag only says references are present; unescaping is the caller's job. A getter that unescapes
  on request is a typed getter, out of scope (C3). The bridge `Forestry.Deserialize.Xml` unescaping
  before converters is a follow-up in the deserialization silo.
- When the flag is set, every reference in the Value is well-formed: the reader has already thrown
  for any malformed one.
- Encodings are recognised only by a BOM or the declaration's encoding name; a UTF-16 document
  without a BOM or declaration starts no markup and throws as today. The rest of the declaration
  stays unchecked (opaque), as today.
- Out of scope, later specs or bugs: #30 line position (a bug, fixed with a test), CDATA,
  skip/try-skip and #33, typed getters, #49 messages, lone `\r` line ends, non-ASCII names, whether
  `Extensions` stays public, `]]>` in character data.
- Out of scope, a later limits spec (S1, developer 2026-11-03): a running size limit on the open
  element names (the name stack), and `ReaderOptions.MaxValueLength`. An endless run of leading
  zeros in a character reference is an oversized value like any long text and is left to
  `MaxValueLength`. A per-field string length limit (an attribute on a field) belongs to the
  deserialization silo.
- Not supported after this spec (C0's list): CDATA sections, entities declared in a DTD (could come
  later if a use case comes up or another contributor joins), a document type's internal subset
  (delimiting `[` … `]` while skipping its literals, comments and processing instructions is later
  work), encodings other than UTF-8, non-ASCII names.
