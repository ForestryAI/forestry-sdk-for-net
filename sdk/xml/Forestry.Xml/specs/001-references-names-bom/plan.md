# Implementation Plan: References, names compared in full, BOM

**Branch**: `xml-reader-spec-kit` | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-references-names-bom/spec.md`

**Issue**: #64 (feature); stories are sub-issues #65 (US1), #66 (US2) and #67 (US3).

## Summary

Three thin stories on `Utf8XmlReader`. **P1**: character and predefined entity references are
scanned inside the existing evaluate-then-commit loops of character data and literals, by a small
`ReferenceConstraint` carried in the markup struct across segments, so a halted cycle re-scans and
commits nothing (C1); the Value stays raw and a new `HasEscapedValue` flag says it holds references.
**P2**: the element stack keeps whole names end to end in its existing 2 KB, end tags compare in
full with the start-tag name's length as their budget, and the new `ReaderOptions.MaxNameLength`
(default 128) bounds every name where it is first read. **P3**: a Skip BOM step before the first
token skips `EF BB BF` untracked and refuses other BOMs; the declaration's encoding name is checked
before commit. "Not supported" and "limit" throws get categories of their own. Decisions and their
grounding in the source: [research.md](research.md).

## Technical Context

**Language/Version**: C# (latest with the SDK), targeting net9.0 and net10.0 (A0)

**Primary Dependencies**: none beyond the BCL (A1): `System.Buffers` (`ReadOnlySequence`,
`SequenceReader`), `System.Text.Ascii`, `InlineArray`

**Storage**: N/A (a forward-only reader)

**Testing**: xunit via `test/Forestry.Xml.Tests.csproj`; performance tests by trait
`Category=Performance`, run with `test/Performance.runsettings` (A4)

**Target Platform**: any .NET 9/10 runtime; a library package

**Project Type**: library (`Forestry.Xml`, namespaces `Forestry.Xml` and `Forestry.Xml.Reading`)

**Performance Goals**: no allocation per token or per `Read()` with references (C2, SC-003); the
reference scan stays inside the existing single pass over the bytes

**Constraints**: synchronous and resumable at every byte (C1); fail at the first byte where
malformed (S0); names bounded before buffering (S1); no DTD processing (S2); `ReaderState` copied
per segment, so the element stack stays 2 KB

**Scale/Scope**: StanForD documents of any size through pipes; names up to 128 bytes by default
(35 bytes in real data); nesting up to 64 levels (unchanged)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Rule | How this plan meets it | Pre | Post |
|---|---|---|---|
| C0 Grammar | each behaviour traces to [66] CharRef, [68] EntityRef, [10] AttValue, [43] content, WFC Legal Character, WFC Entity Declared, WFC Element Type Match, §4.3.3; what stays unsupported throws `NotSupported` (R6) | ✅ | ✅ |
| C1 Resumable | reference and name-length state live in the `IMarkup` struct for one evaluation, re-scanned after a halt; the BOM step halts when cut off; every scenario split at every byte (R1, R7, R9) | ✅ | ✅ |
| C2 No allocation | each constraint is a stack struct; the stack copies names into its inline pool, no `stackalloc` copy; an allocation test is added (R4, R9) | ✅ | ✅ |
| C3 Read contract | each new path is true / false (nothing committed) / throw; no getters or skip added | ✅ | ✅ |
| C4 Test-first | shells per assertion row, reviewed before code, one review task per story (R9) | ✅ | ✅ |
| C5 Ownership | every research decision (R1–R9) reviewed by the developer, 2026-11-03 | ✅ | ✅ |
| C6 On its own | no reference to `Forestry.Deserialize`; SC-004's document is hand-written in this package | ✅ | ✅ |
| D0 Docs | XML docs on `HasEscapedValue`, `MaxNameLength` (listing the names it bounds); READER.md updated (below) | ✅ | ✅ |
| S0 Fail fast | positions per throw in [contracts/exceptions.md](contracts/exceptions.md); checks before commit (rule 26) | ✅ | ✅ |
| S1 Limits | `MaxNameLength` checked at the byte beyond (a PI target once the PI is delimited; PI buffering is the later `MaxValueLength`), end tags budgeted by the start-tag name; `MaxDepth` enforced at the start tag; stack overflow throws, never truncates (R4, R5) | ✅ | ✅ |
| S2 No external resolution | the document type stays opaque; a non-predefined entity after one throws `NotSupported` | ✅ | ✅ |
| A0–A4 | net9/net10; no packages; shared build; A/A/A labels and the performance gate per story | ✅ | ✅ |
| G0 Spec-kit is truth | READER.md and the spec-kit markdown updated in the same story as the code | ✅ | ✅ |

No violations; Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/001-references-names-bom/
├── spec.md
├── checklists/requirements.md
├── plan.md              # this file
├── research.md          # Phase 0: decisions R1–R9
├── data-model.md        # Phase 1: constraints, stack, options, lifetimes
├── quickstart.md        # Phase 1: how to validate each story
├── contracts/
│   ├── public-api.md    # HasEscapedValue, MaxNameLength, behaviour table
│   └── exceptions.md    # category / markup / position per throw
└── tasks.md             # Phase 2 (/speckit-tasks, not created here)
```

