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
only, operates over a caller-supplied span, and is reconstructed fresh from `(buffer, state)` per
step — mirroring `Utf8JsonReader`/`JsonReaderState` exactly, not inventing a new pattern.

**Built against StanForD's real shape first, but not permanently scoped to only what StanForD
happens to use — full XML coverage is the goal.** A real production `.hpr` export (see
`CLAUDE.md`'s Task #110 entry for the grep evidence) is what the reader is developed and tested
against: single default namespace declared once at the root, zero CDATA/entity references, zero
comments, self-closing empty elements for nulls. That sample is what keeps early development
grounded in something real rather than assumed — but `TokenType`/`Constants` already carry CDATA,
comment, and `xsi:nil` support ahead of any real StanForD file using them, on the position that
narrow-dialect assumptions are an optional fast path to reach for later if ever needed, not a hard
boundary on what the reader can parse. (Revised from an earlier, narrower stance — see CLAUDE.md's
Task #110 entry for that history.) Namespace resolution collapsing to a constant check instead of
an ancestor prefix-scope stack remains true for now since no real sample has needed otherwise, but
isn't treated as a permanent constraint either.

**Everything genuinely async stays in the core package's buffering layer.** `Utf8XmlReader` itself
never awaits anything — refilling the span it reads from is `PipeReaderBuffering`'s job (core
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
allowed to be reported as a `TokenType`. See §4.7 for what this means concretely for `TokenType`'s
shape (still to be built — the current enum in §4.2 predates this principle being made explicit).

**On insufficient buffered data mid-token: roll back, don't resume incrementally.** When a token
can't be completed within the currently-buffered bytes, the reader reverts to the position where
*that* token attempt started (not partial progress into it), and reports only prior, fully-complete
tokens as consumed. The retry re-scans the same token from its start once more data arrives. This
isn't a new mechanism to invent for `Utf8XmlReader` — it's making sure the reader honors what
`PipeReaderBuffering` (core package) already assumes: `Advance(0)` maps to `AdvanceTo(sequence
.Start, sequence.End)` (examined everything, consumed nothing, don't re-deliver until there's
more), and `_partialReadBytes` doubles specifically to avoid a tight retry loop when a token spans
a large chunk. `Utf8JsonReader` does the same thing for exactly the same reason.

**Does support `ReadOnlySequence<byte>` (multi-segment) input directly, via a second constructor
— reverses an earlier call recorded in this section.** The original reasoning was that mirroring
`Utf8JsonReader`'s dual single-segment/multi-segment code paths wasn't worth it for a POC, on the
assumption that doing so meant duplicating scanning logic the way `Utf8JsonReader` itself does.
That assumption turned out not to hold for how this got built: sequencing-awareness is isolated
entirely to the buffering layer (`IsBufferReadable()`/`ReadNextSegment()`, `Utf8XmlReader.cs`) —
`ReadDocument`/`ReadProlog`/every token-level method downstream operates on `_buffer` as an
ordinary `ReadOnlySpan<byte>` and has no idea whether it came from a span or a sequence. `TryRead()`
is a single method, not two — there's no parallel `TryReadWithSequencing()` reimplementing parsing.
So the actual cost paid is a handful of extra fields and one buffer-refill method, not duplicated
scanning logic throughout. Combined with piping being the actual point of the POC — chunking a
large file through a `PipeReader` and moving the reader along token by token without loading the
whole thing into memory, the core value proposition stated since this package's own README — a
buffer genuinely spanning multiple segments is normal, expected behavior once a meaningful amount
of streamed data has accumulated, not a rare edge case worth deferring to rollback-and-retry.
Treating it as first-class was the right call once the actual implementation cost was known, not
assumed.

`Utf8XmlReader.IsWithSequencing`/`ValueSequence`/`TryReadWithSequencing` (the earlier, since-removed
placeholder surface this section previously flagged as a cleanup candidate) has been replaced
entirely by the real implementation above — nothing left to remove; that item is resolved, not
just relocated.

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

A `ref partial struct`, sync-only tokenizer over a caller-supplied buffer — the XML analogue of
`Utf8JsonReader`. `Read()` advances to the next element/attribute; `Skip()` skips the current one;
`GetString()` reads the current token as a string. Being a `ref struct`, it can never be held
across an `await` or stored in a class field — a fresh instance is constructed per step from
`(buffer, ReaderState)`, exactly like `Utf8JsonReader`/`JsonReaderState` (see core ARCHITECTURE.md
§4.4).

### 4.2 `TokenType` (`src/TokenType.cs`), `Constants` (`src/Constants.cs`), `Syntax` (`src/Syntax.cs`)

`TokenType` is the set of token kinds `Utf8XmlReader.Read()` advances between — current, as-built
shape: `None`, `StartingTag`, `EndingTag`, `EmptyTag`, `ElementName`, `ElementValue`,
`AttributeName`, `AttributeValue`, `Declaration`, `ProcessInstruction`, `Comment`, `CharacterData`,
`Null`. **This shape predates §2's "token = fully-consumed unit" principle and is superseded by the
redesign in §4.7 (not yet built)** — several of these values (`StartingTag` in particular) report
"recognized the start of," not "fully read," which §4.7 replaces.

`Constants` holds the raw UTF-8 byte-level vocabulary `Utf8XmlReader` scans against: markup
delimiters (`<`, `>`, `&`, `;`), tag-internal delimiters (`/`, `=`, `"`, `?`, space, tab/CR/LF),
the UTF-8 BOM, all 5 predefined XML entities, CDATA/comment/declaration/DOCTYPE delimiters, an
`xsi:nil` attribute-name constant for null detection, and the ASCII byte set for `:`/`_`/`-`/`.`.
Per §2, some of these (CDATA, `xsi:nil`) don't occur in the real StanForD sample used for
development and are unverified against real data as of this writing — they're there for eventual
general-XML coverage, not because StanForD needs them today.

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

The storable, non-ref continuation state for `Utf8XmlReader`, implementing the core package's
`IReaderState<ReaderState>` (see core ARCHITECTURE.md §4.4) — this is what `DeserializeXmlOptions
.CreateReaderState<TState>` is meant to produce and what a fresh `Utf8XmlReader` is reconstructed
from between steps. Currently backs the 4 core interface members (via explicit interface
implementation — see the comment on `ReaderState.cs` for why: `IReaderState<TState>`'s members are
`internal`, which can only be satisfied by a `public` implementing member or an explicit
implementation, never implicitly by an `internal` one, even across the `InternalsVisibleTo` friend
assembly) plus `TokenType`, as a plain immutable data holder — nothing populates real values into
one yet (§5). §4.7 plans a local field rename (`_isObject`→`_isElement`, XML-side only — the core
interface member name doesn't change) plus a new `_isAttribute` field, not yet built.

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

### 4.7 Planned: `TokenType` redesign + the open-element-name boundary (decided, not yet built)

Settled in design discussion; none of this is in code yet (`TokenType.cs`/`ReaderState.cs`/
`Utf8XmlReader.cs` are all still the §4.2/§4.3 as-built shape as of this writing). Recorded here
specifically so it isn't lost before implementation catches up.

**New `TokenType` set: `Element`, `ElementEnd`, `Attribute`, `Value`** (replacing `StartingTag`/
`EndingTag`/`EmptyTag`/`ElementName`/`ElementValue`/`AttributeName`/`AttributeValue`) —
`Declaration`/`ProcessInstruction`/`Comment`/`CharacterData`/`Null` are unaffected. Two changes
bundled together:
- Each remaining value now means "fully consumed," per §2's principle — `Element` isn't reported
  until the name, all attributes, and the `>`/`/>` resolution are all read, not the instant `<` +
  a name-start byte is seen.
- `AttributeName`/`AttributeValue` and `ElementName`/`ElementValue` collapse into shared
  `Attribute`/`Value` — not different token *shapes*, just the same shape in two contexts,
  disambiguated by new context fields rather than doubled enum values (next point).

**New context fields on `Utf8XmlReader`/`ReaderState`: `_isElement` (renamed from `_isObject`)
and `_isAttribute`.** `_isObject` is not a free local field — it satisfies the core package's
`IReaderState<TState>._isObject`, a media-agnostic interface member that has to stay generically
named (a hypothetical JSON reader's "object" is the right word there). The rename is XML-side
only: the *local* backing field becomes `_isElement` (XML's own vocabulary for "positioned within
a non-leaf construct"), still satisfying `IReaderState<ReaderState>._isObject` through the same
explicit-interface-implementation forwarder already in `ReaderState.cs`. `_isAttribute` is new,
needed specifically because `Value` is now shared between element content and attribute values —
something has to say which context a given `Value` token came from.

**WFC: Element Type Match (`https://www.w3.org/TR/xml/#dt-etag`) is enforced inside `Utf8XmlReader`/
`ReaderState`, not lifted to the walk layer.** The spec requires an end-tag's name to match its
start-tag's — `ETag ::= '</' Name S? '>'` doesn't constrain this at the grammar level, it's a
separate well-formedness constraint, so an `ElementEnd` token needs the currently-open element
name(s) to validate against. **Reverses an earlier call recorded in this same section**: the
original reasoning was that `ReaderState` (a `readonly struct` with fixed-size fields,
reconstructed fresh every step) can't cheaply hold a variable-depth stack, so end-tag-name tracking
should live in the deserialization/walk layer instead. That objection turned out to be weaker than
it looked — a struct can hold a reference-type field (an array) fine without losing its own
cheap-to-copy nature (`PipeReaderBuffering`, core package, already does exactly this), so
`ReaderState` holding a name stack isn't actually blocked. Two things made reader-ownership the
better fit once that was clear, not just a viable alternative:
- **A pragmatic, lazy-push stack, not one push per element.** Most elements in real StanForD data
  are leaves (text content, no children) — nothing can nest between a leaf's open and its own
  close, so a leaf's name never needs to coexist with another open name and never needs the stack
  at all. The most-recently-opened element's name is kept in a single cheap slot by default;
  it's only *promoted* onto the real (geometrically-grown, not `Array.Resize`-per-character) stack
  at the moment another `Element` token — not a `Value` — is seen, i.e. exactly when it's
  discovered to have a child. A leaf's `ElementEnd` matches directly against the held slot, stack
  untouched. Against the Task #110 sample's shape (278 `<Log>` records, ~8 `<LogDiameter>` leaf
  children each), this is one push per `Log`, not nine.
- **The stack becoming empty after a pop is also exactly the `_isElement`→trailing-`Misc` phase
  transition signal `ReadDocument` needs.** `document ::= prolog element Misc*` requires the reader
  to know precisely when the one mandatory `element` is fully done so it can start reading `Misc*`.
  If the stack lived in the walk layer, the reader would still need its own separate way to detect
  that moment — probably a redundant depth counter duplicating information the walk layer's stack
  already has but the reader can't see. With the stack in the reader, "just popped back to empty"
  *is* that signal, for free, from the same structure that does WFC validation - not a second
  mechanism.

`ReaderPath` (core package) is unaffected by this and remains conceptually separate: it tracks
position in the *schema*/`TypeDefinition` graph, not raw XML tag names — the reversal changes
*where* the name stack lives, not whether it's the same thing as `ReaderPath` (it still isn't).

## 5. Status / POC Debt

- **The package builds clean** (both TFMs, `Forestry.Deserialize.Xml.slnx`), but most of the
  actual tokenizing/walking logic is still stub:
  - `Utf8XmlReader.Read()`/`Skip()` are unconditional stubs (`return false`/no-op) — no real
    tokenizing happens yet, despite `TokenType`/`Constants` now carrying the real vocabulary to
    tokenize against. `GetString()` only distinguishes `Null` from everything else (returns
    `string.Empty` for any other token). `GetUnescapedValue()` doesn't yet decode entities
    (`// TODO: When escaped convert`).
  - `DeserializeXmlOptions`'s three `internal abstract` overrides and `CreateReaderState<TState>`
    are all explicit `throw new NotImplementedException()` bodies.
  - `ObjectDeserializer<T>.TryReadNullableValue` always returns `Partial`
    (`// TODO: Reader get next property`); `BooleanDeserializer.TryReadNullableValue` throws.
  - `ReaderState` implements the full `IReaderState<ReaderState>` shape but nothing populates real
    values into one from an actual read yet.
- **`Constants.NullAttributeName` (`xsi:nil`) and the CDATA delimiters are unverified against real
  StanForD data** — the Task #110 sample has zero occurrences of either (confirmed by grep against
  the same file again this round). Per §2 this is an intentional, accepted gap — general-XML
  coverage ahead of StanForD actually needing it — not a bug, but worth remembering when a real
  StanForD file's null representation (a bare self-closing empty element, no `xsi:nil` attribute)
  needs to actually work end to end.
- **`Utf8XmlReader.cs` declares its `ref partial struct` with no enclosing namespace** — it sits in
  the global namespace, inconsistent with every other type in this package (`Forestry.Deserialize
  .Xml.Reading`). Still unfixed.
- **No `Enumerable`/`Dictionary`-kind XML deserializer exists** — only `Object` (Object-kind) and
  `Boolean` (Value-kind, the first of what leaf scalar types will follow) have files.
  `DeserializerFactory`/`IDeserializerProvider` wiring for any of them is unbuilt.
- **No test project content** — `Forestry.Deserialize.Xml.Tests` exists as a `.csproj` shell only
  (no test files under it yet).
- **§4.7's `TokenType`/`ReaderState` redesign is decided but unbuilt** — the current `TokenType`/
  `ReaderState`/`Utf8XmlReader` shapes described in §4.2/§4.3 still reflect the pre-redesign
  version. Next real implementation step for the reader.
- **Multi-segment support exists (§2) but is effectively untestable right now, for two independent
  reasons.** `Forestry.Deserialize.Xml.Tests` now has `InternalsVisibleTo` access (added this
  round), but `_buffer`/`_bufferPosition`/`ReadNextSegment()` are `private`, not `internal` —
  cross-assembly visibility doesn't reach `private` members, so the test project still can't drive
  or observe segment transitions directly. Separately, nothing currently *causes* a segment
  transition through the public `Read()` API anyway: `ReadDocument`/`ReadProlog`/etc. are all still
  stub bodies that never advance `_bufferPosition`, so there's no way to exercise `ReadNextSegment()`
  end-to-end yet even with the right visibility. Real verification has to wait for real
  token-reading logic (§4.7), or a deliberate visibility change if earlier, isolated testing of the
  buffering layer alone is wanted before that lands.

## 6. Stability & Volatility Map

| Module | Stability | Notes |
|---|---|---|
| `Utf8XmlReader` | **Started** | `ref struct` shape + real span/`ReadOnlySequence<byte>` dual-constructor buffering in place (§2); token-level reading (`ReadDocument`/`ReadElement`/etc.) still stubs |
| `TokenType` | **Settling** | 9 real token kinds defined; no `None` sentinel (§4.2) |
| `Constants` | **Started** | Byte-level vocabulary defined; `xsi:nil`/CDATA constants unverified against real data (§5) |
| `ReaderState` | **Shape settled** | Full `IReaderState<TState>` implemented; nothing populates real values yet |
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

## 9. Attribution

`Utf8XmlReader`'s shape — a `ref struct` tokenizer over a caller-supplied buffer, reconstructed
fresh from `(buffer, state)` per step rather than held across an `await` — deliberately mirrors
`System.Text.Json.Utf8JsonReader`/`JsonReaderState` (§2, §4.1; see core ARCHITECTURE.md §4.4 for
why). That's Microsoft's open-source .NET runtime (`dotnet/runtime`, MIT licensed), and design
patterns and, in places, specific logic are adapted from it throughout this package. See
[/THIRD-PARTY-NOTICES.md](../../../THIRD-PARTY-NOTICES.md) for the license text and the inline
attribution comment convention used where a specific method is a direct adaptation rather than
just a shared shape.
