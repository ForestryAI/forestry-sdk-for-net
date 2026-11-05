---

description: "Tasks for reader spec 001: references, names compared in full, BOM"
---

# Tasks: References, names compared in full, BOM

**Input**: Design documents from `specs/001-references-names-bom/` (plan.md, spec.md, research.md
R1–R9 as reviewed by the developer on 2026-11-03, data-model.md, contracts/public-api.md,
contracts/exceptions.md, quickstart.md) and `.specify/memory/constitution.md` 1.0.0.

**Issue**: #64. Each story is a sub-issue of #64: US1 #65 (with Setup), US2 #66, US3 #67 (with
Polish).

**Tests**: Required (constitution C4). In every story phase: test shells first, one per acceptance
scenario, edge case or contract row; then a **test review gate** (the developer reviews the shells
before any code); then implementation; then the story's documentation and its **performance gate**.

- **Every shell labels its steps** `// Arrange`, `// Act`, `// Assert` (combined when one statement
  does two) and reads its own document, written one element per line where it is short enough (A4).
- **Every document is read whole and cut up** with the shared helpers of T004: `ReadWhole`,
  `ReadEverySplit` (two segments split at every byte position) and `ReadInParts` (a new reader per halt, built from the previous
  `ReaderState`); the three give identical tokens, values and `HasEscapedValue` (C1, SC-001).
- **Constraints are tested as units** (developer, 2026-11-03), like `ElementStack` in
  `test/ElementNameStackTests.cs`: one test file per constraint feeds `Evaluate` directly, whole and
  chunk by chunk at every split. Reader-level shells keep what only the reader shows (line and
  position, nothing committed on halt or throw, `HasEscapedValue` on the token, the comparison of
  `ReadWhole`, `ReadEverySplit` and `ReadInParts`) with a few representative rows.
- **Exception shells assert** `XmlException.Category`, `Markup`, `LineNumber` and `LinePosition`,
  never message text (#49 owns the wording). New categories are asserted as strings (`"NotSupported"`,
  `"Limit"`) so the shells compile before `Throwing` has them.
- **A shell that uses new API does not compile** (`HasEscapedValue` in US1, `MaxNameLength` in US2)
  until its story's first implementation task declares it, so the test project does not build
  between a story's shells and that task. Accepted in the deserialization silo (developer,
  2026-10-30); stories are built one at a time (P1 → P2 → P3).
- **Performance tests are a gate at the end of every story**: trait `Category=Performance`, left
  out of the default run by `test/Forestry.Xml.Tests.runsettings`, run with
  `dotnet test -s test/Performance.runsettings`. The package has none yet (R9), so Setup adds the
  first one.

**Organization**: Setup, then one phase per story in priority order, then Polish. There is no
Foundational phase: C4 puts every story's shells before any of its code, and each story declares the
surface it needs (`Throwing` categories land with the first story that uses them: `NotSupported` in
US1, `Limit` in US2).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1, US2, US3 (spec.md user stories)
- Paths are relative to `sdk/xml/Forestry.Xml/`

## Not in this spec

