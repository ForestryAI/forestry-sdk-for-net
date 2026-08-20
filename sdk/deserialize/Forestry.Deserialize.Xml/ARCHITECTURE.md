# ARCHITECTURE.md — Forestry Deserialize Xml

## 1. Purpose

**Forestry.Deserialize.Xml** is the first concrete media provider for `Forestry.Deserialize`
(see that package's own [ARCHITECTURE.md](../Forestry.Deserialize/ARCHITECTURE.md)) — a general XML
reader, not a StanForD-specific one. StanForD XML from harvesting and forwarding machines is what
created the need for this package and is the real data it's developed and tested against (see
`CLAUDE.md`'s Task #110 entry), but that's the origin story, not the scope: this package supplies
the one thing the core package deliberately does not — a reader that knows how to walk actual XML
tokens — and it isn't limited to what StanForD's documents happen to use.

## 2. High-Level Architecture

**A custom, `Utf8JsonReader`-shaped tokenizer replaces `System.Xml.XmlReader`.** `XmlReader`
offers no way to ask how many raw bytes its last `Read()` actually consumed, so a caller can't
bound a synchronous parse burst to only what's already buffered — which breaks the buffer-in/
token-out contract the core package's `IBuffering`/`Deserializer<T>.TryReadNullableValue` walk
depends on (see core ARCHITECTURE.md §4.3–4.4). `Utf8XmlReader` is a `ref struct` instead: sync
only, operates over a caller-supplied span or sequence, and is reconstructed fresh from
`(segment, ReaderState)` per step — mirroring `Utf8JsonReader`/`JsonReaderState` exactly, not
inventing a new pattern. `ReaderState` is the piece that makes reconstruction actually possible: a
plain (non-ref) struct that can survive an `await` a `Utf8XmlReader` itself never could, handed
back out via the reader's own `ReaderState` property and passed into the next constructor call
(§4.3, §4.1).

**Built against StanForD's real shape first, but not permanently scoped to only what StanForD
happens to use — full XML coverage is the goal.** A real production `.hpr` export (see
`CLAUDE.md`'s Task #110 entry for the grep evidence) is what the reader is developed and tested
against: single default namespace declared once at the root, zero CDATA/entity references, zero
comments, self-closing empty elements for nulls. That sample is what keeps early development
grounded in something real rather than assumed — but `TokenType`/`Constants` already carry CDATA
and comment support ahead of any real StanForD file using them, on the position that narrow-dialect
assumptions are an optional fast path to reach for later if ever needed, not a hard boundary on
what the reader can parse. (Revised from an earlier, narrower stance — see CLAUDE.md's Task #110
entry for that history.) Namespace resolution collapsing to a constant check instead of an ancestor
prefix-scope stack remains true for now since no real sample has needed otherwise, but isn't
treated as a permanent constraint either.

**Everything genuinely async stays in the core package's buffering layer.** `Utf8XmlReader` itself
never awaits anything — refilling the segment it reads from is `PipeReaderBuffering`'s job (core
package), not this one's. This package's only responsibility is turning already-buffered bytes
into tokens, and turning a schema (`TypeDefinition`/`PropertyDefinition`) plus a stream of tokens
into `Value`s.

**A token must represent a fully-consumed syntactic unit — never merely "recognized the start
of."** This is the same principle `Utf8JsonReader.Read()` follows and it's worth stating
explicitly, because XML's syntax makes it easy to violate by accident: JSON's tokens are often one
byte, so "peek a byte, decide the token" and "fully consume the token" look identical and it's
easy to not notice they're different operations. XML needs multiple bytes of lookahead to even
identify what kind of construct is starting (`<?xml ` needs 6 bytes to tell the declaration apart
from a same-prefixed processing instruction target; `<!--` needs 4 to know it's a comment and not
`<!DOCTYPE`), which makes the distinction impossible to ignore: recognizing the *start* of a
construct and having *fully read* it are genuinely different moments, and only the second one is
allowed to be reported as a `TokenType`. See §4.2 for `TokenType`'s current shape, built to this
principle from the start (`Element` isn't reported until the name, attributes, and `>`/`/>`
resolution are all read).

**On insufficient buffered data mid-token: roll back, don't resume incrementally.** When a token
can't be completed within the currently-buffered bytes, the reader reverts to the position where
*that* token attempt started (not partial progress into it), and reports only prior, fully-complete
tokens as consumed. The retry re-scans the same token from its start once more data arrives. This
isn't a new mechanism to invent for `Utf8XmlReader` — it's making sure the reader honors what
`PipeReaderBuffering` (core package) already assumes: `Advance(0)` maps to `AdvanceTo(sequence
.Start, sequence.End)` (examined everything, consumed nothing, don't re-deliver until there's
more), and `_partialReadBytes` doubles specifically to avoid a tight retry loop when a token spans
a large chunk. `Utf8JsonReader` does the same thing for exactly the same reason.

**Does support `ReadOnlySequence<byte>` (multi-segment) input directly, via a second pair of
constructors.** Sequencing-awareness is isolated entirely to the segment-reading layer
(`IsSegmentReadable()`/`ReadNextSegment()`, `Utf8XmlReader.cs`) — `ReadDocument` and every
token-level method built under it operates on `_segment` as an ordinary `ReadOnlySpan<byte>` and
has no idea whether it came from a span directly or from walking a sequence. `Read()` is a single
method, not two — there's no parallel sequence-aware reimplementation of parsing. Combined with
piping being the actual point of the POC — chunking a large file through a `PipeReader` and moving
the reader along token by token without loading the whole thing into memory, the core value
proposition stated since this package's own README — a segment genuinely spanning multiple pieces
is normal, expected behavior once a meaningful amount of streamed data has accumulated, not a rare
edge case worth deferring to rollback-and-retry.

## 3. Module Boundaries

- **`Forestry.Deserialize.Xml`** — this package. Depends on `Forestry.Deserialize` for every
  contract it implements against (`Deserializer<T>`, `IReaderState<TState>`, `IBuffering`,
  `ReaderPath`/`ReaderPosition`, `DeserializeOptions`). Defines nothing that a JSON or other future
  media package would need to share — anything genuinely media-agnostic belongs in the core
  package, not here.
- **`Forestry.Deserialize`** (sibling, referenced via `ProjectReference`) — the media-agnostic
  walk contract this package plugs into. See its own ARCHITECTURE.md for the shape this package is
  implementing against.

**Stability:** Experimental — the package builds clean today, but most types are still partial
skeletons (§5).
**Dependencies:** `Forestry.Deserialize` only.

## 4. Core Contracts

### 4.1 `Utf8XmlReader` (`src/Reading/Utf8XmlReader.cs`)

A `ref partial struct`, sync-only tokenizer over a caller-supplied byte span or sequence — the XML
analogue of `Utf8JsonReader`. `Read()` advances to the next token and returns whether it did;
`GetString()`/`GetUnescapedValue()` read the current token's raw content. Being a `ref struct`, it
can never be held across an `await` or stored in a class field — a fresh instance is constructed
per step from `(segment, ReaderState)`, exactly like `Utf8JsonReader`/`JsonReaderState` (see core
ARCHITECTURE.md §4.4).

**Fields split into three regions matching what does and doesn't survive reconstruction:**
`segments` and `sequence` are reader-local — the current buffer, its position, and (when backed by
a `ReadOnlySequence<byte>`) the sequence-walking machinery — none of it appears in `ReaderState`,
because a fresh segment/sequence is handed to every constructor call regardless of what came
before. `state` is everything that *does* need to survive — line/position, the `EBNF.Document`
phase, current/previous `TokenType`, the element name tracking, and `ReaderOptions` — mirroring
`ReaderState`'s own `debug`/`assertions`/`options` regions field-for-field (§4.3). `_documentPosition`
sits outside both groupings; a `// TODO` marks that its reset-to-zero-per-reconstruction behavior
(offset within the *current* segment, not an absolute document-wide position) is a known open
question, not a settled design choice.

**Four public constructors**, matching `Utf8JsonReader`'s own constructor set:
- `Utf8XmlReader(ReadOnlySpan<byte> segment, ReaderOptions readerOptions = default)` — whole
  document in memory. `isFinalSegment` is hardcoded `true` and state starts fresh; there is no
  reconstruction path from this overload.
- `Utf8XmlReader(ReadOnlySpan<byte> segment, bool isFinalSegment, ReaderState readerState)` —
  manual buffer management. The caller owns a reusable buffer, refills it themselves, and must pass
  back exactly the `ReaderState` the previous instance ended holding (via its `ReaderState`
  property) on every call after the first. `isFinalSegment: true` means this segment is the last
  bytes that will *ever* be supplied — not that the segment is merely full or exhausted.
- `Utf8XmlReader(ReadOnlySequence<byte> segments, ReaderOptions readerOptions = default)` — whole
  document, already sequence-shaped (e.g. built from several pooled buffers). Same "entire
  document, nothing more ever" contract as the span convenience overload.
- `Utf8XmlReader(ReadOnlySequence<byte> segments, bool isFinalSegment, ReaderState readerState)` —
  the real piping path. `segments` is typically a `PipeReader`'s current `ReadResult.Buffer`, which
  may already span multiple unconsumed pieces. The reader walks forward across those internally via
  `ReadNextSegment()` without needing reconstruction, until the sequence itself runs out. Two
  construction-time behaviors specific to this overload: any empty leading segments are skipped
  automatically before `_segment` is set, and `isFinalSegment`'s recomputation only happens when
  `segments` has more than one piece — for a single-segment sequence, the caller's flag is taken at
  face value, same as the span path.

**`isFinalSegment` (parameter) vs. `_isFinalSegment` (field) vs. `_isExternalFinalSegment` (field).**
The caller's raw flag is stored verbatim as `_isExternalFinalSegment` and re-consulted every time
`ReadNextSegment()` walks forward. `_isFinalSegment` is never set directly from the caller — it's
always derived (trivially, for a span; by conjunction with "is there a next segment already
present" for a sequence) and it's what `IsSegmentReadable()` actually gates on. Naming them
differently (rather than both being "segment"-flavored, or both "buffering"-flavored) is
deliberate: it keeps the caller-supplied input and the reader's own re-derived fact visually
distinct at every call site.

**`ReaderState` property** builds a fresh `ReaderState` from every live field in the `state`
region — the `Utf8JsonReader.CurrentState`-equivalent snapshot a consumer reads after a `Read()`
burst and carries into the next reconstruction's constructor call. Named after its own return type
rather than `CurrentState` — a deliberate departure from the `Utf8JsonReader` naming precedent
this package otherwise mirrors, on the view that it's more literal about what it returns.

**`Skip()` was removed**, along with the entire pre-redesign dispatch chain (`ReadProlog`,
`ReadDeclaration`, `ReadElement`, `ReadElementEnd`, `ReadMiscellaneous`, `ReadComment`,
`ReadProcessInstruction`) — none of it held real logic, and `Read()` now calls straight into
`ReadDocument()` (currently an empty stub, §5) rather than dispatching through a token-type switch
that was itself scaffolding with nothing behind it. `SkipWhitespace` is a known, acknowledged
leftover stub, not yet cleaned up.

### 4.2 `TokenType` (`src/TokenType.cs`), `EBNF` (`src/EBNF.cs`), `Constants` (`src/Constants.cs`), `Syntax` (`src/Syntax.cs`)

`TokenType` is the set of token kinds `Utf8XmlReader.Read()` advances between, grouped into regions
matching the grammar: `None`; `markup` — `Element`, `ElementEnd`, `Attribute`, `Value`; `prolog` —
`Declaration`, `DocumentType`; `miscellaneous` — `ProcessInstruction`, `Comment`. Every value means
"fully consumed," per §2's principle. `Attribute`/`Value` are shared shapes, not doubled per
context (an attribute's name vs. an element's name both report as the token that names them; an
attribute's value vs. an element's text content both report as `Value`) — the consumer disambiguates
via `_previousTokenType` (`Attribute` immediately before means this `Value` belongs to it;
`Element` immediately before means element content) rather than a dedicated context field, which is
why no `_isAttribute` field exists anywhere in `ReaderState`/`Utf8XmlReader`.

**There is no `TokenType.Null`.** `<Name/>` and `<Name></Name>` are syntactically different but
both represent "no content" identically — an `Element` token immediately followed by `ElementEnd`,
with no `Value` token in between. Whether "no content" means C# `null` or an empty string is a
schema-aware decision (the target property's nullability), made by the deserializer, not the
tokenizer — consistent with this package staying a general XML reader rather than baking a
StanForD-specific convention (self-closing-means-null) into the token vocabulary every consumer
has to share. `Constants.NullAttributeName` (`xsi:nil`) still exists as unused vocabulary from
before this decision — worth revisiting whether it belongs in `Constants` at all now that null
detection isn't the reader's concern (§5).

`EBNF` (`src/EBNF.cs`) is a `static partial class` wrapping the grammar's non-terminals as nested
enums — currently just `EBNF.Document: byte { None, Prolog, Element, Miscellaneous }`, tracking
`document ::= prolog element Misc*`'s three sequential, mutually-exclusive phases. Deliberately a
flat enum, not a hierarchical stack: the three phases never nest into each other (once you leave
`Prolog` you never return to it), so a linear state machine models it exactly. The one grammar rule
phase alone can't enforce — `doctypedecl` occurring at most once inside `prolog`, non-adjacent to
itself since `Misc` can appear between — is flagged as needing a small dedicated bit of state (not
yet built), not a reason to restructure `EBNF.Document` into something bigger. The `partial`
modifier leaves room for other non-terminals to get their own nested enum later without needing a
new top-level type each time. Recursive nesting (arbitrary element depth) is deliberately *not*
handled here — that's `ElementNameStack`'s job (§4.3) — because sequential phase-tracking and
recursive depth-tracking are different kinds of complexity that don't need to share one structure.

`Constants` holds the raw UTF-8 byte-level vocabulary `Utf8XmlReader` scans against: markup
delimiters (`<`, `>`, `&`, `;`), tag-internal delimiters (`/`, `=`, `"`, `?`, space, tab/CR/LF),
the UTF-8 BOM, all 5 predefined XML entities, CDATA/comment/declaration/DOCTYPE delimiters, and the
ASCII byte set for `:`/`_`/`-`/`.`. Per §2, CDATA doesn't occur in the real StanForD sample used for
development and is unverified against real data as of this writing — it's there for eventual
general-XML coverage, not because StanForD needs it today.

`Syntax` holds the grammar-level predicates built from those bytes — `IsNameStartingCharacter`/
`IsNameCharacter` (`NameStartChar`/`NameChar`) and `IsCommentCharacter` (`Char`, as referenced by
`Comment`'s content) — split out from `Constants` so raw vocabulary and grammar rules built from it
are separate concerns. Each has a `<see>` link to its exact W3C production and, deliberately, a
doc comment stating precisely what's checked exactly against the spec versus approximated: the
ASCII alternatives in each production are checked byte-for-byte, but everything above ASCII
(`value >= 0x80`) is a coarse "any non-ASCII UTF-8 byte" pass-through, not a decoded Unicode
codepoint range-check against the production's full range list — a real gap from the literal
spec (documented rather than silently approximated), not yet closed. `IsCommentCharacter` also
does not and cannot enforce `Comment`'s "no `--` anywhere in content" constraint — that's
sequential (needs the previous byte), not a per-byte classification; the caller's scan has to
track it.

### 4.3 `ReaderState` (`src/Reading/ReaderState.cs`)

The storable, non-`ref` continuation state for `Utf8XmlReader` — a `readonly struct` implementing
the core package's `IReaderState<ReaderState>` (see core ARCHITECTURE.md §4.4), meant to live
across async/sync boundaries the reader itself can't cross. Fields split into three regions, and
"private fields to the state are the state machine in its entirety" — anything needed to faithfully
resume a reader lives in one of them; nothing about resumption depends on a field that lives only
on `Utf8XmlReader`:

- **`debug`** — `_lineNumber`/`_linePosition`. Help diagnose an exception; otherwise no operative
  function. These are also the *only* fields on the core `IReaderState<TState>` interface — a
  deliberate choice, not a gap: every other field here is XML-specific and reached only through the
  concrete `ReaderState` type, never through the media-agnostic interface.
- **`assertions`** — `_documentNonTerminal` (`EBNF.Document`), `_currentTokenType`/
  `_previousTokenType`, `_elementName`, `_elementNameStack`. Named for their role: validation
  propagates downward through subsequent method calls from `Read()`, each of which must resolve to
  a `bool` or a thrown exception — never silently swallow an inconsistency. `_elementName` is
  `ulong[]` (owned, packed name bytes), not a `ReadOnlySpan<byte>` — a `ReadOnlySpan<byte>` is
  itself a `ref struct` and can only live inside another `ref struct`, which `ReaderState`
  deliberately isn't (that's the whole reason it can survive an `await`), and even setting the
  compile error aside, a span would reference the caller's buffer rather than owning its own copy,
  which wouldn't survive being handed a different buffer on reconstruction.
- **`options`** — `_readerOptions` (`ReaderOptions`: currently just `MaxDepth`, defaulting to 64 to
  match `JsonReaderOptions`'s own default; `MaxDepth == 0` is treated as "caller didn't set one,"
  not a literal zero-depth limit).

**`ElementNameStack`** tracks XML's Element Type Match well-formedness constraint (end-tag name
must match its start-tag's — a WFC, not enforceable at the grammar level alone) without pushing
one entry per element: most StanForD elements are leaves with no children, so the
most-recently-opened element's name is meant to live in a single cheap slot (`_elementName`) by
default, only *promoted* onto a real, geometrically-grown stack the moment another `Element` token
(not a `Value`) is seen — i.e. exactly when an element turns out to have a child. The stack becoming
empty after a pop is also meant to double as the signal `ReadDocument` needs to transition from the
`Element` phase to trailing `Miscellaneous`. **None of this is built yet** — `ElementNameStack` is
still a genuinely empty struct (§5); this section describes the intended design, not current
behavior.

`ReaderState`'s own `ReaderState` constructor (the parameterless-except-`ReaderOptions` one) is
what a first-ever construction uses; the internal 8-field constructor is what `Utf8XmlReader`'s
`ReaderState` property (§4.1) calls to snapshot a live reader — the only caller of that constructor
today.

### 4.4 `DeserializeXmlOptions` (`src/DeserializeXmlOptions.cs`)

The `DeserializeOptions` subclass this media plugs into the core package through (see core
ARCHITECTURE.md §4.5): reflective `TypeDefinition`/`PropertyDefinition` instantiators, an
`IDeserializerProvider`, and `CreateReaderState<TState>`. `Default` is the ready-made instance
consumers are expected to reach for. Every member is currently a stub (§5).

### 4.5 `ObjectDeserializer<T>` and `Deserializers/Value/BooleanDeserializer`

`ObjectDeserializer<T>` (`src/Deserializers/ObjectDeserializer.cs`) is the XML-specific
`Deserializer<T>` for object-shaped types — where `TryReadNullableValue` will actually walk
`Utf8XmlReader` tokens against `ReaderPath`/`ReaderPosition`, matching elements/attributes to
`PropertyDefinition`s and marking them read (see core ARCHITECTURE.md §4.3 for why that walk can
only live here, not in the core package). `GetDeserializerKind` is real (`DeserializerKind
.Object`); `TryReadNullableValue` itself is still a stub returning `Partial` unconditionally.

`BooleanDeserializer` (`src/Deserializers/Value/BooleanDeserializer.cs`) is the first Value-kind
XML deserializer — the pattern any leaf scalar type (numbers, strings, dates) will follow.
`TryReadNullableValue` is currently an explicit `throw new NotImplementedException()`.
Enumerable-kind and Dictionary-kind XML deserializers don't exist yet.

### 4.6 `Deserialization.Property.cs`

`PositionPropertyDefinition` (renamed from an earlier `GetPropertyDefinition` — the new name
reflects that it does more than look up: it also advances `readerPath.Position.PropertyIndex` and
records `PropertyUtf8Name`) resolves a raw property name read off the wire to a
`PropertyDefinition` via the core's `TypeDefinition.GetPropertyDefinition`, falling back to
`PropertyDefinition._Empty` on a miss (`// TODO: Potential dictionary extension support` marks the
open question of what a miss should really mean). `GetPropertyName` pulls the raw name out of a
`Utf8XmlReader` via `GetUnescapedValue()`.

## 5. Status / POC Debt

- **The package builds clean** (both TFMs, `Forestry.Deserialize.Xml.slnx`), but most of the
  actual tokenizing/walking logic is still stub:
  - `Utf8XmlReader.ReadDocument()` is an unconditional stub (`return false`) — no real tokenizing
    happens yet, despite `TokenType`/`EBNF`/`Constants` now carrying the real vocabulary to
    tokenize against, and `Read()`/the constructors/segment-reading machinery being real. `Skip()`
    no longer exists at all (removed, §4.1) rather than being a no-op stub. `GetUnescapedValue()`
    doesn't yet decode entities (`// TODO: When escaped convert`).
  - `DeserializeXmlOptions`'s three `internal abstract` overrides and `CreateReaderState<TState>`
    are all explicit `throw new NotImplementedException()` bodies.
  - `ObjectDeserializer<T>.TryReadNullableValue` always returns `Partial`
    (`// TODO: Reader get next property`); `BooleanDeserializer.TryReadNullableValue` throws.
  - `ElementNameStack` is a genuinely empty struct — no fields at all (§4.3). Nothing backs the
    lazy single-slot/promoted-stack design it's meant to implement yet.
  - `ReaderState` implements the full `IReaderState<ReaderState>` shape and now round-trips real
    values via `Utf8XmlReader`'s `ReaderState` property, but since `ReadDocument()` never actually
    advances anything yet, nothing populates it with real, non-default values from an actual read.
- **`_documentPosition`'s semantics are marked `// TODO`, not settled.** It resets to `0` on every
  reconstruction along with everything else in the `segments` region — meaning it currently means
  "offset within the current segment," not "offset within the whole document," and nothing carries
  it forward via `ReaderState` the way `_lineNumber`/`_linePosition` do. Whether it should is an
  open question, not yet decided.
- **`Constants.NullAttributeName` (`xsi:nil`) is now vocabulary without a clear purpose.** It
  predates the decision to drop `TokenType.Null` (§4.2) — null detection is no longer a reader-level
  concern, so whether `xsi:nil` recognition belongs in `Constants` at all (versus being something a
  schema-aware deserializer checks for itself, if ever needed) hasn't been revisited since that
  decision landed. The CDATA delimiters remain a deliberate, unrelated gap: general-XML coverage
  ahead of StanForD actually needing it, not a bug.
- **No `Enumerable`/`Dictionary`-kind XML deserializer exists** — only `Object` (Object-kind) and
  `Boolean` (Value-kind, the first of what leaf scalar types will follow) have files.
  `DeserializerFactory`/`IDeserializerProvider` wiring for any of them is unbuilt.
- **No test project content** — `Forestry.Deserialize.Xml.Tests` exists as a `.csproj` shell only
  (no test files under it yet).
- **`SkipWhitespace(ReadOnlySpan<byte> value)` is a known, acknowledged leftover stub** — not
  wired to anything, not yet cleaned up.
- **Multi-segment support exists (§2) but is effectively untestable right now, for two independent
  reasons.** `Forestry.Deserialize.Xml.Tests` has `InternalsVisibleTo` access, but `_segment`/
  `_segmentPosition`/`ReadNextSegment()` are `private`, not `internal` — cross-assembly visibility
  doesn't reach `private` members, so the test project still can't drive or observe segment
  transitions directly. Separately, nothing currently *causes* a segment transition through the
  public `Read()` API anyway: `ReadDocument()` is still a stub body that never advances
  `_segmentPosition`, so there's no way to exercise `ReadNextSegment()` end-to-end yet even with the
  right visibility. Real verification has to wait for real token-reading logic, or a deliberate
  visibility change if earlier, isolated testing of the segment-reading layer alone is wanted before
  that lands.

## 6. Stability & Volatility Map

| Module | Stability | Notes |
|---|---|---|
| `Utf8XmlReader` | **Started** | `ref struct` shape, real span/`ReadOnlySequence<byte>` dual-constructor buffering, and a working `ReaderState` round-trip all in place (§4.1); `ReadDocument()`/token-level reading still a stub |
| `TokenType` | **Settled** | 9 real token kinds, grouped by grammar region; no `Null` (§4.2), deliberately |
| `EBNF` | **Settled** | `EBNF.Document` covers the 3 sequential top-level phases; extensible via `partial` for future non-terminals |
| `Constants` | **Started** | Byte-level vocabulary defined; `xsi:nil` now unused pending revisit, CDATA unverified against real data (§5) |
| `ReaderState` | **Shape settled** | Full `IReaderState<TState>` implemented, real round-trip via `Utf8XmlReader.ReaderState`; nothing populates non-default values from an actual read yet |
| `ElementNameStack` | **Not begun** | Empty struct; lazy single-slot/promoted-stack design decided (§4.3), unbuilt |
| `DeserializeXmlOptions` | **Unstarted** | Every override throws |
| `ObjectDeserializer<T>` | **Started** | `GetDeserializerKind` real; `TryReadNullableValue` still a stub |
| `BooleanDeserializer` | **Started** | First Value-kind deserializer; `TryReadNullableValue` throws |
| `PositionPropertyDefinition`/`GetPropertyName` | **Started** | Real lookup + `ReaderPath.Position` bookkeeping wired |
| Enumerable/Dictionary deserializers | **Not begun** | No files exist |

## 7. Anti-Goals

- **Revised** (see §2) — this package no longer treats general-XML constructs as permanently out
  of scope. CDATA, predefined entity decoding, and comments are being built as real, if optional,
  capabilities rather than excluded until a StanForD sample happens to need them. What remains a
  genuine anti-goal: DTD processing and external entity resolution (a real security concern for any
  XML parser, not just a scoping convenience) and XInclude — general-XML features with no StanForD
  relevance and real complexity/attack-surface cost, not planned unless something concrete requires
  them.
- **Null detection is not a reader-level concern** (§4.2) — `Utf8XmlReader` reports what's
  syntactically there (an `Element`/`ElementEnd` pair with no `Value` between them); whether that
  means C# `null` or an empty string is left entirely to the schema-aware deserializer layer.
- It does not reimplement anything the core `Forestry.Deserialize` package already owns — the
  walk contract, `Value`/`TypeDefinition` shapes, and buffering all come from there; this package
  only supplies the XML-specific reader and `Deserializer<T>` subclasses.
- It does not target `Forestry.StanForD`'s eventual real schema classes directly — this package is
  media plumbing; the real ingestion project (Feature #105) is a consumer of it, not part of it.

## 8. Related Work Items

- Feature #105 — Chat POC external
  - User Story #106 — Deserialize
    - Task #110 — Review current code base / core plumbing (see `CLAUDE.md` for the design
      history this document summarizes, including the real `.hpr` sample analysis behind §2's
      dialect scoping)
- Feature #12 — Reader architecture (this document's current focus)
  - Task #13 — Reader state — `EBNF.Document` phase tracking, the `debug`/`assertions`/`options`
    field split, `_elementName`/`ElementNameStack`'s lazy-promotion design (§4.3)
  - Task #14 — Reader constructor — the four constructors' span vs. sequence contracts,
    `isFinalSegment`'s external/internal split (§4.1)
  - Task #15 — Read token — `TokenType`'s current shape, the `Null` token's removal in favor of
    deserialization-owned nullability (§4.2)

## 9. Attribution

`Utf8XmlReader`'s shape — a `ref struct` tokenizer over a caller-supplied buffer, reconstructed
fresh from `(segment, state)` per step rather than held across an `await` — deliberately mirrors
`System.Text.Json.Utf8JsonReader`/`JsonReaderState` (§2, §4.1; see core ARCHITECTURE.md §4.4 for
why). That's Microsoft's open-source .NET runtime (`dotnet/runtime`, MIT licensed), and design
patterns and, in places, specific logic are adapted from it throughout this package. See
[/THIRD-PARTY-NOTICES.md](../../../THIRD-PARTY-NOTICES.md) for the license text and the inline
attribution comment convention used where a specific method is a direct adaptation rather than
just a shared shape.
