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
| 2 | Skip spacing | #34 | Outside content: skip all spacing. In content: skip only spacing-only character data (next non-spacing is `<`) | continue; halt when drained or undecided |
| 3 | Content ready | #22 | `>` after an Element (S3) or an attribute's Value (S5): skip it, content ready = true | continue (never a token) |
| 4 | Empty element end | #35 | `/>` in a start tag: pop the element stack | continue (not `/`); **ElementEnd** token; halt when `/` is the last byte; throw when malformed |
| 5 | Drained? | – | Step 3 may have skipped `>` as the last byte | halt |
| 6 | Peek starting terminal | #17, #25, #36 | Fill the scratch pad with a starting terminal (+ overshoot), or leave it empty | continue; halt when the pad is incomplete and reading is not completed |
| 7 | Candidate | #27 | Scratch pad + context → one candidate non-terminal | always continue (no side effects) |
| 8 | Delegate | per candidate | Read the candidate from the reader position | token, halt (simulated rollback) or throw |

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
| S6 | Start tag | empty | Attribute | – | #40 in progress |
| S7 | Start tag | `"` or `'` | AttributeValue | – | not built |
| S8 | Content | empty, `"` or `'` | CharacterData | – | not built |
| S9 | anything else | – | None (default delegate) | – | not built |

An **empty** scratch pad means "no starting terminal starts here". It does **not** mean a name
starts here. In a start tag every non-starting-terminal byte (`a`, `1`, `=`, `/`, `>`) reaches
the Attribute candidate.

### Steps not built yet

| Step | Issue | Where | Why it matters |
|---|---|---|---|
| Skip `=` (delimiting terminals) | #32 | before peek | Until built, the `=` after an attribute name reaches the Attribute candidate and throws |
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
12. **Skip steps return `true` when they drained the segment(s)** (halting), like skip spacing
    (#27, #34).

### Markup, names, positions
13. **One malformed markup makes the document malformed.** `MarkupState` is Unknown /
    WellFormed / Malformed; Unknown means the context decides (#39).
14. **Names are ASCII only in the POC.** A non-ASCII byte throws "unsupported" (#38).
15. **A name ends at the first non-name byte** (no ending terminal), so a segment ending inside a
    name is undecided: `false`, or a token when reading is completed (#38).
16. **Element names are compared on their first 32 bytes.** A `ValueSequence` name is copied to a
    32-byte `stackalloc` before pushing (#37, #23).
17. **Line and position are 0-based internally, 1-based in exceptions.** Line position counts
    bytes, not characters (#24, bug #30).
18. **Spacing in content may be character data.** Spacing-only character data is skipped by
    default; a reader option later (#34).

### Decided, not built yet
- **Content ready S4 throws** (`>` right after an attribute name, e.g. `<a b>`), instead of
  ignoring it (2026-10-08, for #40).
- **Spacing is required before an attribute.** Skip spacing records that it skipped; how the
  check is done is still open in #40.
- **`/>`'s ElementEnd has an empty `Value`.** Exposing the popped name needs storage in the
  reader; revisit later (#35).

## 3. Conventions

- State table rows are `S0`, `S1`, … Another task's state is written `#17's S3`.
- `|` in examples marks a segment boundary.
- Bugs are written in the same format as tasks, Velocity included.
- Reuse an issue number only if it has no commits.
