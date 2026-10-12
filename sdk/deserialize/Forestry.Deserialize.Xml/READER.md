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
| 2 | Skip delimiter | #32 | After an attribute name (current token Attribute): skip the whole equals non-terminal `S? '=' S?` up to the first non-spacing byte after `=`, which is not checked. Peeks first, commits only when all of it is there; never sets the spacing flag | continue (skipped, or no delimiter applies); halt when undecided, nothing committed; throw when the first non-spacing byte isn't `=` (Expected), or undecided with reading completed (Incomplete) |
| 3 | Skip spacing | #34, #40 | Outside content: skip all spacing. In content: skip only spacing-only character data (next non-spacing is `<`). In a start tag: record that spacing was skipped (the spacing flag) | continue; halt when drained or undecided |
| 4 | Content ready | #22, #40 | `>` after an Element (S3) or an attribute's Value (S5): skip it, content ready = true, clear the spacing flag | continue (never a token); throw after an attribute name (S4, unreachable since #32) |
| 5 | Empty element end | #35, #40 | `/>` in a start tag: pop the element stack, clear the spacing flag | continue (not `/`); **ElementEnd** token; halt when `/` is the last byte; throw when malformed or after an attribute name (S5.a, unreachable since #32) |
| 6 | Drained? | – | Step 4 may have skipped `>` as the last byte | halt |
| 7 | Peek starting terminal | #17, #25, #36 | Fill the scratch pad with a starting terminal (+ overshoot), or leave it empty | continue; halt when the pad is incomplete and reading is not completed |
| 8 | Candidate | #27 | Scratch pad + context → one candidate non-terminal | always continue (no side effects) |
| 9 | Delegate | per candidate | Read the candidate from the reader position | token, halt (simulated rollback) or throw |

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
| S5 | Content | `</` | EndTag | – | not built |
| S6 | Start tag | empty | Attribute | `ReadAttribute` | built #40 |
| S7 | Start tag | `"` or `'` | AttributeValue | – | not built |
| S8 | Content | empty, `"` or `'` | CharacterData | – | not built |
| S9 | anything else | – | None (default delegate) | – | not built |

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
    S5.a are kept but unreachable (#40, #32).
21. **The attribute delegate checks the spacing flag before reading the name**, and its
    candidate never checks the first character, so #38's malformed first character is
    reachable here (`<a 1="x">`), unlike in the start tag (#40).

### Reader state
22. **A new `ReaderState` field touches six places**: the `ReaderState` field, its public
    constructor (default value) and its internal constructor (parameter); the reader's own field
    and its `ReaderState` property; and **both** reader constructors, `Utf8XmlReader.Single.cs`
    and `Utf8XmlReader.Multiple.cs`, which copy it from the state. Missing the constructor copy
    loses the field whenever a reader is reconstructed, i.e. on every piped segment (#24's
    `_documentType`, #40's spacing flag). Test helpers that build a `ReaderState` by named
    arguments need the new argument too.
23. **`Value` lives for one `Read()` only.** Step 0 clears `Value` and `ValueSequence`, so a
    later cycle sees the previous token's type but not its value (e.g. the attribute name when
    #41's guard throws after `=`, the popped name on `/>`). Anything needed across cycles must be
    stored in the reader (a `ReaderState` field, rule 22) or left out (#35, #41).

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
- **A value expected after `=` is the attribute delegate's check, not the candidate's.** In
  `<a b=1>` the `1` is an empty pad, so the candidate stays Attribute (rule 5; #27: "ordering
  within a context is the delegate's concern"). The delegate throws "attribute value missing" when
  the current token is already Attribute, **before** the spacing flag check. Bug #41 (#40's miss),
  built after #32; #32 does not check the quote. The attribute name is not in `Value` by then
  (rule 23).
- **The attribute value delegate is not built.** Until it is, no attribute is read past the
  equals non-terminal; the candidate after `=` is AttributeValue (S7) when the byte is a quote.
- **`/>`'s ElementEnd has an empty `Value`.** Exposing the popped name needs storage in the
  reader; revisit later (#35).

## 3. Conventions

- State table rows are `S0`, `S1`, … Another task's state is written `#17's S3`.
- `|` in examples marks a segment boundary.
- Bugs are written in the same format as tasks, Velocity included.
- Reuse an issue number only if it has no commits.