CDATA, typed getters and unescaping, skip/try-skip (#33), #49 message wording, #30 line position counting characters (its one-line `>` fix is in T025),
lone `\r` line ends, non-ASCII names, a running limit on the open names and `MaxValueLength`
(the later limits spec), the bridge `Forestry.Deserialize.Xml`
unescaping before converters (deserialization silo).

---

## Phase 1: Setup (Build Check and Shared Test Infrastructure)

**Purpose**: Switch on the XML-doc check D0 assumes, then the first performance test, so the gate
at the end of US1 runs something, then the helpers every story's shells use. No behaviour
changes (T001 touches only the project file and doc comments). Reviewed with US1's shells at T017.

- [X] T001 Switch on D0's XML-doc check (behaviour unchanged): in `src/Forestry.Xml.csproj` add `<GenerateDocumentationFile>true</GenerateDocumentationFile>` and `<WarningsNotAsErrors>$(WarningsNotAsErrors);CS1591</WarningsNotAsErrors>` as `sdk/deserialize/Forestry.Deserialize/src/Forestry.Deserialize.csproj` does (`eng/Directory.Build.Common.props` sets `TreatWarningsAsErrors`, so CS1591 stays a warning). Measured on 2026-11-03 without editing the project: **51 CS1591 per target framework** (39 `EBNF` constants and nested members, 9 `TokenType` members, `ReaderOptions` and `ReaderOptions.MaxDepth`, the `ReaderState(ReaderOptions)` constructor), and **8 malformed doc comments that stay errors** and must be fixed for the build to pass: `cref` given a URL in `src/EBNF.cs:156`, `:175`, `:208` (use `<see href="…"/>`); a stale `<param name="startingTerminalLength">` in `src/Reading/CandidateNonTerminal.cs:175`; `cref="ElementNameStack"` in `src/Reading/Extensions.cs:16` (now `ElementStack`); `</remarks>` closing a `<summary>` in `src/Reading/Utf8XmlReader.cs:226`; a raw `<` in `src/Reading/Utf8XmlReader.cs:1020`–`1021` (write `&lt;`). Fix the 8 and write the 51 docs in this task (one line each for the constants and enum members), then build: no CS1591 and no other doc diagnostics
- [X] T002 Add the first performance test class `test/ReadAllocationTests.cs`, trait `Category=Performance` (pattern: the deserialization silo's `MemoryTests`): generate a document of 10,000 `<Log Key="n"><Species>Spruce and pine</Species></Log>` elements without references; read it to the end as one span and as a sequence of 4 KB segments; measure `GC.GetAllocatedBytesForCurrentThread()` from after construction to after the last `Read()`; assert zero bytes (C2). If today's reader allocates, report the number to the developer instead of loosening the assertion. Done 2026-11-05: the first segmented read in a process allocates 376 bytes (net9) or 432 (net10) once, whatever the document's size, and 0 afterwards (checked up to 100,000 elements and with 64-byte segments), so the test reads the document once before measuring; the assertion stays zero
- [X] T003 Run `dotnet test Forestry.Xml.slnx` (default run, T002 left out) and `dotnet test Forestry.Xml.slnx -s test/Performance.runsettings` (T002 only): both pass on the current code, the baseline for every story's gate (depends on T001, T002), before T004
- [X] T004 Move `Sequence(params string[])` and `SequenceSplitAt(string, params int[])` from `test/Utf8XmlReaderTests.PeekStartingTerminal.cs` into a new shared partial `test/Utf8XmlReaderTests.Documents.cs`, and add: a token record `ReadToken(TokenType Type, string Value, bool HasEscapedValue)`, carrying the flag from the start so US1's shells are written once in their final shape; `ReadWhole(byte[] document, ReaderOptions options = default)` returning every token to the end; `ReadEverySplit(byte[] document, ReaderOptions options = default)` reading the document as two segments split at every byte position (reading completed) and asserting each list equals `ReadWhole`'s; `ReadInParts(byte[] document, ReaderOptions options = default)` feeding one more byte at a time with `isReadingCompleted: false`, building each new reader from the previous `ReaderState` over the bytes from the committed `Position` on, the last part read with `isReadingCompleted: true`, and asserting the result equals `ReadWhole`'s; `ReadThrown(byte[] document, ReaderOptions options = default)` returning the `XmlException` of a whole read; and `ReadThrownEverySplit` and `ReadThrownInParts` (same parameters), which read the same way as `ReadEverySplit` and `ReadInParts` and assert every read throws an `XmlException` with the same `Category`, `Markup`, `LineNumber` and `LinePosition` as `ReadThrown`'s. Update `PeekStartingTerminal`'s callers; behaviour unchanged. From here the test project does not build until T018 declares `HasEscapedValue` (that is why the baseline run comes first)

**Checkpoint**: Helpers and the performance baseline are in place; no behaviour in `src/` has changed (T001: project file and doc comments only).

---

## Phase 2: User Story 1 - Read references in text and attribute values (Priority: P1) 🎯 MVP

**Goal**: Character references and the five predefined entity references in content and attribute
values are read as part of one raw Value, flagged by `HasEscapedValue`, with no allocation; malformed
references throw at the first byte where they are known; an entity after a document type throws
"not supported" (FR-001 to FR-007, R1–R3, R6).

**Independent Test**: Read documents with references in text and in attribute values, whole, split
at every byte and in parts, and compare tokens, values and the flag; read each malformed reference
and check category, markup, line and position (quickstart §1).

### Test shells for User Story 1 ⚠️

New shells in `test/Utf8XmlReaderTests.ReadReferences.cs` unless another file is named; each is read
with `ReadWhole`, `ReadEverySplit` and `ReadInParts` (T004).

- [X] T005 [US1] Test shell, US1-1 / FR-001 / FR-002: `<a>Spruce &amp; pine</a>` gives Element `a`, one Value `Spruce &amp; pine` with `HasEscapedValue` true, ElementEnd `a`, in `test/Utf8XmlReaderTests.ReadReferences.cs`
- [X] T006 [US1] Test shell, US1-2 / FR-002: `<a>Spruce and pine</a>` gives the Value with `HasEscapedValue` false; and in `<a><b>&amp;</b><c>x</c></a>` the flag is true only for `b`'s Value and false for every Element, ElementEnd and `c`'s Value (the flag lives for one `Read()`), in `test/Utf8XmlReaderTests.ReadReferences.cs`
- [X] T007 [US1] Test shell, US1-3 / FR-001: `<a b="&lt;&#60;&#x3C;"/>` gives Attribute `b` and Value `&lt;&#60;&#x3C;` with `HasEscapedValue` true, and `<a b='x &quot; y'/>` gives `x &quot; y` flagged, in `test/Utf8XmlReaderTests.ReadReferences.cs`
- [X] T008 [US1] Test shell, US1-4: a theory over `&amp;`, `&lt;`, `&gt;`, `&quot;`, `&apos;`, `&#65;`, `&#x41;`, `&#x1F332;`, each as the whole text of an element and as the whole of an attribute value: one Value equal to the reference, flagged, in `test/Utf8XmlReaderTests.ReadReferences.cs`
- [X] T009 [US1] Test shell, US1-5 / FR-004 / C1: `<a>x &am` (a segment ending inside the reference) read with `isReadingCompleted: false` halts at the Value: `Read()` returns false and `Position`, `TokenType`, `Depth` and the `ReaderState` line tracking are as before the call (nothing committed); a new reader over both segments `<a>x &am` + `p; y</a>` gives the whole document's tokens (Value `x &amp; y`, flagged). Also every split position inside `<a b="&#x1F332;"/>`, in `test/Utf8XmlReaderTests.ReadReferences.cs`
- [X] T010 [US1] Test shell, US1-6 / FR-005 / S0, reader level: representative rows of contracts/exceptions.md "Story 1", each in content (`<a>…</a>`) and in an attribute value (`<a b="…"/>`), asserting category, markup, line and position (the mapping from the constraint's offset to the document only the reader shows): bare `&` (`x & y`: Expected + Reference at the space); `&nbsp;` without a document type (Name + Reference at `n`); `&#x110000;` (Expected + Reference at the last `0`); `&#0;` (at `;`); and one of them with the reference on line 2 after a line feed, so the line and position walk across lines. Each throws with nothing committed, and the same exception with `ReadThrownEverySplit` and `ReadThrownInParts` (T004). The full table is T011, in `test/Utf8XmlReaderTests.ReadReferences.cs`
- [X] T011 [P] [US1] Test shells, `ReferenceConstraint` as a unit (like `ElementStack` in `test/ElementNameStackTests.cs`), in a new `test/ReferenceConstraintTests.cs`: feed `Evaluate` from the `&` directly, whole and chunk by chunk at every split, and assert `MarkupState`, the offending offset, `MarkupReason` and `HasReferences`: every contracts/exceptions.md "Story 1" row (bare `&`; missing `;` before `<`, a quote or spacing; `&;`, `&#;`, `&#x;`; `&#6A;`, `&#xG;`; `&#X41;`; `&nbsp;`, `&ampx;`, `&am;`, `&a;` without a document type as UndeclaredEntity at the first byte no entity accepts; `&#x110000;` at the last `0` and `&#99999999999999999999;` at the 7th `9`, never wrapping; `&#0;`, `&#xD800;`, `&#xDFFF;`, `&#xFFFE;`, `&#xFFFF;` at `;`); the five predefined names and `&#65;`, `&#x41;`, `&#x1F332;` well-formed with `HasReferences`; leading zeros (`&#0065;`, `&#x0041;`, `&#x0000000041;`); the highest legal values (`&#x10FFFF;`, `&#1114111;`) and the boundaries `&#x9;`, `&#x20;`, `&#xD7FF;`, `&#xE000;`, `&#xFFFD;`, `&#x10000;`; `_entity` choosing on the first byte (`l`, `g`, `q`) and on the second after `a` (`m`, `p`); with `IsDocumentType`, `&e;`, `&ampx;` and `&am;` are NotSupportedEntity with `ReferenceStart` 0, and `&amp;` is still well-formed; Unknown at the end of a chunk inside a reference
- [X] T012 [US1] Test shell, US1-7 / FR-006 / S2: `<!DOCTYPE a [<!ENTITY e "x">]><a>&e;</a>` throws `"NotSupported"` + EntityReference at the `&`; the same with `&e` followed by `<` throws Expected + Reference at `<` (a missing `;` is malformed with or without a document type); the entity is never applied (no token carries `x`); US1-8 (option A, developer 2026-11-05): `<!DOCTYPE a [<!ENTITY e "x">]><a/>` throws `"NotSupported"` + DocumentType at the `[` (line 1, position 13) with nothing committed, and `<!DOCTYPE a SYSTEM "a[1].dtd"><a/>` reads (a `[` in a quoted literal), in `test/Utf8XmlReaderTests.ReadReferences.cs`
- [X] T013 [US1] Test shell, spec edge cases: `<a> &amp; </a>` gives the Value ` &amp; ` and `<a>\n  &amp;<b/></a>` gives `\n  &amp;` (spacing next to a reference is text, READER.md rule 18); `&#x0000000041;` is read and flagged (one row; leading zeros are covered on the constraint, T011); in an attribute value `<` still throws and `>` and the other quote are plain bytes (`<a b="&lt;>'"/>` gives `&lt;>'`); `<a>&amp` with reading completed throws Incomplete + CharacterData at the end of data; `<a b="&amp` throws Incomplete + AttributeValue at the end of data, in `test/Utf8XmlReaderTests.ReadReferences.cs`
- [X] T014 [P] [US1] Update shells that encode the old `&` behaviour (FR-007): `ReadCharacterData_ForS1AmpersandFirst…`, `…ForS2TextBeforeAmpersand…`, `…ForM3AmpersandStraddling…` and the `EvaluateCharacterData` E2 row now expect one Value with `HasEscapedValue` in `test/Utf8XmlReaderTests.ReadCharacterData.cs`; `ReadAttributeValue_ForReference_ItShould_ThrowUnsupportedCommittingNothing` and the M5 `&` row now expect the literal read and flagged in `test/Utf8XmlReaderTests.ReadAttributeValue.cs`; the `&amp;` rows in `test/Utf8XmlReaderTests.ReadValueDelegation.cs:278` and `test/Utf8XmlReaderTests.SkipSpacing.cs:238` keep candidate CharacterData but no longer expect a throw. Direct calls of `EvaluateCharacterDataNonTerminalMarkup` / `EvaluateLiteralNonTerminalMarkup` with `out bool unsupportedCharacter` move to the markup structs' `Evaluate` with an `out` reason (T019)
- [X] T015 [P] [US1] Update shells for the moved name throw (R6): `Throwing_ForUnsupportedNameCharacter…` (both) in `test/Utf8XmlReaderTests.ExceptionPosition.cs`, `ReadName_ForS5Unsupported…` (both) in `test/Utf8XmlReaderTests.ReadName.cs`, `ReadEndTag_ForS8UnsupportedNameByte…` in `test/Utf8XmlReaderTests.ReadEndTag.cs` and the S1 unsupported cases in `test/Utf8XmlReaderTests.ReadStartTag.cs` and `test/Utf8XmlReaderTests.ReadAttribute.cs` also assert `Category == "NotSupported"` and `Markup == "Name"`, same position. `EvaluateNameNonTerminalMarkup`'s direct callers in `ReadName.cs` move to the `out` reason (T019)
- [X] T016 [P] [US1] Test shell, SC-003 / C2: in `test/ReadAllocationTests.cs`, the same generated document with `&amp;` in each `Species` text and `Key="&#x31;"` in each attribute allocates zero bytes over the read loop, as one span and as 4 KB segments (`Category=Performance`)
- [X] T017 [US1] **Test review gate**: the developer reviews T002–T016 (`test/Utf8XmlReaderTests.Documents.cs`, `test/ReadAllocationTests.cs`, `test/Utf8XmlReaderTests.ReadReferences.cs`, `test/ReferenceConstraintTests.cs` and the updated files) and closes the review task before T018 starts (C4)

### Implementation for User Story 1

- [X] T018 [US1] Declare the surface (depends on T017): `public bool HasEscapedValue { get; private set; }` in the value region of `src/Reading/Utf8XmlReader.cs` with the XML doc of contracts/public-api.md (behaviour follows `Utf8JsonReader.ValueIsEscaped`); `Read()` step 0 clears it with `Value` and `ValueSequence`; `Throwing.Category.NotSupported` and `Throwing.Markup.Reference`, `Throwing.Markup.EntityReference` in `src/Reading/Throwing.cs`, with `GetMessage` using a new `Formatting.NotSupported` template `"Not supported: {0}{1}"` (placeholder, #49) in `src/Formatting.Resource.cs`; a new `#region Entities` in `src/EBNF.cs`, in the `ReadOnlySpan<byte>` terminal style, with `AmpersandEntityName => "amp"u8`, `LessThanEntityName => "lt"u8`, `GreaterThanEntityName => "gt"u8`, `QuoteEntityName => "quot"u8` and `ApostropheEntityName => "apos"u8`; the XML doc of `GetString()` (and `TryGetString()`) in `src/Reading/Utf8XmlReader.Get.cs` says it returns the raw bytes, references unresolved, and that `HasEscapedValue` tells the caller (unescaping stays out of scope, a bridge follow-up). The test project builds; the new shells fail
- [X] T019 [US1] Replace `IMarkup.Evaluate`'s `out bool unsupportedCharacter` with `out MarkupReason reason` (`None`, `UnsupportedCharacter`, `MalformedReference`, `UndeclaredEntity`, `NotSupportedEntity`) in `src/Reading/Utf8XmlReader.cs`; `NameMarkup`, `ReadName`, `ReadEndTag` and `ProcessingInstructionTarget`'s callers (renamed to `ProcessingInstructionTargetConstraint` in T063) adapt with no behaviour change; make the span paths call the same markup structs as `EvaluateMultipleMarkup<T>`; `CharacterDataMarkup` (and `LiteralMarkup`, `NameMarkup`) become `internal` instead of `private`, since T014's evaluation shell builds `CharacterDataMarkup` directly (developer, 2026-11-05). Then run `dotnet test Forestry.Xml.slnx`: everything is green except the new US1 shells, so a regression from this refactor shows up here and not at T025 (depends on T018). Done 2026-11-05: every failure was a US1 shell except `Throwing_ForEveryCategory_ItShould_HaveAMalformedMarkupMessage`, which assumed every category is malformed; split so NotSupported (and Limit) get a "Not supported:" template test (R6)
- [X] T020 [US1] Add `ReferenceConstraint` (an `internal` struct nested in `Utf8XmlReader`, markup region of `src/Reading/Utf8XmlReader.cs`, reached by the tests as `Utf8XmlReader.ReferenceConstraint` like `Utf8XmlReader.MarkupState`; `MarkupReason` internal too), evaluating one reference from its `&` with `MarkupState Evaluate(ReadOnlySpan<byte> characters, out int length, out MarkupReason reason)`, chunk by chunk with its state in the struct (Unknown at the end of a chunk), which `CharacterDataMarkup` and `LiteralMarkup` call at each `&`; per data-model.md "Reference": states `None`, `Ampersand`, `Hash`, `Decimal`, `Hex`, `HexMarker`, `EntityName`; `_codePoint` "never above `0x10FFFF` (checked per digit)", so a digit that takes it above throws at that digit and never wraps; leading zeros add nothing; at `;` the code point must be a legal Char (`#x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF]`), else MalformedReference at `;`; empty number or name is MalformedReference at `;`; `_entity` is "which predefined name is being matched: `None` until it is chosen": the first byte after `&` chooses `LessThan`, `GreaterThan` or `Quote` for `l`, `g`, `q`; `a` waits for the second byte, which chooses `Ampersand` (`m`) or `Apostrophe` (`p`); after that each byte is compared with the chosen EBNF constant (T018) at `_entityLength`, and `;` is accepted only once the whole constant has matched; without a document type the first byte no entity accepts (`;` included) is UndeclaredEntity at that byte; with a document type (`IsDocumentType`), a name no entity accepts is `_entity = Other` and scanned as a `Name` to `;`, and any name not fully matched (`Other`, or a partial match like `&am;`) is NotSupportedEntity reported at the reference's `&` (`ReferenceStart`); the constraint only checks references, it never unescapes; `HasReferences` is set when a reference completes; positions per research.md R2 (depends on T019)
- [X] T021 [US1] `CharacterDataMarkup` holds a `ReferenceConstraint` initialised with the reader's `_documentType`: `&` no longer ends character data, `<` ends it only outside a reference, and `<` inside a reference is MalformedReference at `<`, in `src/Reading/Utf8XmlReader.cs` (depends on T020)
- [X] T022 [US1] `LiteralMarkup` holds a `ReferenceConstraint` the same way: `&` is no longer malformed, the matching quote ends the literal only outside a reference, and a quote inside a reference is MalformedReference at the quote; `<` stays malformed, in `src/Reading/Utf8XmlReader.cs` (depends on T020)
- [X] T023 [US1] `ReadContentCharacterData` and `ReadAttributeValue` in `src/Reading/Utf8XmlReader.cs`: remove the `&` "unsupported" throws (FR-007, the S1 and S7 TODOs); throw per contracts/exceptions.md "Story 1" (MalformedReference → Expected + Reference; UndeclaredEntity → Name + Reference; NotSupportedEntity → NotSupported + EntityReference at `ReferenceStart`), each before anything is committed (rule 26); set `HasEscapedValue` from `HasReferences` on commit; and `ReadDocumentType` throws NotSupported + DocumentType at the first `[` outside the delimited value's quoted literals, before committing (FR-006, US1-8) (depends on T021, T022)
- [X] T024 [P] [US1] Move `Throwing.ThrowUnsupportedNameCharacter` onto `GetXmlException` with `Category.NotSupported` + `Markup.Name`, same position, in `src/Reading/Throwing.cs` (R6) (depends on T018)
- [X] T025 [US1] Run `dotnet test Forestry.Xml.slnx`: T005–T016 pass, every other test still green (depends on T023, T024). Done 2026-11-05: 984/984 on net9 and net10 after #30's one-line fix (a start tag's `>` now adds one to the line position in `ContentReady`, kept by the developer); without it 8 position shells were one column short. `Forestry.Deserialize.Xml`'s tests still pass (16, 14 skipped for its own US3)
- [X] T026 [P] [US1] Documentation (D0, G0): in `READER.md` rewrite rule 18's "Character data ends at `<` or `&`" paragraph (ends at `<`; references are part of the Value and set `HasEscapedValue`), rule 21's literal paragraph (references allowed and flagged), and "Decided, not built yet" (the unsupported exception is designed as far as `Category.NotSupported`); in `CHANGELOG.md` add `HasEscapedValue` and references under Features Added, under Behavior Changes that `&` no longer throws: `GetString()` and the `Forestry.Deserialize.Xml` bridge above it now get raw `&amp;` with no error unless the caller checks `HasEscapedValue` (unescaping is a bridge follow-up), and the moved name throw's category under Breaking Changes (depends on T025)
- [X] T027 [US1] **Performance gate** (end of US1): `dotnet test Forestry.Xml.slnx -s test/Performance.runsettings` passes: T002 and T016 allocate nothing (SC-003, SC-005) (depends on T025)

**Checkpoint**: References read; US1 is complete and the MVP.

---

## Phase 3: User Story 2 - Compare names in full, within a limit (Priority: P2)

**Goal**: End tags are compared with the whole start-tag name, the element stack keeps whole names
(R4), and `ReaderOptions.MaxNameLength` (default 128) bounds every name where it is first read
(FR-008 to FR-011, R4, R5).

**Independent Test**: Read documents whose names are 32, 33, 35, 128 and 129 bytes long, with
matching and mismatched end tags, with the default and an overridden limit, whole, split at every
byte and in parts (quickstart §2).

### Test shells for User Story 2 ⚠️

New shells in `test/Utf8XmlReaderTests.ReadNameLength.cs` unless another file is named; each read
whole and cut up with `ReadWhole`, `ReadEverySplit` and `ReadInParts` (T004).

- [X] T028 [US2] Test shell, US2-1 / SC-004 name: `<ButtEndProfileExtrapolationFunction>1</ButtEndProfileExtrapolationFunction>` gives Element, Value, ElementEnd with the full 35-byte name, in `test/Utf8XmlReaderTests.ReadNameLength.cs`
- [X] T029 [US2] Test shell, US2-2 / FR-008: `<ButtEndProfileExtrapolationFunctionX></ButtEndProfileExtrapolationFunctionY>` throws Name + EndTag at the end tag's name start (line 1, the byte after `</`), in `test/Utf8XmlReaderTests.ReadNameLength.cs`
- [X] T030 [US2] Test shell, US2-3 / FR-008: `<abc></ab>` and `<ab></abc>` throw Name + EndTag at the end tag's name start, in `test/Utf8XmlReaderTests.ReadNameLength.cs`
- [X] T031 [US2] Test shell, US2-4 / FR-010 / S1: element names and attribute names of 32, 33 and 128 bytes are read; 129 bytes throws `"Limit"` + Name at the byte at index 128 of the name; with `isReadingCompleted: false` and the data ending one byte after that byte, it still throws rather than halting (no more of the name is buffered), in `test/Utf8XmlReaderTests.ReadNameLength.cs`
- [X] T032 [US2] Test shell, US2-5 / FR-009: `default(ReaderOptions)` reads a 128-byte element name; with `MaxNameLength = 16` a 17-byte element name throws `"Limit"` + Name; setting `MaxNameLength` to 0, -1 or `ElementStack.NameBytesCapacity + 1` (2049) throws `InvalidOperationException`; the option survives `ReaderState` into a new reader (in parts), in `test/Utf8XmlReaderTests.ReadNameLength.cs`
- [X] T033 [P] [US2] Test shell, FR-009 / MaxDepth (developer, 2026-11-03): setting `MaxDepth` to 0, -1 or 65 (`ElementStack.NonAllocatingMaxDepth + 1`) throws `InvalidOperationException`; 64 is accepted; `default(ReaderOptions)` still reads a document nested 3 levels deep (the constructors default an unset 0 to 64); `MaxDepth = 2` with three nested elements (`<a><b><c/></b></a>`) throws `"Limit"` + Element at the third start tag with nothing committed, and two nested read (S1, enforced), in a new `test/ReaderOptionsTests.cs`
- [X] T034 [US2] Test shell, US2-6 / FR-011: an end tag of 129 bytes closing a 3-byte start tag throws Name + EndTag, never `"Limit"`; `<abc></abcd` with `isReadingCompleted: false` throws the mismatch rather than halting (the end tag is evaluated only to the start-tag name's length plus one byte), in `test/Utf8XmlReaderTests.ReadNameLength.cs`
- [X] T035 [US2] Test shell, US2-7 / C1: a document with 35- and 128-byte element and attribute names read split at every byte and in parts gives the whole document's tokens, in `test/Utf8XmlReaderTests.ReadNameLength.cs`
- [X] T036 [US2] Test shell, FR-010 other names: a processing instruction target of `MaxNameLength + 1` bytes throws `"Limit"` + ProcessingInstructionTarget at index `MaxNameLength`; after a document type, an entity name of `MaxNameLength + 1` bytes in a reference throws `"Limit"` + EntityReference at the first byte beyond (both with `MaxNameLength = 16` to keep the documents short), in `test/Utf8XmlReaderTests.ReadNameLength.cs`
- [X] T037 [P] [US2] Element stack shells (R4) in `test/ElementNameStackTests.cs`: replace `TryPop_ForANameLongerThanThePackedLength_ItShould_MatchOnTheCappedPrefix` with a full-comparison mismatch; the worked layout (push `ab`, `Log`, `LogDiameter`: depth 3, `TopLength` 11, `TryPeek("LogDiameter")` true, `TryPeek("LogDiameteX")` and `TryPeek("Log")` false); `Push(ReadOnlySequence<byte>)` of a name straddling segments then `TryPeek` of it as a sequence; `Pop()` drops the top name so `TopLength` is 3; a push that would pass 2048 bytes reports it does not fit and leaves the stack unchanged. Update the `Pop(buffer)` tests and the `stack.Pop(stackalloc …)` call in `test/Utf8XmlReaderTests.cs:78` to `Pop()`
- [X] T038 [P] [US2] Test shell, R4 overflow: open start tags whose names total more than 2048 bytes (e.g. 20 levels of 120-byte names) throw `"NotSupported"` + ElementStack at the start tag that does not fit; and, with the default `MaxDepth` of 64, 65 nested short names (`<a><a>…`) throw `"Limit"` + Element at the 65th start tag (today the `PushAllocating` `NotImplementedException`; NotSupported + ElementStack remains only for the 2 KB pool), each with nothing committed (`Depth` and `Position` as before the failing start tag), in `test/Utf8XmlReaderTests.ReadNameLength.cs`
- [X] T039 [US2] **Test review gate**: the developer reviews T028–T038 (`test/Utf8XmlReaderTests.ReadNameLength.cs`, `test/ReaderOptionsTests.cs`, `test/ElementNameStackTests.cs`, `test/Utf8XmlReaderTests.cs`) and closes the review task before T040 starts (C4)

### Implementation for User Story 2

- [X] T040 [US2] Declare the surface (depends on T039): `ReaderOptions.MaxNameLength` and `internal const int DefaultMaxNameLength = 128` in `src/Reading/ReaderOptions.cs`, setter throwing `InvalidOperationException` via `Throwing.WhenValueNotPositive` for `value <= 0` (an explicit 0 is rejected) and for a value above `ElementStack.NameBytesCapacity` (declared here as `internal const int NameBytesCapacity = NonAllocatingMaxDepth * 32`, 2048, used by T041), with the XML doc of contracts/public-api.md listing exactly the names it bounds (element names in start tags, attribute names, processing instruction targets, entity names in references) (D0); the `MaxDepth` setter changes from `value < 0` to `value <= 0` and also rejects a value above `ElementStack.NonAllocatingMaxDepth` (64) with the same `InvalidOperationException`, in the same file; `MaxDepth` is enforced (S1): in `ReadStartTag` in `src/Reading/Utf8XmlReader.cs`, when `Depth == MaxDepth`, throw Limit + Element at the start tag, before committing and before the push (T043); both reader constructors are unchanged in meaning: a field still 0 was never set, so the default applies (64 / 128), and they now also default `MaxNameLength`, in `src/Reading/Utf8XmlReader.Single.cs` and `src/Reading/Utf8XmlReader.Multiple.cs`, so `default(ReaderOptions)` keeps working; `Throwing.Category.Limit` with a `Formatting.Limit` template `"Limit exceeded: {0}{1}"` (placeholder, #49) and `Throwing.Markup.ElementStack` in `src/Reading/Throwing.cs` and `src/Formatting.Resource.cs`; `MarkupReason.TooLong`. Done 2026-11-05: a value above the cap throws through a new `Throwing.WhenValueAbove` ("Value {0} named {1} is above {2}"), since `WhenValueNotPositive`'s message only fits `<= 0`
- [X] T041 [US2] Rewrite `src/Reading/ElementStack.cs` per research.md R4 and data-model.md "ElementStack": `_names` `[InlineArray(2048)] byte` (`NameBytesCapacity = NonAllocatingMaxDepth × 32`), `_ends` `[InlineArray(64)] ushort`, `_depth` capped at 64; the top name is `_names[_ends[_depth - 2] .. _ends[_depth - 1]]` (from 0 when `_depth == 1`); `TryPush(ReadOnlySpan<byte>)` and `TryPush(ReadOnlySequence<byte>)` (`CopyTo` straight in) copy the whole name and record its end, returning false and changing nothing when it does not fit (the bytes would pass 2048, or `_depth` is already 64); `PushAllocating` and `PeekAllocating` are removed; `TopLength`; `TryPeek(ReadOnlySpan<byte>)` and `TryPeek(ReadOnlySequence<byte>)` compare lengths first, then `SequenceEqual` or `SequenceReader<byte>.IsNext`; `TryPop` keeps its contract; `Pop()` is `_depth--` behind a guard for an empty stack (keep today's `InvalidOperationException` for `_depth == 0`, so a caller bug can never make `_depth` negative and corrupt the top-name slice); `TryPeek` returns false on differing lengths before comparing any byte, so an end tag never compares or buffers past the start-tag name (T034 tests the observable part); remove `PackedNameLength` and the stack's use of `Extensions.Pack` / `Unpack`. Invariants: `_ends[0 .. _depth]` strictly increasing and at most 2048; `_depth <= 64` (depends on T040)
- [X] T042 [US2] Name budget: `NameMarkup.MaxLength` (init) and a remaining budget in `EvaluateNameNonTerminalMarkup`, so a name byte at index `MaxLength` returns Malformed with `MarkupReason.TooLong` without looking further; `ReadName` (start tag and attribute names) uses `MaxNameLength` and throws Limit + Name at that byte, in `src/Reading/Utf8XmlReader.cs` (depends on T040)
- [X] T043 [US2] `ReadStartTag` pushes `Value` or `ValueSequence` with `TryPush` (no `stackalloc`, removing `Utf8XmlReader.cs:1042`) after T040's `MaxDepth` check, and throws NotSupported + ElementStack at the name start when the names do not fit the 2 KB pool, before committing; `TryReadEmptyElementEndingTerminal` calls `Pop()` (removing `:859`), in `src/Reading/Utf8XmlReader.cs` (depends on T041, T042). Done 2026-11-05: `ReadName` split into `EvaluateName` (nothing committed) and the commit, so `ReadStartTag` checks `MaxDepth`, then the name, then `ElementStack.CanPush` before committing anything (T038's nothing-committed)
- [X] T044 [US2] `ReadEndTag` evaluates the name with `TopLength` as its budget: a name byte at that index throws Name + EndTag at the name start (FR-011), and the name is compared in full with `TryPeek` over the span or the sequence slice (no `stackalloc`, removing `:1111`), in `src/Reading/Utf8XmlReader.cs` (depends on T041, T042)
- [X] T045 [US2] Bound the other names (FR-010): `ProcessingInstructionTarget` (renamed to `ProcessingInstructionTargetConstraint` in T063) gets `MaxLength` and a target byte at that index is Limit + ProcessingInstructionTarget, checked once the instruction is delimited (after `?>` is found, FR-010), in `src/Reading/Utf8XmlReader.cs`, `src/Reading/Utf8XmlReader.Single.cs` and `src/Reading/Utf8XmlReader.Multiple.cs`; `ReferenceConstraint`'s document-type entity name is bounded by `MaxNameLength` and throws Limit + EntityReference at the first byte beyond (depends on T042)
- [X] T046 [P] [US2] Update the doc comments of `Extensions.Pack` and `Unpack` in `src/Reading/Extensions.cs`: no longer used by the element stack and no "#23 tradeoff" (methods unchanged, public as before) (depends on T041)
- [X] T047 [US2] Run `dotnet test Forestry.Xml.slnx`: T028–T038 pass, US1 and every other test still green (depends on T043–T045). Done 2026-11-05: 1013/1013 on net9 and net10, 0 warnings; `Forestry.Deserialize.Xml` still 16 passed, 14 skipped. Two existing `ReadEndTag` rows encoded the old behaviour and were removed: S5's 32-byte leniency (now T029's mismatch) and `</Stem` halting with `a` open (now T034's mismatch at once)
- [X] T048 [P] [US2] Documentation (D0, G0): in `READER.md` replace rule 16 (names compared on 32 bytes) with full comparison, the stack's layout and `MaxNameLength`; in `CHANGELOG.md` add `MaxNameLength` under Features Added and full name comparison, `Category.Limit`, `MaxDepth` now enforced (a start tag at `Depth == MaxDepth` throws Limit + Element) and an explicit `MaxDepth = 0` now throwing (it used to mean 64) and `MaxDepth` above 64 or `MaxNameLength` above 2048 throwing when set under Breaking Changes (a document accepted on 32 bytes now throws) (depends on T047)
- [X] T049 [US2] **Performance gate** (end of US2): `dotnet test Forestry.Xml.slnx -s test/Performance.runsettings` passes: T002 and T016 still allocate nothing with the new stack (SC-005) (depends on T047)

**Checkpoint**: Names compared in full; US1 and US2 both work.

---

## Phase 4: User Story 3 - Skip a UTF-8 BOM, refuse other encodings (Priority: P3)

**Goal**: A UTF-8 BOM at the start is skipped without line tracking, other BOMs throw "not
supported" at line 1, position 1, and a declaration naming an encoding other than UTF-8 throws "not
supported" at the name (FR-012 to FR-014, R7, R8, #67).

**Independent Test**: Read the same document with and without a UTF-8 BOM, with each other BOM and
each encoding name, whole, split at every byte and in parts (quickstart §3).

### Test shells for User Story 3 ⚠️

New shells in `test/Utf8XmlReaderTests.ByteOrderMark.cs` and
`test/Utf8XmlReaderTests.DeclarationEncoding.cs`; each read whole and cut up with
`ReadWhole`, `ReadEverySplit` and `ReadInParts` (T004).

- [ ] T050 [P] [US3] Test shell, US3-1 / FR-012: `EF BB BF` + `<?xml version="1.0"?><a/>` gives the same tokens as without the BOM and the Declaration is accepted as first, in `test/Utf8XmlReaderTests.ByteOrderMark.cs`
- [ ] T051 [P] [US3] Test shell, US3-2 / FR-012: `EF BB BF` + `<a>&</a>` throws with the same category, markup, line and position as `<a>&</a>` (the BOM is not counted), in `test/Utf8XmlReaderTests.ByteOrderMark.cs`
- [ ] T052 [P] [US3] Test shell, US3-3 / FR-012 / C1 / R7: `EF` + `BB BF<a/>` and `EF BB` + `BF<a/>` halt with nothing committed until the BOM is complete, then give the same tokens as `<a/>`; `EF BB BF` + `<a/>` (a halt after the BOM) gives the same tokens; `EF BB BF EF BB BF<a/>`, read whole and with a halt after the first BOM, skips only the first: the second `EF` throws today's generic "starts no markup" (`ThrowUnexpectedMarkup`) at line 1, position 1, in `test/Utf8XmlReaderTests.ByteOrderMark.cs`
- [ ] T053 [P] [US3] Test shell, US3-4 / FR-013: `FE FF`, `FF FE`, `00 00 FE FF`, `FF FE 00 00`, each followed by `<a/>` in that encoding's bytes, throw `"NotSupported"` + ByteOrderMark at line 1, position 1; each split inside a BOM (`FE|FF`, `FF|FE`, `00|00 FE FF`, `00 00|FE FF`, `00 00 FE|FF`, each followed by `<a/>` in that encoding) gives the same exception with `ReadThrown`, `ReadThrownEverySplit` and `ReadThrownInParts` (T004), the first segment halting with nothing committed (C1); with reading completed, `FE`, `FF`, `00`, `00 00` and `00 00 FE` alone throw today's generic "starts no markup" at line 1, position 1, in `test/Utf8XmlReaderTests.ByteOrderMark.cs`
- [ ] T054 [P] [US3] Test shell, spec edge cases: `EF BB BF` alone, and `EF BB` with reading completed, throw Incomplete + RootElement at the end of data; `<a>` + `EF BB BF` + `</a>` gives a Value of those three bytes (plain text ≥ 0x80); `<a/>` + `EF BB BF` throws as any byte after the root does today, in `test/Utf8XmlReaderTests.ByteOrderMark.cs`
- [ ] T055 [P] [US3] Test shell, US3-5 / FR-014 / R8, reader level: `<?xml version="1.0" encoding="UTF-8"?><a/>` and a declaration without an encoding are read, the Declaration's Value staying the raw declaration; `encoding="ISO-8859-1"` throws `"NotSupported"` + Encoding at `I` with its line and position and nothing committed; both split at every byte give the same token (`ReadEverySplit`, `ReadInParts`) or the same exception (`ReadThrownEverySplit`, `ReadThrownInParts`, T004). The encoding names and shapes are T056, in `test/Utf8XmlReaderTests.DeclarationEncoding.cs`
- [ ] T056 [P] [US3] Test shells, `DeclarationEncodingConstraint` as a unit, in a new `test/DeclarationEncodingConstraintTests.cs`: feed `Evaluate` the declaration's bytes directly, whole and chunk by chunk at every split, and assert `MarkupState`, `OffendingIndex`, `Category` and `Markup`: `UTF-8` in any case (`UTF-8`, `utf-8`, `Utf-8`), single and double quotes, spacing around `=`, and no encoding are well-formed; `ISO-8859-1`, `UTF-16`, `UTF8`, an empty name, `UTF\r8` and `UTF-\x188` (bytes that `| 0x20` would fold into `-` and `8`), `UTF-8X` and `UTF-` are NotSupported + Encoding at the name's first byte (`OffendingIndex`), each fed whole and split at every byte; `encoding` without the `S? '=' S?` quote shape, and `xencoding="x"` (no spacing before it), throw nothing
- [ ] T057 [P] [US3] Test shells, `ProcessingInstructionTargetConstraint` and `NoConstraint` as units (with the T063 refactor), in a new `test/ProcessingInstructionTargetConstraintTests.cs`: feed `Evaluate` the bytes after `<?` directly, whole and chunk by chunk at every split: a target followed by spacing or `?>` is well-formed; an empty target, a first byte that cannot start a name, `?` not followed by `>`, and `xml` in any case (`OffendingIndex` 0) are Malformed with Name + ProcessingInstructionTarget at `OffendingIndex`; a target of `MaxLength + 1` bytes is Limit + ProcessingInstructionTarget at index `MaxLength` (T045); `NoConstraint` is WellFormed for any bytes; `DocumentTypeConstraint` is NotSupported + DocumentType at a `[` outside quoted literals (`<!DOCTYPE a [` at offset 12) and WellFormed for `<!DOCTYPE a SYSTEM "a[1].dtd">`. The reader-level PI tests in `test/Utf8XmlReaderTests.ReadOpaqueValue.cs` stay as they are (line and position, nothing committed)
- [ ] T058 [US3] Add `isByteOrderMarkChecked` to the test helpers that build a `ReaderState` by named arguments (`StartTagState` in `test/Utf8XmlReaderTests.ReadAttribute.cs`, `AfterEqualsState` in `test/Utf8XmlReaderTests.ReadAttributeGuard.cs`, `NestedStartTagState` in `test/Utf8XmlReaderTests.ReadEmptyElementEnd.cs`, `EndTagState` in `test/Utf8XmlReaderTests.ReadEndTag.cs`, `OpaqueState` in `test/Utf8XmlReaderTests.ReadOpaqueValue.cs`, `ContextState` in `test/Utf8XmlReaderTests.ReadValueDelegation.cs`), set to `true` for every state past the start of a document (READER.md rule 22)
- [ ] T059 [US3] **Test review gate**: the developer reviews T050–T058 (`test/Utf8XmlReaderTests.ByteOrderMark.cs`, `test/Utf8XmlReaderTests.DeclarationEncoding.cs`, `test/DeclarationEncodingConstraintTests.cs`, `test/ProcessingInstructionTargetConstraintTests.cs`, the updated helpers) and closes the review task before T060 starts (C4)

### Implementation for User Story 3

- [ ] T060 [US3] `Throwing.Markup.ByteOrderMark` and `Throwing.Markup.Encoding` in `src/Reading/Throwing.cs` (depends on T059)
- [ ] T061 [US3] `_isByteOrderMarkChecked` in READER.md rule 22's six places: the `ReaderState` field, its public constructor (`false`) and its internal constructor (parameter `isByteOrderMarkChecked`) in `src/Reading/ReaderState.cs`; the reader's field and its `ReaderState` property in `src/Reading/Utf8XmlReader.cs`; both reader constructors in `src/Reading/Utf8XmlReader.Single.cs` and `src/Reading/Utf8XmlReader.Multiple.cs` (depends on T059)
- [ ] T062 [US3] Skip BOM step in `Read()` after the first drained check and before skip delimiter, only while `_isByteOrderMarkChecked` is false (research.md R7): `EF BB BF` skips 3 bytes without line tracking and leaves the reader on a readable byte (`TrySkipEmptySegments` with multiple segments), then the drained check; every incomplete start of a listed BOM at the end of the data (`EF`, `EF BB`, `FE`, `FF`, `00`, `00 00`, `00 00 FE`) halts when reading is not completed (C1); when reading is completed, `EF` or `EF BB` throws Incomplete + RootElement, and `FE`, `FF`, `00`, `00 00`, `00 00 FE` skip nothing and throw what an unrecognised start throws today: the generic "starts no markup" (`ThrowUnexpectedMarkup`, candidate None) at line 1, position 1; `FE FF`, `FF FE`, `00 00 FE FF` throw NotSupported + ByteOrderMark at offset 0 (`FF FE` at once, not waiting for `FF FE 00 00`, which is NotSupported at offset 0 too); anything else skips nothing; the field is set once the step has decided. Peek with `PeekCharacter`, commit only when decided, in `src/Reading/Utf8XmlReader.cs` (depends on T060, T061)
- [ ] T063 [US3] Behaviour-preserving refactor (research.md R8): add an internal `IConstraint` in `src/Reading/Utf8XmlReader.cs` with `MarkupState Evaluate(ReadOnlySpan<byte> characters)` evaluated segment by segment over the opaque value (state in the struct), `int OffendingIndex`, `Throwing.Category Category` and `Throwing.Markup Markup`; add `NoConstraint` (always well-formed); rename `ProcessingInstructionTarget` to `ProcessingInstructionTargetConstraint` implementing `IConstraint`; `IConstraint`, `NoConstraint` and `ProcessingInstructionTargetConstraint` become `internal` (nested in `Utf8XmlReader`, no longer `private`), so the tests reach them like `ElementStack` (Name + ProcessingInstructionTarget, Limit from T045); make `ReadSingleOpaqueValue<TConstraint>` / `IgnoreSingleOpaqueValue<TConstraint>` in `src/Reading/Utf8XmlReader.Single.cs` and `ReadMultipleOpaqueValue<TConstraint>` / `IgnoreMultipleOpaqueValue<TConstraint>` in `src/Reading/Utf8XmlReader.Multiple.cs` generic with `where TConstraint : struct, IConstraint`, one check-then-throw before commit in each path, replacing `ThrowWhenMultipleProcessingInstructionMalformed` and the single path's PI check; remove `isProcessingInstruction` (never add `isDeclaration`); call sites in `src/Reading/Utf8XmlReader.cs`: `ReadProcessingInstruction` passes `ProcessingInstructionTargetConstraint`, `ReadDocumentType` passes a `DocumentTypeConstraint` (T023's internal-subset check moved into an `IConstraint`: NotSupported + DocumentType at a `[` outside quoted literals), `ReadDeclaration` and `ReadComment` pass `NoConstraint`. Ends with `dotnet test Forestry.Xml.slnx` green except the US3 reader shells and `DeclarationEncodingConstraintTests`; `ProcessingInstructionTargetConstraintTests` pass (depends on T059)
- [ ] T064 [US3] `DeclarationEncodingConstraint` (research.md R8) in `src/Reading/Utf8XmlReader.cs`, evaluated segment by segment like `ProcessingInstructionTargetConstraint`: the first `encoding` preceded by spacing, then `S? '=' S?`, a quote, the name to the matching quote, compared with `UTF-8` ignoring ASCII letter case on one path: each chunk is compared with `System.Text.Ascii.EqualsIgnoreCase` against the matching slice of `"UTF-8"u8`, starting from what is matched so far (no `| 0x20`, which folds more than letters: `0x0D | 0x20` is `-`, `0x18 | 0x20` is `8`); a name longer than 5 bytes, shorter at its closing quote, or different gives `OffendingIndex` at its first byte; any other shape throws nothing. It is the second `IConstraint` (T063), `internal` like the first, with `Category` NotSupported and `Markup` Encoding; `ReadDeclaration` passes it instead of `NoConstraint` (depends on T060, T063)
- [ ] T065 [US3] Run `dotnet test Forestry.Xml.slnx`: T050–T058 pass, US1, US2 and every other test still green (depends on T062, T064)
- [ ] T066 [P] [US3] Documentation (D0, G0): in `READER.md` add a Skip BOM row between pipeline steps 1 and 2, remove it from "Steps not built yet", remove "a BOM until #31" from candidate None's cases, add `_isByteOrderMarkChecked` to rule 22's list, and a glossary line: markup delimits, constraint checks ("evaluate" for both: a markup is evaluated to its end, a constraint over its value; `IMarkup` and `IConstraint`); in `CHANGELOG.md` add the BOM and encoding checks under Features Added, linking #67 (depends on T065)
- [ ] T067 [US3] **Performance gate** (end of US3): `dotnet test Forestry.Xml.slnx -s test/Performance.runsettings` passes (SC-005) (depends on T065)

**Checkpoint**: All three stories work independently.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [ ] T068 Test shell, SC-004: a hand-written StanForD-shaped document (UTF-8 BOM, `<?xml version="1.0" encoding="UTF-8"?>`, a `ButtEndProfileExtrapolationFunction` element, `&amp;` in a text field, written one element per line) read to the end with default options, whole and cut up with `ReadWhole`, `ReadEverySplit` and `ReadInParts` (T004), in `test/Utf8XmlReaderTests.StanForD.cs`; the developer reviews it (C4); it passes with no code change (depends on T067)
- [ ] T069 Run quickstart.md: `dotnet build Forestry.Xml.slnx` shows no CS1591 warnings (D0, checked by the build since T001 for every public member), and the default run and the performance run pass (depends on T068)
- [ ] T070 G0 check: where the code differs from `specs/001-references-names-bom/research.md`, `data-model.md` or `contracts/` (names, positions, members), update the spec-kit markdown to match and tell the developer what changed (depends on T069)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies; T001 first (the build must pass with docs checked), then T003, the performance baseline, runs before T004 makes the test project wait for T018.
- **US1 (Phase 2)**: after Setup. Shells T005–T016 → gate T017 → T018 → T019 → T020 → T021/T022
  → T023 (T024 after T018) → T025 → T026, T027.
- **US2 (Phase 3)**: after Setup; built after US1 (P1 → P2). T036's entity-name row and T045's
  constraint bound need US1's `ReferenceConstraint`; everything else in US2 is independent of US1.
- **US3 (Phase 4)**: after Setup; built after US2. Independent of US1 and US2 except T051, which
  compares a BOM document's exception with the same document's without a BOM, whatever that
  exception is.
- **Polish (Phase 5)**: after all three stories.

### Within Each Story

- Shells, then the review gate, then the surface declaration, then the rest of the code (C4).
- Every check before its commit (READER.md rule 26); one `Read()` either advances one token, halts
  with nothing committed, or throws (C3).
- The story's `dotnet test` run, then its documentation and its performance gate.

### Parallel Opportunities

- US1: T011, T014, T015, T016 alongside the reader shells (different files); T024 alongside T020–T023;
  T026 alongside T027.
- US2: T033, T037, T038 alongside the other shells (different files); T046 alongside T042–T045; T048 alongside T049.
- US3: T050–T057 in four files, the BOM ones, T055, T056 and T057 in parallel; T063 then T064 alongside T061–T062;
  T066 alongside T067.

---

## Parallel Example: User Story 3

```text
Task: "T050–T054 BOM shells in test/Utf8XmlReaderTests.ByteOrderMark.cs"
Task: "T055 encoding shell in test/Utf8XmlReaderTests.DeclarationEncoding.cs"
Task: "T056 constraint shells in test/DeclarationEncodingConstraintTests.cs"
Task: "T057 constraint shells in test/ProcessingInstructionTargetConstraintTests.cs"

# after the gate T059 and T060:
Task: "T061 + T062 ReaderState field and the Skip BOM step"
Task: "T063 IConstraint refactor, then T064 DeclarationEncodingConstraint"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Setup T001–T004: the XML-doc check, the performance baseline, then the helpers.
2. US1 T005–T027: references read and flagged, the first gate passed.
3. **Stop and validate**: quickstart §1; a document with `&amp;` can now be read at all.

### Incremental Delivery

1. Setup → baseline.
2. US1 → references (MVP).
3. US2 → names compared in full; StanForD's 35-byte name is safe.
4. US3 → BOM and encoding (#67).
5. Polish → SC-004 end to end, quickstart, G0.

Each story ends green with its documentation and performance gate, so the branch can stop after any
of them.

---

## Notes

- [P] tasks touch different files and depend on no incomplete task.
- Shells may fail (or, in US1 and US2, not compile) until their story's implementation; that is
  expected between the gate and the code.
- Commit after each task or logical group, `#64` prefix, only when the developer asks.
- Stop at any checkpoint to validate a story on its own.
