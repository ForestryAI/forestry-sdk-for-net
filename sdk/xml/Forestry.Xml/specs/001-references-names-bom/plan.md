# Implementation Plan: References, names compared in full, BOM

**Branch**: `xml-reader-spec-kit` | **Date**: 2026-11-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-references-names-bom/spec.md`

**Issue**: #64 (feature); each story becomes a sub-issue when tasks are generated; Story 3 links #31.

## Summary

Three thin stories on `Utf8XmlReader`. **P1**: character and predefined entity references are
scanned inside the existing evaluate-then-commit loops of character data and literals, by a small
`ReferenceScanner` carried in the markup struct across segments, so a halted cycle re-scans and
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
| C2 No allocation | scanner is a stack struct; the stack copies names into its inline pool, no `stackalloc` copy; an allocation test is added (R4, R9) | ✅ | ✅ |
| C3 Read contract | each new path is true / false (nothing committed) / throw; no getters or skip added | ✅ | ✅ |
| C4 Test-first | shells per assertion row, reviewed before code, one review task per story (R9) | ✅ | ✅ |
| C5 Ownership | every research decision (R1–R9) reviewed by the developer, 2026-11-03 | ✅ | ✅ |
| C6 On its own | no reference to `Forestry.Deserialize`; SC-004's document is hand-written in this package | ✅ | ✅ |
| D0 Docs | XML docs on `HasEscapedValue`, `MaxNameLength` (listing the names it bounds); READER.md updated (below) | ✅ | ✅ |
| S0 Fail fast | positions per throw in [contracts/exceptions.md](contracts/exceptions.md); checks before commit (rule 26) | ✅ | ✅ |
| S1 Limits | `MaxNameLength` checked at the byte beyond, end tags budgeted by the start-tag name; stack overflow throws, never truncates (R4, R5) | ✅ | ✅ |
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
├── data-model.md        # Phase 1: scanner, stack, options, lifetimes
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
│   ├── Formatting.Resource.cs        # NotSupported and Limit templates (placeholder wording, #49)
│   └── Reading/
│       ├── ElementStack.cs           # US2: byte pool + end offsets, full comparison, Push(sequence), TopLength
│       ├── Extensions.cs             # US2: doc only (no longer used by the stack)
│       ├── ReaderOptions.cs          # US2: MaxNameLength, DefaultMaxNameLength
│       ├── ReaderState.cs            # US3: _isByteOrderMarkChecked (rule 22)
│       ├── Throwing.cs               # US1–3: Category.NotSupported/Limit, new Markup members
│       ├── Utf8XmlReader.cs          # US1: ReferenceScanner, markup structs, HasEscapedValue;
│       │                             # US2: name budgets, ReadStartTag/ReadEndTag/empty-element pop;
│       │                             # US3: Skip BOM step, DeclarationEncoding
│       ├── Utf8XmlReader.Single.cs   # US2/US3: defaults, state copy, declaration check
│       └── Utf8XmlReader.Multiple.cs # US2/US3: defaults, state copy, declaration check
└── test/
    ├── Utf8XmlReaderTests.ReadReferences.cs      # US1 (new)
    ├── Utf8XmlReaderTests.ReadNameLength.cs      # US2 (new)
    ├── Utf8XmlReaderTests.ByteOrderMark.cs       # US3 (new)
    ├── Utf8XmlReaderTests.DeclarationEncoding.cs # US3 (new)
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

1. `ReferenceScanner` and the state transitions in [data-model.md](data-model.md#reference-one-evaluation).
2. `CharacterDataMarkup` and `LiteralMarkup` own a scanner; `EvaluateCharacterDataNonTerminalMarkup`
   and `EvaluateLiteralNonTerminalMarkup` become their `Evaluate`, used by both span and sequence
   paths; `IMarkup.Evaluate`'s `out bool unsupportedCharacter` becomes an `out` reason.
3. `ReadContentCharacterData` and `ReadAttributeValue`: remove the `&` throws (FR-007), throw per
   [exceptions.md](contracts/exceptions.md#story-1-references), set `HasEscapedValue` on commit.
4. `Read()` step 0 clears `HasEscapedValue`.
5. `Throwing`: `Category.NotSupported`, `Markup.Reference`, `Markup.EntityReference`;
   `ThrowUnsupportedNameCharacter` onto `NotSupported` + `Name`.

### US2 Names (P2)

1. `ElementStack` layout per [data-model.md](data-model.md#elementstack-across-readers).
2. `ReaderOptions.MaxNameLength`; both constructors default `<= 0`.
3. `NameMarkup.MaxLength` and the name evaluation's budget; `ReadName` (start tag, attribute)
   throws `Limit` + `Name`; `ProcessingInstructionTarget` throws `Limit` + `ProcessingInstructionTarget`.
4. `ReadStartTag` pushes a span or a sequence with no `stackalloc`; `ReadEndTag` evaluates with
   `TopLength` as budget and compares in full; `TryReadEmptyElementEndingTerminal` calls `Pop()`.
5. `Throwing`: `Category.Limit`, `Markup.ElementStack`.

### US3 BOM and encoding (P3)

1. `ReaderState._isByteOrderMarkChecked` in rule 22's six places, and the test helpers.
2. Skip BOM step in `Read()` after the first drained check (R7).
3. `DeclarationEncoding` scan before the declaration commits, single and multiple paths (R8).
4. `Throwing`: `Markup.ByteOrderMark`, `Markup.Encoding`.

## Documentation changes (D0, G0)

READER.md, in the story that changes the behaviour:

- **Pipeline**: a Skip BOM row between steps 1 and 2 (US3); remove it from "Steps not built yet".
- **Rule 16** (names compared on 32 bytes): replaced by full comparison and `MaxNameLength` (US2).
- **Rule 18** ("Character data ends at `<` or `&`"): character data ends at `<`; references are
  part of the Value and flagged (US1).
- **Rule 21** (a literal's string excludes `&`): references allowed and flagged (US1).
- **Rule 22**: add `_isByteOrderMarkChecked` to the list of fields that proved the rule (US3).
- **Decided, not built yet**: the unsupported exception is designed as far as a category (R6);
  candidate None no longer lists the BOM.

CHANGELOG.md: `HasEscapedValue`, `MaxNameLength`, BOM, references under Features Added; full name
comparison and the new exception categories under Breaking Changes (a document accepted on 32
bytes now throws).

## Open for review

Nothing is left for review. The developer reviewed every research decision on 2026-11-03: R1,
R2, R5, R6, R7, R8 and R9 approved, R3 renamed to `HasEscapedValue`, R4 approved and rewritten
around a worked layout. Follow-ups noticed: `MaxDepth` is never enforced; getters allocate for
value sequences.

## Complexity Tracking

No constitution violations to justify.
