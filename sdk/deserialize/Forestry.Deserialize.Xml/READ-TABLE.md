# READ-TABLE.md — `Read()` as a state × input table (DRAFT)

**Purpose:** #20's logic as an exhaustive table, so a missing case shows up as a blank or `❓` cell
*before* code is written, and each cell becomes one test row. Prose in #20 explains; this table is
the contract. Cells marked `❓Dn` are undecided — see the decision list at the bottom.

Derived only from what the reader already holds (`Depth`, `RootElement`, content-ready, current
and previous `TokenType`) — nothing else is needed to pick a row.

## States

| State | Derivation |
|---|---|
| **S0 Start** | Depth 0, no root, current = None |
| **S1 Prolog** | Depth 0, no root, current ∈ {Declaration, Comment, ProcessInstruction, DocumentType} |
| **S2 After Element** | Depth > 0, not content-ready, current = Element |
| **S3 After Attribute** | Depth > 0, not content-ready, current = Attribute |
| **S4 After attribute Value** | Depth > 0, not content-ready, current = Value, previous = Attribute |
| **S5 Empty just emitted** | Depth > 0, not content-ready, current = Value, previous ∈ {Element, Value} |
| **S6a Content opened** | content-ready, nothing emitted since `>`: current = Element, or Value with previous = Attribute |
| **S6b After content Value** | content-ready, current = Value, previous ∈ {Element, Value} |
| **S6c After child ElementEnd** | Depth > 0, content-ready, current = ElementEnd |
| **S6d After Comment/PI in content** | Depth > 0, content-ready, current ∈ {Comment, ProcessInstruction} |
| **S7 After root** | Depth 0, root seen (current ∈ {ElementEnd, Comment, ProcessInstruction}) |

Content-ready is ONE bit for the innermost open element: `Push` → false; `>` skipped → true;
`Pop` → true when `Depth > 0` afterwards (an open ancestor is always past its `>`).

## Cross-cutting rules (apply to every cell)

- **X1** `Value` is defaulted first; token type / `Push` / `Pop` change only after the value read succeeds.
- **X2 Drained at start of a Read:** last readable segment → assert state (no root → throw; `Depth != 0` → throw), otherwise `Read` returns false (end of document); not last → rollback (false).
- **X3 Too few bytes to classify** (needs k, have fewer): last readable → throw; otherwise rollback, no state changed.
  Bytes needed: `</` 2, `<N` 2, `<?` 2, `/>` 2, `<!-` 3, `<?xml␠` 6 (5 + a whitespace byte, so `<?xml-stylesheet` is a PI), `<!DOCTYPE` 9.
- **X4 Spacing skip** (drift, persists through rollback) runs at the start of Read in S0, S1, S2, S3, S4, S7. **Not** in S5 (`/>` must be contiguous). S6* → ❓D1.
- **X5 The `>` skip is a committed step:** position and content-ready move together; a later rollback must not undo either.
- **X6** Push beyond the configured max depth → throw (`XmlException`, reader option).

## Input classes

`<N` = `<` + name-start · `</` · `<!--` · `<!DOCTYPE` · `<?xml␠` · `<?` (other) · `<![` / `&` (CDATA / Reference: out of POC scope) ·
`<x` (`<` + anything else) · `/>` · `/x` (`/` + not `>`) · `>` · `=` · name-start · other byte.

## Table 1 — document level

| State | `<N` | `</` | `<!--` | `<!DOCTYPE` | `<?xml␠` | `<?` | `<![` | `<x` | other byte |
|---|---|---|---|---|---|---|---|---|---|
| **S0** | Element (root) | throw | Comment | DocumentType | Declaration | ProcessInstruction | throw | throw | throw ❓D4 (BOM) |
| **S1** | Element (root) | throw | Comment | DocumentType ❓D8 | ProcessInstruction ❓D5 | ProcessInstruction | throw | throw | throw |
| **S7** | throw (2nd root) | throw | Comment | throw | ProcessInstruction ❓D5 | ProcessInstruction | throw | throw | throw |

## Table 2 — inside a start tag

| State | name-start | `=` | `/>` | `/x` | `>` | `<` (any) | other byte |
|---|---|---|---|---|---|---|---|
| **S2** | Attribute | throw | Value (empty) ❓D3 | throw | skip, content-ready, loop | throw | throw |
| **S3** | throw | Value (attribute) | throw | throw | throw | throw | throw |
| **S4** | Attribute (needs ≥1 space before, else throw ❓D6) | throw | Value (empty) ❓D3 | throw | skip, content-ready, loop | throw | throw |
| **S5** | throw | throw | throw | throw | ElementEnd (pop; must be adjacent) | throw | throw |

## Table 3 — content (any byte except `<` and `&` is character data, including `>`, `=`, `/`)

| State | `<N` | `</` | `<!--` | `<?` | `<!DOCTYPE` / `<x` | `<![` / `&` | other byte |
|---|---|---|---|---|---|---|---|
| **S6a** | Element (push) | Value (empty), then ElementEnd | Comment | ProcessInstruction | throw | throw ❓D7 | Value (char data) ❓D1 |
| **S6b** | Element (push) | ElementEnd (name match, else throw) | Comment | ProcessInstruction | throw | throw ❓D7 | n/a — the value read consumed the whole run (assert) |
| **S6c** | Element (push) | ElementEnd | Comment | ProcessInstruction | throw | throw ❓D7 | Value (char data) ❓D1 |
| **S6d** | Element (push) | ElementEnd ❓D2 | Comment | ProcessInstruction | throw | throw ❓D7 | Value (char data) ❓D1 |

## Decisions still open

- **D1** Whitespace in content (`<a> x </a>`, or the newline + indent between siblings): skip as drift, or emit it as a `Value`? Skipping needs a lookahead to the next non-space byte.
- **D2** `<a><!--c--></a>`: does a comment/PI count as "content exists" (no empty `Value`, proposed) or not?
- **D3** Who consumes `/` and `>` of an empty element? Recommended: `Value (empty)` is zero-width, and `ElementEnd` consumes `/>` as one terminal (keeps #28's slice rule; nothing crosses reads).
- **D4** UTF-8 BOM at S0: skip 3 bytes before classification (still a TODO in code).
- **D5** `<?xml␠` when not first: PI (as #20 says now) or malformed (the spec reserves the target `xml`)?
- **D6** Attributes need whitespace between them (`<a b="1"c="2">` is malformed): enforce or be lenient?
- **D7** CDATA / Reference: a clear "unsupported" throw is proposed (accepted POC debt).
- **D8** Accepted debt, already documented elsewhere: second `<!DOCTYPE>`, comment `--`, `]]>` in char data.
