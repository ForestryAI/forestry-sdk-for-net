# BLOCKING.md — Forestry Deserialize Xml

**Purpose:** further `Utf8XmlReader` feature work (attribute reading, content reading, multi-segment)
is paused until each phase below gets a dedicated pass checking it against the real W3C XML EBNF,
one GitHub issue per phase. This file is the short, scannable gate — *why* something's wrong, once
investigated, goes in `ARCHITECTURE.md` §5/§6 as it already has all session, not here. This file
only tracks *that* a phase needs the pass and what's already known to look at when it happens.

**Reference contract:** each phase review checks its code against the high-level `Read`/`Skip`/
getter contract and the `Read`-vs-state split defined in **#28** (High level flow EBNF) — partial-
revert-with-drift on a failed `Read`, sticky `_documentNonTerminal`, everything-unreliable on
throw, position not carried in state. Settle disagreements with #28 before changing code.

**Why this exists:** repeatedly this session, aligning one piece of code's naming/structure with the
actual grammar (not just "does it build") surfaced a real, previously-unnoticed bug — the `None`→
`Prolog` transition dropped during a refactor, `_segmentPosition` never advancing past a matched
terminal, a missing dispatch case. Catching these by accident while doing unrelated work doesn't
scale, and it especially won't hold if this ever stops being solo development. Reviewing each phase
deliberately, once, against the spec is cheaper than the churn of continuing to trip over the same
class of bug piecemeal.

## Phase 1 — Prolog (`ReadPrologNonTerminal`, `ReadDeclaration`)

GitHub issue: #20 ("Prolog read, skip, value")

- Confirm the `None`→`Prolog` transition (just fixed, keyed on `readable` generally in
  `ReadPrologNonTerminal`, not declaration-specifically) is correct for all three entry paths -
  declaration first, comment/PI first, doctype first with no declaration or comment/PI before it.
- Already-known, already-accepted debt (documented in `ARCHITECTURE.md` §5, not new) - re-confirm
  still accurately described, not expanded:
  - No assertion against a second `<!DOCTYPE>` separated from the first by intervening `Misc`.
  - Declaration's starting terminal matched as the 6-byte `"<?xml "` (approximation - misses a
    tab/CR/LF-separated declaration, which is technically legal per `S`).

## Phase 2 — Element non-terminal / markup (`ReadElementNonTerminal`, `ReadEmptyElementNonTerminal`,
`ReadContentElementNonTerminal`, `ReadStartNonTerminal`, `ReadElement`, `ReadAttribute`,
`ReadEndingNonTerminal`, `ReadElementName`, `ReadName`)

**This is the least settled phase right now - expect the review to reshape it, not just patch it.**

GitHub issue: #___

- `ReadElementNonTerminal()` currently always returns `false` - `ReadEmptyElementNonTerminal()` and
  `ReadContentElementNonTerminal()` are both `return false;` stubs. **12 of 75 tests currently fail**
  because of this (confirmed by running the suite) - expected mid-refactor, not a surprise, but real.
- Structural question raised and not yet resolved: `EmptyElemTag` and `STag` share an *identical*
  grammar prefix (`'<' Name (S Attribute)* S?`, attributes included) and only diverge at the very
  last terminal (`/>` vs `>`) - so "Empty | Content" as two independent, `||`-tried non-terminal
  methods (the shape that works well for Declaration/Comment/PI/DocumentType, which *are*
  distinguishable from their first few bytes) doesn't fit this rule's shape. Needs a decision on
  where the actual branch point lives in the code, not just where the `||` is written.
- Direction being considered: collapse "Empty" and "Simple" content into one shape - every leaf
  element (self-closing, explicitly empty, or real text) becomes `Element -> Value(possibly empty)
  -> ElementEnd`; only "Complex" (child elements) differs, producing nested Element/.../ElementEnd
  instead of a `Value`. If adopted, `/>`'s single byte-pattern match then has to produce *two*
  tokens (`Value` then `ElementEnd`) across two separate `Read()` calls, which needs some form of
  "pending close" state to carry across that gap - not yet designed.
- Related open question: should `ElementNameStack` carry more than just packed names per entry
  (e.g. a pending-close flag), given the above? Raised, not resolved.
- **Concrete bug, still present, already flagged once:** `ReadStartNonTerminal()`'s (and
  `ReadElement()`'s duplicate copy of the same) inner check never advances `_segmentPosition` past
  the matched `>` before checking the next byte for `<`:
  ```csharp
  if (Utf8Reader.TryMatch(_segment[_segmentPosition..], EBNF.StopTerminal, out int lastMatchReadBytes))
  {
      if (Utf8Reader.TryMatch(_segment[_segmentPosition..], EBNF.StartTerminal, out int firstMatchReadBytes)) {
          // still checking the SAME un-advanced position - this can never be true
  ```
  Both branches under it are also still empty (`// TODO: Complex content` / `// TODO: Simple
  content`) - nothing here is reachable or built yet regardless.
- `ReadStartNonTerminal()` and `ReadElement()` currently hold two separate, overlapping copies of
  this same broken logic - same duplication pattern already resolved once for `ReadEndingTerminal`/
  `ReadEndingElement`; needs the same consolidation once the design above is settled.
- **Missing dispatch case:** the `_currentTokenType switch` that routes to `ReadElement()`/
  `ReadAttribute()` has no case for `TokenType.ElementEnd` - once a child element closes, the next
  call needs to know "I'm back inside the parent's content," and nothing currently handles that.
- `ReadAttribute()` is an unbuilt stub. When it's built, watch for two already-identified
  correctness traps: `Eq ::= S? '=' S?` means spacing is *optional* on both sides of `=`, not
  required; and `AttValue` allows either `"` or `'` as its delimiter, whichever one opens, dynamic
  per-attribute, not a fixed compile-time terminal.
- `TokenDepth`'s existing `// TODO: token type == value of an attribute` is still open - depends on
  how attribute values end up being counted once attribute reading is real.

## Phase 3 — Miscellaneous (`ReadMiscellaneousNonTerminal`)

GitHub issue: #___

- Built and tested (Comment/PI/Spacing all read end-to-end). Hasn't had the same "check it against
  the actual production" scrutiny phases 1 and 2 just got - likely a quick pass, not a deep one.
- Already-known, already-accepted debt (documented in `ARCHITECTURE.md` §5, not new): `Comment`'s
  "no `--` inside comments" well-formedness constraint isn't enforced (sequential, not something a
  per-byte character classification can express).

## Not part of this pass

Already tracked separately, not duplicated here: multi-segment support (#24), `Skip()`/
`ReaderOptions` auto-skip (noted in `ARCHITECTURE.md` §5), the 32-byte name-packing cap (#23),
`_documentPosition`'s undecided semantics.
