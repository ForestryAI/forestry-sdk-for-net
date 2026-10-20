# READER.md — How `Utf8XmlReader.Read()` works

A quick refresher before writing a new task. Two parts: the **read pipeline** (what runs, in
what order, and what each step guarantees to the next) and the **settled rules** (decisions
that later tasks must not break, each with the issue that decided it).

The package `ARCHITECTURE.md` predates the rebuilt reader (it still describes `ReadDocument`,
`ReadMarkup` and `TokenIndex` rollback). Where they disagree, this page and the issues win.

## 1. Read pipeline

One `Read()` call is one cycle. Every step either **continues** to the next step, **halts** the
cycle (returns `false`: the caller expands the segment(s) and calls again), produces a **token**
(returns `true`) or **throws** (malformed markup).

| # | Step | Issue | What it does | Outcomes |
|---|---|---|---|---|
| 0 | Reset | – | `Value` and `ValueSequence` are cleared | continue |
| 1 | Drained? | – | Nothing left at the reader position | halt; on the last readable segment, throw if there is no root element or an element is not ended |
| 2 | Skip delimiter | #32, #45 | After an attribute name (current token Attribute) and unless the equals flag is set: skip the whole equals non-terminal `S? '=' S?` up to the first non-spacing byte after `=`, which is not checked. Peeks first, commits only when all of it is there, then sets the equals flag; never sets the spacing flag | continue (skipped, or no delimiter applies); halt when undecided, nothing committed; throw when the first non-spacing byte isn't `=` (Expected), or undecided with reading completed (Incomplete) |
| 3 | Skip spacing | #34, #40 | Outside content: skip all spacing. In content: skip only spacing-only character data (next non-spacing is `<`). In a start tag: record that spacing was skipped (the spacing flag) | continue; halt when drained or undecided |
| 4 | Content ready | #22, #40 | `>` after an Element (S3) or an attribute's Value (S5): skip it, content ready = true, clear the spacing flag | continue (never a token); throw after an attribute name (S4: `<a b=>`, reached after `=` is skipped) |
| 5 | Empty element end | #35, #40 | `/>` in a start tag: pop the element stack, clear the spacing flag | continue (not `/`); **ElementEnd** token; halt when `/` is the last byte; throw when malformed or after an attribute name (S5.a: `<a b=/>`, reached after `=` is skipped) |
| 6 | Drained? | – | Step 4 may have skipped `>` as the last byte | halt |
| 7 | Skip spacing-only text | #48 | Only in the cycle where step 4 skipped the start tag's `>` (current token Element, or Value after Attribute): peek past spacing with `TryPeekNonSpacing`; when the next byte is `<`, skip the spacing with `SkipSingleSpacing()` / `SkipMultipleSpacing()` (never sets the spacing flag). Spacing followed by text is left alone, it starts the text | continue; halt when the spacing runs to the end, nothing skipped; throw Incomplete (CharacterData) when that happens with reading completed |
| 8 | Peek starting terminal | #17, #25, #36 | Fill the scratch pad with a starting terminal (+ overshoot), or leave it empty | continue; halt when the pad is incomplete and reading is not completed |
| 9 | Candidate | #27 | Scratch pad + context → one candidate non-terminal | always continue (no side effects) |
| 10 | Delegate | per candidate | Read the candidate from the reader position | token, halt (simulated rollback) or throw |

### Context