### Source Code

```text
sdk/xml/Forestry.Xml/
├── READER.md                         # pipeline step Skip BOM; rules 16, 18, 21 rewritten; unsupported designed
├── CHANGELOG.md                      # Features Added / Breaking Changes for 1.0.0-beta.1
├── src/
│   ├── Forestry.Xml.csproj           # Setup: GenerateDocumentationFile, CS1591 a warning (D0)
│   ├── EBNF.cs                       # US1: #region Entities (amp, lt, gt, quot, apos); Setup: doc fixes
│   ├── Formatting.Resource.cs        # NotSupported and Limit templates (placeholder wording, #49)
│   └── Reading/
│       ├── ElementStack.cs           # US2: byte pool + end offsets, full comparison, Push(sequence), TopLength
│       ├── Extensions.cs             # US2: doc only (no longer used by the stack)
│       ├── ReaderOptions.cs          # US2: MaxNameLength, DefaultMaxNameLength
│       ├── ReaderState.cs            # US3: _byteOrderMark (rule 22)
│       ├── Throwing.cs               # US1–3: Category.NotSupported/Limit, new Markup members
│       ├── Utf8XmlReader.cs          # US1: ReferenceConstraint, markup structs, HasEscapedValue;
│       │                             # US2: name budgets, ReadStartTag/ReadEndTag/empty-element pop;
│       │                             # US3: Skip BOM step, DeclarationEncodingConstraint
│       ├── Utf8XmlReader.Single.cs   # US2/US3: defaults, state copy, declaration check
│       └── Utf8XmlReader.Multiple.cs # US2/US3: defaults, state copy, declaration check
└── test/
    ├── Utf8XmlReaderTests.ReadReferences.cs      # US1 (new)
    ├── Utf8XmlReaderTests.ReadNameLength.cs      # US2 (new)
    ├── Utf8XmlReaderTests.ByteOrderMark.cs       # US3 (new)
    ├── Utf8XmlReaderTests.DeclarationEncoding.cs # US3 (new)
    ├── ReferenceConstraintTests.cs               # US1 (new), constraint as a unit
    ├── DeclarationEncodingConstraintTests.cs     # US3 (new), constraint as a unit
    ├── ProcessingInstructionTargetConstraintTests.cs # US3 (new), with the IConstraint refactor
    ├── NoConstraintTests.cs                      # US3 (new), with the IConstraint refactor
    ├── DocumentTypeConstraintTests.cs            # US3 (new), US1's internal-subset check as a constraint
    ├── ReaderOptionsTests.cs                     # US2 (new), MaxDepth setter and enforcement
    ├── Utf8XmlReaderTests.StanForD.cs            # Polish (new), SC-004 end to end
    ├── ReadAllocationTests.cs                    # SC-003, Category=Performance (new, first in this package)
    ├── Utf8XmlReaderTests.Documents.cs           # shared whole / every-split / in-parts helpers (new)
    └── existing ReadCharacterData, ReadAttributeValue, ReadEndTag, ReadStartTag,
        ElementNameStackTests, Utf8XmlReaderTests: updated where they encode the old behaviour
```

**Structure Decision**: One library and its test project, as they are; no new projects. Story order
P1 → P2 → P3 follows the spec's priority, but the stories touch mostly separate code (markup
structs; element stack and names; the BOM step and declaration), so they can be built in any order.
`Throwing`'s new categories land with the first story that needs them (P1: `NotSupported`; P2:
`Limit`).

## Per story: what changes

### US1 References (P1)

