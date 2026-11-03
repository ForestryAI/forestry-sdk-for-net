# Research: References, names compared in full, BOM

**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-11-03

Phase 0 of `/speckit-plan`. The spec has no clarification markers, so this page resolves the *how*:
each decision below is grounded in the current source (`src/Reading/`) and `READER.md`, and is a
proposal until the developer confirms it (C5). Every decision was reviewed by the developer on
2026-11-03: R1, R2, R5, R6, R7, R8 and R9 approved, R3 renamed, R4 approved and rewritten.

## What the source does today

Findings that shape every decision below (file and line as of `e5cec73`).

- **Evaluate then commit.** Each delegate evaluates its markup from the committed position without
  advancing (`Evaluate…NonTerminalMarkup` over a span, `EvaluateMultipleMarkup<T>` over the sequence,
  carrying the `IMarkup` struct's state from segment to segment), then commits once with
  `AdvanceValue` (`Utf8XmlReader.cs:1314`). `MarkupState.Unknown` means the bytes ran out: halt when
  reading is not completed, Incomplete otherwise. A halted cycle commits nothing, so the next reader
  re-evaluates from the same committed position (C1, READER.md rules 8, 9).
- **Scan state never survives a halt.** The only state carried across readers is `ReaderState`
  (rule 22); scan state lives in the `IMarkup` struct for one evaluation. `ProcessingInstructionTarget`
  (`Utf8XmlReader.cs:1529`, renamed `ProcessingInstructionTargetConstraint` in R8) is the existing example of a small state machine scanned segment by
  segment with an `OffendingIndex`.
- **`&` today.** `EvaluateCharacterDataNonTerminalMarkup` ends character data at `&` with
  `unsupportedCharacter = true` (`:1505`), so the text before it is a Value and the next cycle throws
  Expected at the `&` (`ReadContentCharacterData`, `:1182`). `EvaluateLiteralNonTerminalMarkup`
  returns Malformed at `&` (`:1467`) and the delegate throws Expected (`:1277`). Both are FR-007.
- **`&` reaches character data.** `&` is not a starting-terminal first character, so in content it
  leaves an empty scratch pad and candidate S8 (CharacterData) delegates; skip spacing (S4) keeps
  spacing before `&` as text. References need no candidate or pipeline change.
- **Names are packed to 32 bytes.** `ElementStack` stores `NonAllocatingMaxDepth (64) ×
  PackedNameLength (4 ulongs)` inline (`ElementStack.cs:31`, 2 KB) and `Extensions.Pack` truncates;
  `ReadStartTag` and `ReadEndTag` copy a straddling name into a 32-byte `stackalloc`
  (`Utf8XmlReader.cs:1042`, `:1111`); `TryReadEmptyElementEndingTerminal` unpacks into one
  (`:859`). Names are evaluated to their end with no bound (`EvaluateNameNonTerminalMarkup`, `:1391`).
- **Options.** `ReaderOptions` has one option, `MaxDepth`: a negative value throws in the setter
  (`Throwing.WhenValueNotPositive`), and both reader constructors replace `<= 0` with
  `DefaultMaxDepth` (`Utf8XmlReader.Single.cs:50`, `Utf8XmlReader.Multiple.cs:77`). `ReaderState`
  carries the whole `ReaderOptions` struct, so a new option needs no new `ReaderState` field.
  `MaxDepth` is defaulted but never enforced (this spec enforces it, R5).
- **Exceptions.** `Throwing.Category` is why the markup is malformed (Incomplete, Expected, Name,
  Order, Unique); `XmlException.Category` carries its name. The only "not supported" throw,
  `ThrowUnsupportedNameCharacter`, has no category, so it cannot be told apart from
  `ThrowUnexpectedMarkup` except by message.
- **Declaration.** Read as an opaque value (`ReadDeclaration`, `:919`); it is "first" when line and
  position are both 0. The processing instruction target is checked before commit with
  `isProcessingInstruction` (`Utf8XmlReader.Single.cs:173`, `Multiple.cs:283`).
- **No performance test in this package.** `Performance.runsettings` exists, but no test carries
  `Category=Performance` yet (the deserialization silo's `MemoryTests` is the pattern). SC-003 and
  SC-005 need one.

## R1. Reference scanning inside character data and literals

**Decision** (approved by the developer, 2026-11-03; named a constraint in R8): A `ReferenceConstraint` struct (internal, in `Utf8XmlReader.cs`'s markup region) scans
one reference byte by byte from `&` to `;`. `CharacterDataMarkup` and `LiteralMarkup` each hold one
and hand it every byte from `&` on, so a reference that straddles segments carries its state like
`NameMarkup` does. The text scan and the reference scan are one loop: a Value is text and references
up to `<` (content) or the quote (literal). The span paths call the same markup structs, so there is
one constraint for both.

States: after `&` → `#` (character reference) or a name start (entity reference); after `&#` → `x`
(hex) or a decimal digit; digits accumulate into an `int` code point (leading zeros add nothing);
`;` ends the reference and is checked for an empty number, an empty name and a legal Char.

Entity names are matched against five EBNF constants (`AmpersandEntityName` `amp`,
`LessThanEntityName` `lt`, `GreaterThanEntityName` `gt`, `QuoteEntityName` `quot`,
`ApostropheEntityName` `apos`, a new `#region Entities` in `src/EBNF.cs`), chosen by the first byte
after `&` (the second after `a`) and compared byte by byte; the constraint only checks references and
never unescapes (developer, 2026-11-03). A later unescaping getter (the `// TODO` in
`GetUnescapedValue`, out of scope here) will switch on the same constants.

**Rationale**: Re-scanning from the committed position after a halt (C1) means the constraint needs no
`ReaderState` field and no rollback; it is the PI-target pattern applied to a second construct. It
adds no allocation (C2): a few `int`/`byte` fields in a struct on the stack.

**Alternatives considered**:
- *A reference as a token of its own*: rejected by the spec (a Reference is never a token) and it
  would split one Value into several.
- *Ending character data at `&` and reading the reference in its own cycle* (today's shape): gives
  several Values per text, so the flag and the deserializer would have to join them.
- *Resolving references into a buffer*: allocates or needs a scratch buffer per value; the spec
  returns raw bytes and leaves unescaping to the caller.

## R2. Where a malformed reference is reported

**Decision** (approved by the developer, 2026-11-03): At the first byte where the reference is known to be malformed (S0), as an offset
from the committed position like every other throw (rule 25):

| Case | Example | Reported at | Category |
|---|---|---|---|
| Bare `&` | `x & y` | the byte after `&` (the space) | Expected |
| Missing `;` | `&amp<`, `&#65"` | the first byte that is neither a digit/name byte nor `;` | Expected |
| Empty reference | `&;`, `&#;`, `&#x;` | the `;` | Expected |
| Non-digit | `&#6A;`, `&#xG;` | the non-digit (`A`, `G`) | Expected |
| Uppercase `X` | `&#X41;` | the `X` | Expected |
| Not one of the five, no document type | `&nbsp;`, `&ampx;` | the first byte that no predefined name continues with (`n`, `x`) | Name |
| Above U+10FFFF / too many digits | `&#x110000;`, `&#99999999999999999999;` | the digit that takes the value above U+10FFFF | Expected |
| Not a legal Char | `&#0;`, `&#xD800;`, `&#xFFFE;` | the `;` (until then a digit could still make it legal, e.g. `&#0` + `9`) | Expected |
| Entity name with a document type | `<!DOCTYPE a [<!ENTITY e "x">]><a>&e;</a>` | the `&` | NotSupported (R6) |
| Reading completed inside | `<a>&amp` | the end of the data (`RemainingLength`) | Incomplete |

FR-005a's digit counts (7 decimal, 6 hex) follow from the per-digit range check: an eighth
significant decimal digit or a seventh hex digit always exceeds U+10FFFF, so one check covers both
and the value never wraps around. Tests still cover each count.

**Rationale**: S0 asks for the first byte where it is known. Without a document type, an entity
can only be one of five names of at most 4 bytes, so a name is known to be undeclared at its first
diverging byte, and `MaxNameLength` is never reached. With a document type (`_documentType` is
already in the reader), the name may be declared, so it is scanned as a whole `Name` bounded by
`MaxNameLength` (FR-010), must end with `;`, and only then throws "not supported" at the `&`: the
reference as a whole is what is not supported.

**Alternatives considered**: reporting every malformed reference at its `&` (simpler, but not the
first byte where it is known); reporting "not supported" at the name's first byte (equally valid;
the `&` reads better for "this reference is not supported").

## R3. The references flag

**Decision** (named by the developer, 2026-11-03): `public bool HasEscapedValue { get; private set; }` on `Utf8XmlReader`, cleared in
step 0 with `Value` and `ValueSequence`, set by `ReadContentCharacterData` and `ReadAttributeValue`
when they commit a Value whose scan saw at least one reference. Not in `ReaderState` (rule 23).
Named by the developer (2026-11-03).

**Rationale**: The behaviour is `Utf8JsonReader.ValueIsEscaped`'s, which the spec cites as the
precedent: raw bytes plus a flag, unescaping left to the caller. The name is the developer's
choice; "escaped" is also the word the code already uses (`GetUnescapedValue()`,
`Utf8XmlReader.Get.cs:15`, "TODO: When escaped convert").

**Alternatives considered**: `ValueIsEscaped` (`Utf8JsonReader`'s name), `ValueContainsReferences`
or `ValueHasReferences` (the spec's "references flag").

## R4. Names compared in full: the element stack

**Decision**: The stack keeps whole names in two inline arrays and a depth (approved by the
developer, 2026-11-03):

- `_names`: `[InlineArray(2048)] byte`, the open names' bytes laid end to end
  (`NonAllocatingMaxDepth × 32`, the same 2 KB as today).
- `_ends`: `[InlineArray(64)] ushort`, the end offset of each open name in `_names`.
- `_depth`: how many names are open. Depth stays capped at 64 (`NonAllocatingMaxDepth`).

A worked layout, inside `<ab><Log><LogDiameter>`:

```text
_names: "ab" "Log" "LogDiameter"    (bytes 0-1, 2-4, 5-15)
_ends:   2    5     16
_depth:  3
```

In plain steps:

- **Top name**: `_names[_ends[_depth - 2] .. _ends[_depth - 1]]`, starting at 0 when there is one
  name. Here `_names[5..16]` = `LogDiameter`.
- **Push**: copy the name's bytes to `_names` from the current end (a `ReadOnlySequence` name
  straight in with `CopyTo`), then record the new end in `_ends[_depth]` and add one to `_depth`.
  Pushing `Length` above gives `_ends: 2 5 16 22`.
- **TryPeek**: compare lengths first, so an end tag never buffers past the start-tag name
  (FR-011; the end tag's budget is the top name's length, R5). Then compare the bytes with
  `SequenceEqual` for a span, or `SequenceReader<byte>.IsNext` for a sequence, in full and with no
  copy.
- **Pop**: `_depth--`. Nothing is cleared or copied; the next push overwrites the bytes.
- A push that does not fit the 2048 bytes throws "not supported" (R6), as depth beyond 64 already
  throws today. A running limit on the open names is the later limits spec (spec Assumptions).

This removes the three `stackalloc` copies (`Utf8XmlReader.cs:859` in the `/>` pop, `:1042` in
`ReadStartTag`, `:1111` in `ReadEndTag`) and the stack's use of `Extensions.Pack` and `Unpack`.
`ReaderState` stays about 2 KB (2048 + 128 bytes of offsets, against 2048 today). `Pack` and
`Unpack` stay public and unchanged (whether `Extensions` stays public is out of scope); their doc
no longer refers to the stack.

**Rationale**: Comparing in full needs the whole name. Raising the packed slot to 128 bytes would
make the stack 8 KB, and `ReaderState` copies it twice per segment (the `ReaderState` property and
the next constructor). Laid end to end, typical StanForD names (35 bytes at most, a depth of about
ten) use a few hundred bytes of the same 2 KB. Exact byte comparison is what FR-008 asks; nothing
is hashed or truncated (S1).

**Alternatives considered**:
- *One span with a length after each name* (no `_ends`): saves only the 128-byte offsets array, and
  finding the top name means reading a length back from the end, which makes the arithmetic less
  clear. Considered by the developer, who agreed to the separate offsets array.
- *A 128-byte packed slot per level*: `MaxNameLength` is a runtime option while an inline array's
  size is a constant, and 8 KB per copy.
- *Hash plus length*: not exact, a collision would accept a mismatch (C0).
- *A heap buffer*: allocates per reader state (C2).

## R5. `MaxNameLength`: where names are bounded

**Decision** (approved by the developer, 2026-11-03):

- `ReaderOptions.MaxNameLength`, like `MaxDepth`: `DefaultMaxNameLength = 128`. Both setters
  (`MaxDepth` and `MaxNameLength`) throw `InvalidOperationException` via
  `Throwing.WhenValueNotPositive` for `value <= 0`, so an explicit 0 is rejected (today `MaxDepth`
  rejects only `< 0`, and an explicit 0 quietly becomes 64). The `MaxNameLength` setter also
  rejects a value above `ElementStack.NameBytesCapacity` (2048, R4), rather than letting it fail
  later as a Limit throw at the first long name, and the `MaxDepth` setter likewise rejects a value
  above `ElementStack.NonAllocatingMaxDepth` (64), the stack's depth cap. `MaxDepth` is enforced
  (S1; developer, 2026-11-03): in `ReadStartTag`, when `Depth == MaxDepth`, the start tag throws
  Limit + Element at the start tag, before committing; with the default 64 that is the 65th open
  element. NotSupported + ElementStack remains only for the 2 KB name pool overflowing (R4, R6). The constructors are unchanged: `ReaderOptions` is
  a struct, so a field still 0 means never set and gets the default (64 / 128), and
  `default(ReaderOptions)` keeps working (developer, 2026-11-03). This departs on purpose from
  `JsonReaderOptions`, which accepts an explicit 0 as "default": a mistake throws where it is
  known. Carried by `ReaderState._readerOptions`, so rule 22's six places are not touched for it.
- `NameMarkup` gets a `MaxLength` init property and `EvaluateNameNonTerminalMarkup` a remaining
  budget: a name byte at index `MaxLength` is Malformed with a "too long" reason, reported at that
  byte, so the scan never looks further (S1). Used by `ReadName` (start tag and attribute names) with
  `MaxNameLength`.
- `ProcessingInstructionTargetConstraint` stops at the same bound (FR-010), checked once the
  instruction is delimited: the PI is opaque and found by its ending terminal `?>` first, then the
  constraint is evaluated over it (R8's "delimit, then check"). So the bound decides where it
  throws, not how much is buffered; the instruction's body is unbounded anyway until the later
  `MaxValueLength` spec, which is what bounds that buffering (developer, 2026-11-03, option B of
  the analysis).
- An entity name is bounded only when a document type exists (R2); without one it never exceeds 4
  bytes.
- `ReadEndTag` uses the top name's length as its budget (FR-011): a name byte at that index is a
  mismatch, reported like today's mismatch at the name's start (`offset: startingTerminalLength`), so
  an end tag buffers at most the start-tag name plus one byte and never throws a name-length error.

The throw for a too-long name is a new category, `Limit` (R6), so it is not mistaken for a naming
rule. The XML doc on `MaxNameLength` lists exactly these names: element names in start tags,
attribute names, processing instruction targets, and entity names in references (D0).

**Rationale**: FR-009 to FR-011 and S1. The budget lives in the markup struct, so a name straddling
segments is bounded across them with no new state.

**Alternatives considered**: checking the length after the name is evaluated (buffers the whole name
first, breaking S1); bounding end tags by `MaxNameLength` (the spec says an end tag only fails on a
mismatch).

## R6. "Not supported" and "limit" throws

**Decision** (approved by the developer, 2026-11-03): Two new `Throwing.Category` members, `NotSupported` and `Limit`, so
`XmlException.Category` tells them apart from the malformed categories (FR-015). Templates in
`Formatting.Resource.cs`: `NotSupported` "Not supported: {0}{1}" and `Limit` "Limit exceeded:
{0}{1}" — placeholder wording, the final wording is #49's. New `Throwing.Markup` members:
`Reference` (character and entity references), `EntityReference` (an entity with a document type),
`ByteOrderMark`, `Encoding`, `ElementStack` (pool overflow, R4).
`ThrowUnsupportedNameCharacter` moves onto `NotSupported` + `Name`, so every "not supported" is
found the same way.

**Rationale**: READER.md rule 24 (category + markup) and "The unsupported exception is not designed
(#43)": this spec needs it designed far enough to be distinguishable, not worded.

**Alternatives considered**: a subclass `XmlNotSupportedException` (a second public type for one
property's worth of information); keeping the name throw as it is (two ways to recognise "not
supported").

## R7. BOM: a step before the first token

**Decision** (approved by the developer, 2026-11-03): A new pipeline step, **Skip BOM**, after step 1 (Drained?) and before step 2 (Skip
delimiter), active only while a new `ReaderState` field `_isByteOrderMarkChecked` is false:

- `EF BB BF`: skip 3 bytes with no line tracking (FR-012); finish with `SkipMultipleSpacing`-style
  `TrySkipEmptySegments` so the step leaves the reader on a readable byte (rule 12), then the
  drained check: a BOM-only document throws Incomplete RootElement as an empty document does.
- Every incomplete start of a listed BOM at the end of the data (`EF`, `EF BB`, `FE`, `FF`, `00`,
  `00 00`, `00 00 FE`) halts when reading is not completed, so a split document reads as the whole
  one does (C1; developer, 2026-11-03). When reading is completed, `EF` or `EF BB` throws
  Incomplete RootElement (an incomplete document), and `FE`, `FF`, `00`, `00 00`, `00 00 FE` skip
  nothing and throw what an unrecognised start throws today: the generic "starts no markup"
  (`ThrowUnexpectedMarkup`, candidate None) at line 1, position 1.
- `FE FF`, `FF FE` (covers `FF FE 00 00`), `00 00 FE FF`: throw NotSupported + ByteOrderMark at
  line 1, position 1 (offset 0). `FF FE` throws at once without waiting for `FF FE 00 00`, since
  both are NotSupported at offset 0.
- Anything else: nothing skipped.
- The field is set once the step has decided (skipped, or nothing to skip), so a later reader never
  checks again and a second `EF BB BF` is never skipped.

The field touches rule 22's six places (the `ReaderState` field and both constructors, the reader's
field and `ReaderState` property, both reader constructors) and the test helpers that build a
`ReaderState` by named arguments.

**Rationale**: Without the field, a reader that skipped the BOM and then halted before the first
token (`EF BB BF|<a/>`) leaves line 0, position 0 and token None, exactly what a fresh document
looks like, so a second BOM would be skipped silently (S0). The declaration's "first" check (line
and position both 0) still holds after the BOM because the BOM is not tracked.

**Alternatives considered**: deciding "at the start" from line, position and token type alone (the
double-BOM gap above); skipping the BOM in the constructor (a constructor cannot halt, and a BOM can
straddle segments).

## R8. Encoding in the declaration

**Decision** (approved by the developer, 2026-11-03): Before the declaration is committed (rule 26), a `DeclarationEncodingConstraint`, evaluated segment by segment like
`ProcessingInstructionTargetConstraint`, looks for the first `encoding` preceded by
spacing, then `S? '=' S?`, a quote, and the name up to the matching quote. The name is compared
with `UTF-8` ignoring ASCII letter case on one path: each chunk is compared with
`System.Text.Ascii.EqualsIgnoreCase` against the matching slice of `"UTF-8"u8`, starting from
what is matched so far (no allocation, no separate span and segment versions). Not `| 0x20`, which
folds more than letters (`0x0D | 0x20` is `-`, `0x18 | 0x20` is `8`, so `UTF\r8` would match;
developer, 2026-11-03). A different name, or one longer than 5 bytes, throws NotSupported +
Encoding at the name's first byte. No `encoding`, or text after `encoding` that does
not have that shape, throws nothing: the rest of the declaration stays unchecked, as today. The
Declaration token's `Value` stays the raw declaration.

**How the opaque readers run a constraint** (developer, 2026-11-03): the opaque readers take a
generic struct, not a flag or a delegate. An internal `IConstraint` has `MarkupState
Evaluate(ReadOnlySpan<byte> characters)`, evaluated segment by segment over the value with its
state in the struct, plus `int OffendingIndex`, `Throwing.Category Category` and `Throwing.Markup
Markup`; `NoConstraint` is always well-formed. `ReadSingleOpaqueValue<TConstraint>` /
`IgnoreSingleOpaqueValue<TConstraint>` and their Multiple counterparts take `where TConstraint :
struct, IConstraint`, with one check-then-throw before commit in each path; `isProcessingInstruction`
is removed and no `isDeclaration` is added. The declaration passes `DeclarationEncodingConstraint`,
the processing instruction `ProcessingInstructionTargetConstraint` (today's
`ProcessingInstructionTarget`, renamed), comment and document type `NoConstraint`. Not a delegate:
`Utf8XmlReader` is a ref struct, so a lambda cannot capture it, and the span and sequence paths
would need separate delegate types; the generic struct is the idiom the reader already uses in
`EvaluateMultipleMarkup<T> where T : IMarkup`.

**Vocabulary**: markup delimits, constraint checks; "evaluate" is the verb for both. A markup is
evaluated to its end (`IMarkup`: `NameMarkup`, `CharacterDataMarkup`, `LiteralMarkup`); a
constraint is evaluated over its value (`IConstraint`, after the XML specification's
"well-formedness constraint"). `ReferenceConstraint` (R1) shares the noun but does not implement
`IConstraint`: it runs byte by byte inside a markup's evaluation, not over a delimited value, and
reports `HasReferences`, a `MarkupReason` and `ReferenceStart`, which `IConstraint` has no place
for. Every constraint is `internal` (nested in `Utf8XmlReader`, no longer `private`) so it is tested
as a unit, fed `Evaluate` directly whole and chunk by chunk, like `ElementStack`; reader-level tests
keep line and position, halts and the token (developer, 2026-11-03).

**Rationale**: FR-014 and XML 1.0 §4.3.3 (encoding names are case-insensitive). The check is bounded
by the declaration, which is already found by its ending terminal.

**Alternatives considered**: checking the whole `XMLDecl` production (version, standalone): out of
scope, the spec keeps the declaration opaque; comparing after decoding to a string (allocates).

## R9. Tests and the performance gate

**Decision** (approved by the developer, 2026-11-03):

- Test shells first (C4), one file per area, Arrange/Act/Assert (A4):
  `Utf8XmlReaderTests.ReadReferences.cs` (US1), `Utf8XmlReaderTests.ReadNameLength.cs` and updates to
  `ReadEndTag`/`ReadStartTag`/`ElementNameStackTests` (US2), `Utf8XmlReaderTests.ByteOrderMark.cs`
  and `Utf8XmlReaderTests.DeclarationEncoding.cs` (US3).
- The constraints (`ReferenceConstraint`, `ProcessingInstructionTargetConstraint`,
  `DeclarationEncodingConstraint`, `NoConstraint`) are tested as units in one file each
  (`ReferenceConstraintTests.cs`, `ProcessingInstructionTargetConstraintTests.cs`,
  `DeclarationEncodingConstraintTests.cs`), fed `Evaluate` whole and chunk by chunk at every split;
  the reader-level files keep representative rows (developer, 2026-11-03, R8).
- Every scenario is read whole, as two segments split at every byte position (the existing
  `SequenceSplitAt` helper in `Utf8XmlReaderTests.PeekStartingTerminal.cs`, moved to a shared
  partial so every file can use it), and as input arriving in parts (a new reader per halt, built
  from the previous `ReaderState`) (SC-001).
- Existing tests that encode the old behaviour change with it: `ElementNameStackTests`'s
  capped-prefix test (now a mismatch), the `Pop(stackalloc …)` callers, and the `&` "unsupported"
  cases in `ReadCharacterData` and `ReadAttributeValue`.
- A first `Category=Performance` test, `ReadAllocationTests`, reads a generated document in a loop
  and compares `GC.GetAllocatedBytesForCurrentThread()` with and without references (SC-003); it
  makes `dotnet test -s Performance.runsettings` run something at the end of each story (SC-005).
- SC-004 uses a hand-written StanForD-shaped document (BOM, `ButtEndProfileExtrapolationFunction`,
  `&amp;`) in this package's tests, so the reader silo needs no sample file from the deserialization
  silo (C6).

**Rationale**: C4, A4, SC-001 to SC-005. The gate needs at least one performance test to mean
anything.

## Follow-ups noticed (not this spec)

- Usage tests for public settings (every public option has tests for its default, an override and
  the values it rejects) are deferred to the CI/CD and packaging spec, as a gate before publishing
  to NuGet; not added to the constitution or to this spec, which meets it anyway through its option
  shells (developer, 2026-11-03).
- Processing instruction buffering (limits spec): a PI target is checked against `MaxNameLength`
  only once `?>` is found, so a PI is buffered whole first; the later `MaxValueLength` bounds that
  (FR-010, R5; developer, 2026-11-03).
- `Utf8XmlReader.GetString()` and `GetUnescapedValue()` allocate (`ToArray()`) for a value sequence;
  typed getters are out of scope (C3).