Derived from the element stack, no extra state (#27, #34):

| Context | Reader state |
|---|---|
| Prolog | depth == 0 and no root element |
| Start tag | depth != 0 and content ready == false |
| Content | depth != 0 and content ready == true |
| Miscellaneous | depth == 0 and root element |

The content ready flag alone is unreliable at depth 0, so content is always `depth != 0 &&
contentReady` (#34).

### Candidates and delegates

`CandidateNonTerminal.cs`, first match wins:

| Row | Context | Scratch pad | Candidate | Delegate | Status |
|---|---|---|---|---|---|
| S0 | Prolog, no token yet | `<?xml` + spacing | Declaration | `ReadDeclaration` | built #24 |
| S1 | Prolog | `<!DOCTYPE` | DocumentType | `ReadDocumentType` | built #24 |
| S2 | Prolog, Content, Misc | `<!--` | Comment | `ReadComment` | built #24 |
| S3 | Prolog, Content, Misc | `<?` | ProcessingInstruction | `ReadProcessingInstruction` | built #24 |
| S4 | Prolog, Content | `<` + name starting character | StartTag | `ReadStartTag` | built #37 |
| S5 | Content | `</` | EndTag | `ReadEndTag` | built #47 |
| S6 | Start tag | empty | Attribute | `ReadAttribute` | built #40 |
| S7 | Start tag | `"` or `'` | AttributeValue | `ReadAttributeValue` | built #44 |
| S8 | Content | empty, `"` or `'` | CharacterData | `ReadContentCharacterData` | built #48 |
| S9 | anything else | – | None (default delegate) | `ReadNone` | throws a generic malformed exception (#48), specific explanations are a later task |

An **empty** scratch pad means "no starting terminal starts here". It does **not** mean a name
starts here. In a start tag every non-starting-terminal byte (`a`, `1`, `=`, `/`, `>`) reaches
the Attribute candidate.

### Steps not built yet

| Step | Issue | Where | Why it matters |
|---|---|---|---|
| Ignore optional non-terminals | #33 | before peek | Reader options (comments, etc.); a skip step, never a delegate |
| Skip BOM | #31 | start of document | Not part of the grammar; no line tracking |

## 2. Settled rules

### Peek and candidates
1. **Peek never advances and never judges.** It records what starts here; whether the markup is
   well-formed is decided by later steps (#17, #25).
2. **The scratch pad holds a starting terminal (plus overshoot) or is empty.** Never an ending
   terminal, never a byte that starts no terminal (#25).
3. **Only starting terminals become candidates.** Ending terminals (`>`, `/>`) are pipeline
   steps, not candidates (#25, #27, #35). If this comes up again, this rule is the answer.
4. **The candidate is the outermost non-terminal that starts at the pad.** Known break from the
   EBNF: `STag` and `EmptyElemTag` share one **StartTag** candidate; the ending terminal decides
   later (#27).
5. **Delegation only chooses, never validates.** Extra pad characters are checked only to choose
   between candidates. Reader state is used only when it changes the choice. No side effects
   (#27).
6. **Pass only the state that's needed.** The candidate takes the span, the element stack by
   `in` and the current token type, not the whole `ReaderState`; members it reads stay
   `readonly` (code remark in `CandidateNonTerminal`).

### Delegates and steps
7. **Delegates start at the reader position**, re-reading the scratch pad, so each is
   self-contained (#27).
8. **The true / false / throw contract** (#24, #27):
   - `true`: exactly one token; previous token type = current, current = the new token; `Value`
     or `ValueSequence` set.
   - `false`: nothing committed (simulated rollback); only when reading is not completed.
   - throw: malformed markup, or reading completed without the ending.
9. **Simulated rollback = search first, commit on success** (#24, #38). Prefer it to recording
   and restoring positions.
10. **Delegates never consume trailing spacing.** The next cycle's skip spacing does (#24).
11. **Once delegated, a candidate always produces a token.** Ignoring something (comments by
    reader option) is a skip step before delegation (#24, #33).
12. **Skip steps return `true` to continue and `false` to halt** (drained or undecided), so
    `false` means halt everywhere in the pipeline, as for `Read()`, the delegates and the peek.
    `Read()` jumps to its `Halted` label. Before #32, skip spacing returned `true` to halt
    (#27, #34, #32).

    **A step that continues leaves the reader on a readable byte**, never at the end of a
    segment: the next step indexes `_segment[_segmentPosition]`. With multiple segments that means
    finishing a commit with `SkipMultipleSpacing()` (or `TrySkipEmptySegments()`) even when there
    is nothing to skip, e.g. after an `=` that is a segment's last byte (#32's M1).

### Markup, names, positions
13. **One malformed markup makes the document malformed.** `MarkupState` is Unknown /
    WellFormed / Malformed; Unknown means the context decides (#39).
14. **Names are ASCII only in the POC.** A non-ASCII byte throws "unsupported" (#38).
15. **A name ends at the first non-name byte** (no ending terminal), so a segment ending inside a
    name is undecided: `false`, or a token when reading is completed (#38).
16. **Element names are compared on their first 32 bytes.** A `ValueSequence` name is copied to a
    32-byte `stackalloc` before pushing (#37, #23).
17. **Line and position are 0-based internally, 1-based in exceptions.** Line position counts
    bytes, not characters (#24, bug #30). Only a line feed ends a line; a carriage return adds one
    to the position like any other byte, so `\r\n` is one line end (`Utf8Reader.Track`). A lone
    `\r` is **not** a line end, unlike XML 1.0 §2.11: a known gap, harmless for `\r\n` files.
18. **Spacing in content may be character data.** Spacing-only character data is skipped by
    default; a reader option later (#34).

    **Indentation never produces a Value token** (#48). Skip spacing (step 3) runs before
    content ready (step 4), so in the cycle that skips a start tag's `>` the spacing after it is
    left to step 7, which applies the same rule. Text keeps its spacing at both ends:
    `<a>
  A<b/>` gives the Value `
  A`, `<a>A
<b/>` gives `A
`.

    **Character data ends at `<` or `&`** and is the raw bytes before it: control bytes other
    than tab, line feed and carriage return are malformed, every other byte (quotes, `>`, bytes
    ≥ 0x80) is plain text, as in literals (rule 21). `&` starts a reference, unsupported in the
    POC: the text before it is a Value token, then the next cycle starts at `&` (an empty pad,
    so character data again) and the delegate throws Expected for its length 0 with the
    unsupported flag. `]]>` is not checked.

### Attributes
19. **An attribute needs spacing before it**, but spacing is skipped before the candidate sees
    the pad. Skip spacing records it in the start tag (`_hasSkippedSpacingInStartTag`, carried in
    `ReaderState` and copied by both constructors). The attribute delegate throws when it's not
    set, and clears it on success; `>` and `/>` clear it when closing the start tag (#40).

    **Skip delimiter owns the spacing around `=`** (`Eq ::= S? '=' S?`) and runs before skip
    spacing, so that spacing never sets the spacing flag and `<a b ="1"c="2">` still throws at
    `c`. It peeks with `TryPeekNonSpacing` (offsets only, no line tracking), then commits with
    `SkipSingleSpacing()` / `SkipMultipleSpacing()`, which track lines but don't set the flag.
    Never `SkipSpacing()` (#32).
20. **A dangling attribute name throws in skip delimiter**, as an expected `=` (`<a b>`, `<a b/>`),
    before either ending terminal is reached. #40's checks at content ready S4 and empty element
    S5.a throw "attribute value missing" when `=` was skipped but no literal follows (`<a b=>`,
    `<a b=/>`) (#40, #32, #45).
21. **The attribute delegate checks the spacing flag before reading the name**, and its
    candidate never checks the first character, so #38's malformed first character is
    reachable here (`<a 1="x">`), unlike in the start tag (#40).

    **A skipped `=` is recorded in `_hasSkippedAttributeDelimiter`** (the equals flag): skip
    equals sets it when it commits, skip delimiter continues at once while it is set, and the
    attribute value delegate clears it when it produces the Value. Without it, a value that halts
    after `=` was committed in the same cycle makes the next reader expect `=` again
    (`<a b="1|0"/>`) (#45).

    **A value expected after `=` is the attribute delegate's check, not the candidate's.** In
    `<a b=1/>` the `1` is an empty pad, so the candidate stays Attribute (rule 5). The delegate's
    guard throws "attribute value missing" when the current token is Attribute and the equals flag
    is set, **before** the spacing check; it reads no bytes, so it reports the byte after `=` and
    never halts. The attribute name is not in `Value` by then (rule 23) (#41).

    **A literal's string is any bytes up to the matching quote except `<`, `&` and control bytes**
    other than tab, line feed and carriage return; the other quote kind is content. Bytes
    ≥ 0x80 pass unchecked (no UTF-8 validation, no ASCII rule as for names), and `Value` is the
    raw bytes between the quotes: not normalized, unlike XML 1.0 §3.3.3. `&` starts a reference,
    well-formed but unsupported in the POC; until the unsupported exception is designed it throws
    Expected (#44). The starting quote is guaranteed by candidate S7 (a `Debug.Assert`); the
    current token is not, so a value without an attribute name throws (`<a "1">`, #44's G0).

### Reader state
22. **A new `ReaderState` field touches six places**: the `ReaderState` field, its public
    constructor (default value) and its internal constructor (parameter); the reader's own field
    and its `ReaderState` property; and **both** reader constructors, `Utf8XmlReader.Single.cs`
    and `Utf8XmlReader.Multiple.cs`, which copy it from the state. Missing the constructor copy
    loses the field whenever a reader is reconstructed, i.e. on every piped segment (#24's
    `_documentType`, #40's spacing flag, #45's equals flag). Test helpers that build a `ReaderState` by named
    arguments need the new argument too.
23. **`Value` lives for one `Read()` only.** Step 0 clears `Value` and `ValueSequence`, so a
    later cycle sees the previous token's type but not its value (e.g. the attribute name when
    #41's guard throws after `=`, the popped name on `/>`). Anything needed across cycles must be
    stored in the reader (a `ReaderState` field, rule 22) or left out (#35, #41).

    **A value ending exactly at a segment's end is a `Value`, not a `ValueSequence`.** The
    sequence slice's end position is then the start of the next segment, so `IsSingleSegment`
    is false although every byte is in the first span; `AdvanceValue` checks the first span's
    length too (#48's M1).

### Exceptions
24. **An exception is a category (why) + a markup (what).** `Throwing.Category` picks the
    message template; the markup only fills it. Pass a found byte (Expected) or bytes
    (Incomplete, Name) only when the message should show them (#42).
25. **A throw site reports the offending byte by passing an offset**: the number of bytes from
    the committed position to that byte. The exception walks those bytes with the reading line
    rules (`Utf8Reader.Track`, rule 17) and never commits. Incomplete passes `RemainingLength`
    (end of the data); an opaque value missing its ending terminal passes 0 (its start) (#43).
26. **Every check runs before its step commits.** The offset can't point backwards, so a check
    after the commit would report the end of what was committed, not the offending byte (#43,
    e.g. the processing instruction target).

### Decided, not built yet
- **The unsupported exception is not designed** (#43). `&` in a literal (#44), `&` in
  character data (#48) and a non-ASCII name byte (#38) are its users so far.
- **Candidate None throws one generic malformed exception** (#48): `Throwing.ThrowUnexpectedMarkup`
  ("… starts no markup allowed here"), an `XmlException` without category or markup, like the
  unsupported name throw. Its cases (second root, text or an end tag outside the root, a document
  type after the prolog, `<` without a name, markup inside a start tag, a BOM until #31) need
  specific category + markup explanations (rule 24) in their own task.
- **`/>`'s ElementEnd has an empty `Value`.** Exposing the popped name needs storage in the
  reader; revisit later (#35).

## 3. Conventions

- State table rows are `S0`, `S1`, … Another task's state is written `#17's S3`.
- `|` in examples marks a segment boundary.
- Bugs are written in the same format as tasks, Velocity included.
- Reuse an issue number only if it has no commits.
