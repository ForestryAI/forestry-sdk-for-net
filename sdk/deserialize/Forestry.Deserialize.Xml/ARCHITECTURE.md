# ARCHITECTURE.md — Forestry Deserialize Xml

## 1. Purpose

**Forestry.Deserialize.Xml** is the first concrete media provider for `Forestry.Deserialize`
(see that package's own [ARCHITECTURE.md](../Forestry.Deserialize/ARCHITECTURE.md)), targeting
StanForD XML from harvesting and forwarding machines. It supplies the one thing the core package
deliberately does not: a reader that knows how to walk actual XML tokens and the
`Deserializer<T>` subclasses that drive `ReaderPath`/`ReaderPosition` from what that reader sees.

## 2. High-Level Architecture

**A custom, `Utf8JsonReader`-shaped tokenizer replaces `System.Xml.XmlReader`.** `XmlReader`
offers no way to ask how many raw bytes its last `Read()` actually consumed, so a caller can't
bound a synchronous parse burst to only what's already buffered — which breaks the buffer-in/
token-out contract the core package's `IBuffering`/`Deserializer<T>.TryReadNullableValue` walk
depends on (see core ARCHITECTURE.md §4.3–4.4). `Utf8XmlReader` is a `ref struct` instead: sync
only, operates over a caller-supplied span, and is reconstructed fresh from `(buffer, state)` per
step — mirroring `Utf8JsonReader`/`JsonReaderState` exactly, not inventing a new pattern.

**Scoped to StanForD's actual dialect, not general XML.** Verified directly against a real
production `.hpr` export (see `CLAUDE.md`'s Task #110 entry for the grep evidence): a single
default namespace declared once at the root and never reassigned deeper in the tree, zero
CDATA/entity/character references, zero comments, and self-closing empty elements used for nulls.
That means namespace resolution collapses to a constant check instead of an ancestor prefix-scope
stack, and the tokenizer only has to recognize: start/end elements, attributes, self-closing
empties, and plain text content. This is a tokenizer for one narrow grammar, not a competitor to
`System.Xml`.

**Everything genuinely async stays in the core package's buffering layer.** `Utf8XmlReader` itself
never awaits anything — refilling the span it reads from is `PipeReaderBuffering`'s job (core
package), not this one's. This package's only responsibility is turning already-buffered bytes
into tokens, and turning a schema (`TypeDefinition`/`PropertyDefinition`) plus a stream of tokens
into `Value`s.

## 3. Module Boundaries

- **`Forestry.Deserialize.Xml`** — this package. Depends on `Forestry.Deserialize` for every
  contract it implements against (`Deserializer<T>`, `IReaderState<TState>`, `IBuffering`,
  `ReaderPath`/`ReaderPosition`, `DeserializeOptions`). Defines nothing that a JSON or other future
  media package would need to share — anything genuinely media-agnostic belongs in the core
  package, not here.
- **`Forestry.Deserialize`** (sibling, referenced via `ProjectReference`) — the media-agnostic
  walk contract this package plugs into. See its own ARCHITECTURE.md for the shape this package is
  implementing against.

**Stability:** Experimental — every type below is an early skeleton; several don't compile yet
(§5).
**Dependencies:** `Forestry.Deserialize` only.

## 4. Core Contracts

### 4.1 `Utf8XmlReader` (`src/Reading/Utf8XmlReader.cs`)

A `ref partial struct`, sync-only tokenizer over a caller-supplied buffer — the XML analogue of
`Utf8JsonReader`. `Read()` advances to the next element/attribute; `Skip()` skips the current one;
`GetString()` reads the current token as a string. Being a `ref struct`, it can never be held
across an `await` or stored in a class field — a fresh instance is constructed per step from
`(buffer, ReaderState)`, exactly like `Utf8JsonReader`/`JsonReaderState` (see core ARCHITECTURE.md
§4.4).

### 4.2 `TokenType` (`src/TokenType.cs`)

The token kinds `Utf8XmlReader.Read()` advances between — currently only the `None` default
exists (§5). Per the narrow-dialect decision in §2, this only ever needs to distinguish
start/end element, attribute, self-closing empty element, and text content — not the full
`System.Xml` token vocabulary (no CDATA, no entity references, no processing instructions/DTDs).

### 4.3 `ReaderState` (`src/Reading/ReaderState.cs`)

The storable, non-ref continuation state for `Utf8XmlReader`, implementing the core package's
`IReaderState<ReaderState>` (see core ARCHITECTURE.md §4.4) — this is what `DeserializeXmlOptions
.CreateReaderState<TState>` is meant to produce and what a fresh `Utf8XmlReader` is reconstructed
from between steps. Currently carries `ReaderPositionLineNumber` and `TokenType`; the interface
also requires `ReaderPositionName`, `ReaderPosition` (byte offset in line), and `IsObject`, none of
which are implemented yet (§5).

### 4.4 `DeserializeXmlOptions` (`src/DeserializeXmlOptions.cs`)

The `DeserializeOptions` subclass this media plugs into the core package through (see core
ARCHITECTURE.md §4.5): reflective `TypeDefinition`/`PropertyDefinition` instantiators, an
`IDeserializerProvider`, and `CreateReaderState<TState>`. `Default` is the ready-made instance
consumers are expected to reach for. Every member is currently a stub (§5).

### 4.5 `ObjectDeserializer<T>` (`src/Deserializers/ObjectDeserializer.cs`)

The XML-specific `Deserializer<T>` for object-shaped types — where `TryReadNullableValue` will
actually walk `Utf8XmlReader` tokens against `ReaderPath`/`ReaderPosition`, matching elements/
attributes to `PropertyDefinition`s and marking them read (see core ARCHITECTURE.md §4.3 for why
that walk can only live here, not in the core package). Not started (§5) beyond the empty class
declaration. Value-kind, Enumerable-kind, and Dictionary-kind XML deserializers don't exist yet
either — only the Object case has a (stub) file.

## 5. Status / POC Debt

- **Nothing in this package compiles.** Specifically, as of this writing:
  - `ObjectDeserializer<T>` declares no members, so it does not satisfy `Deserializer`/
    `Deserializer<T>`'s abstract surface (`Type`, `GetDeserializerKind`, `CanDeserialize`,
    `InitializeTypeDefinition`, `TryReadNullableValue`) — a non-abstract class must implement all
    of these.
  - `ReaderState` implements only `ReaderPositionLineNumber` and a non-interface `TokenType`
    member; `IReaderState<ReaderState>` also requires `ReaderPositionName`, `ReaderPosition`, and
    `IsObject`, none of which are declared.
  - `DeserializeXmlOptions`'s three `internal abstract` overrides and `CreateReaderState<TState>`
    are all explicit `throw new NotImplementedException()` bodies.
  - `Utf8XmlReader.cs` declares its `ref partial struct` with **no enclosing namespace** — it sits
    in the global namespace, inconsistent with every other type in this package (`Forestry
    .Deserialize.Xml.Reading`). Worth fixing when the reader is actually built out, so it resolves
    the same way its `ReaderState`/`TokenType` neighbors do.