1. `ReferenceConstraint` and the state transitions in [data-model.md](data-model.md#reference-one-evaluation).
2. `CharacterDataMarkup` and `LiteralMarkup` own a `ReferenceConstraint`; `EvaluateCharacterDataNonTerminalMarkup`
   and `EvaluateLiteralNonTerminalMarkup` become their `Evaluate`, used by both span and sequence
   paths; `IMarkup.Evaluate`'s `out bool unsupportedCharacter` becomes an `out` reason.
3. `ReadContentCharacterData` and `ReadAttributeValue`: remove the `&` throws (FR-007), throw per
   [exceptions.md](contracts/exceptions.md#story-1-references), set `HasEscapedValue` on commit.
4. `Read()` step 0 clears `HasEscapedValue`.
5. `Throwing`: `Category.NotSupported`, `Markup.Reference`, `Markup.EntityReference`;
   `ThrowUnsupportedNameCharacter` onto `NotSupported` + `Name`.
6. `EBNF`: the five entity-name constants; `GetString()` / `TryGetString()` docs say the bytes are
   raw, references unresolved, with `HasEscapedValue` telling the caller.

### US2 Names (P2)

1. `ElementStack` layout per [data-model.md](data-model.md#elementstack-across-readers).
2. `ReaderOptions.MaxNameLength`; both setters reject `<= 0` and values above the stack's caps
   (2048 bytes, depth 64); the constructors default an unset 0; `MaxDepth` is enforced in
   `ReadStartTag` (Limit + Element at `Depth == MaxDepth`).
3. `NameMarkup.MaxLength` and the name evaluation's budget; `ReadName` (start tag, attribute)
   throws `Limit` + `Name`; `ProcessingInstructionTarget` (renamed in US3) throws `Limit` +
   `ProcessingInstructionTarget` once the instruction is delimited.
4. `ReadStartTag` pushes a span or a sequence with no `stackalloc`; `ReadEndTag` evaluates with
   `TopLength` as budget and compares in full; `TryReadEmptyElementEndingTerminal` calls `Pop()`.
5. `Throwing`: `Category.Limit`, `Markup.ElementStack`.

### US3 BOM and encoding (P3)

1. `ReaderState._byteOrderMark` in rule 22's six places, and the test helpers.
2. Skip BOM step in `Read()` after the first drained check (R7).
3. A behaviour-preserving refactor: the opaque readers take a generic `TConstraint : struct,
   IConstraint` (`NoConstraint`, `ProcessingInstructionTargetConstraint`, and
   `DocumentTypeConstraint` for US1's internal-subset check) instead of
   `isProcessingInstruction` (R8).
4. `DeclarationEncodingConstraint`, the second `IConstraint`, passed by `ReadDeclaration` (R8).
5. `Throwing`: `Markup.ByteOrderMark`, `Markup.Encoding`.

**Scope note** (developer, 2026-11-06): US3 also carries step 3, a refactor broader than "BOM and
declaration". The encoding check needs the opaque readers to run a check before commit, and the
developer preferred one generic way (R8) over a second flag next to `isProcessingInstruction`.
Changing the opaque readers' signature touches every opaque markup: the processing instruction's
existing target check and US1's internal-subset check move into constraints, and the comment passes
`NoConstraint`. Neither the processing instruction nor the document type gains new behaviour; the
refactor has its own green checkpoint (T063), and each constraint its own unit-test file (T056,
T057).

## Documentation changes (D0, G0)

READER.md, in the story that changes the behaviour:

- **Pipeline**: a Skip BOM row between steps 1 and 2 (US3); remove it from "Steps not built yet".
- **Rule 16** (names compared on 32 bytes): replaced by full comparison and `MaxNameLength` (US2).
- **Rule 18** ("Character data ends at `<` or `&`"): character data ends at `<`; references are
  part of the Value and flagged (US1).
- **Rule 21** (a literal's string excludes `&`): references allowed and flagged (US1).
- **Rule 22**: add `_byteOrderMark` to the list of fields that proved the rule (US3).
- **Glossary**: markup delimits, constraint checks; "evaluate" for both (US3, R8).
- **Decided, not built yet**: the unsupported exception is designed as far as a category (R6);
  candidate None no longer lists the BOM.

CHANGELOG.md: `HasEscapedValue`, `MaxNameLength`, BOM, references under Features Added; full name
comparison and the new exception categories under Breaking Changes (a document accepted on 32
bytes now throws).

## Open for review

Nothing is left for review. The developer reviewed every research decision on 2026-11-03: R1,
R2, R5, R6, R7, R8 and R9 approved, R3 renamed to `HasEscapedValue`, R4 approved and rewritten
around a worked layout. Follow-ups noticed: usage tests for public settings are deferred to the CI/CD and packaging spec,
as a gate before publishing to NuGet (developer, 2026-11-03; research.md); PI buffering is bounded only by the later `MaxValueLength`; getters allocate for
value sequences.

## Complexity Tracking

No constitution violations to justify.