- **`TokenType` has only its `None = 0` default** — no real token kinds defined yet (§4.2).
- **`Utf8XmlReader.Read()`/`Skip()`/`GetString()` are unconditional stubs** (`return false`/no-op/
  `return null`) — no actual tokenizing happens.
- **No `Value`/`Enumerable`/`Dictionary`-kind XML deserializer exists** — only the `Object`-kind
  stub file. `DeserializerFactory`/`IDeserializerProvider` wiring for any of them is unbuilt.
- **No test project content** — `Forestry.Deserialize.Xml.Tests` exists as a `.csproj` shell only
  (no test files under it yet).

## 6. Stability & Volatility Map

| Module | Stability | Notes |
|---|---|---|
| `Utf8XmlReader` | **Unstarted** | `ref struct` shape agreed (§2), no real tokenizing implemented |
| `TokenType` | **Unstarted** | Only the `None` default exists |
| `ReaderState` | **Unstarted** | Missing 3 of 4 `IReaderState<TState>` members |
| `DeserializeXmlOptions` | **Unstarted** | Every override throws |
| `ObjectDeserializer<T>` | **Unstarted** | Stale seed from before the current `Deserializer<T>` shape; doesn't compile |
| Value/Enumerable/Dictionary deserializers | **Not begun** | No files exist |

## 7. Anti-Goals

- This package does not aim to parse general XML — no namespace-prefix reassignment, no CDATA, no
  entity/character reference decoding, no mixed content, no comments, no processing instructions
  or DTDs — unless a real StanForD sample surfaces one of these (see CLAUDE.md's Task #110 entry
  for the grep evidence this scoping is based on).
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
