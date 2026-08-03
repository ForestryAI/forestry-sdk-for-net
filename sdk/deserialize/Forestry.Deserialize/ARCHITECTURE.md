# ARCHITECTURE.md — Forestry Deserialize

## 1. Purpose

**Forestry Deserialize** reads large documents — the primary case is StanForD XML from
harvesting and forwarding machines — and streams `Value`s out one at a time instead of loading a
whole document into memory. A multi-gigabyte harvester log should cost roughly the same,
memory-wise, to process as a small one. This package defines the media-agnostic machinery that
makes that possible; it does not itself know how to parse XML, JSON, or any other concrete
format — that's a sibling package per media (`Forestry.Deserialize.Xml` today).

---

## 2. High-Level Architecture

Four ideas carry the whole design:

**Async only exists at the buffering boundary — never inside the walk itself.** Reading a
document is driven by ordinary synchronous stepping through a `TypeDefinition`/`PropertyDefinition`
graph. The only place real awaiting happens is filling a buffer from a `Stream` (or a
`System.IO.Pipelines.PipeReader` wrapped around one) — buffering an already in-memory span never
needs to be async at all. This is what lets the walk stay simple, recursive, and free of the C#
`async`/`ref`/`out` restrictions that would otherwise contaminate every layer that touches it: see
`IBuffering<TBuffering, TStream>` (`ReadAsync`/`Read`/`Advance`), which every concrete buffering
strategy implements.

**`TypeDefinition`/`PropertyDefinition` reflect the media's shape and tie reading to a
`Deserializer`.** A schema class describes what a document should look like — which properties
exist, and (via each `Deserializer`'s `DeserializerKind`) whether a given type reads as an object,
a single value, an enumerable, or a dictionary. This is a guard as much as a map: because the
shape is known ahead of time, a mismatch between what the media actually contains and what the
schema expects can be caught while streaming, not only after everything has been loaded. Every
`TypeDefinition` carries the `Deserializer` responsible for actually reading its `Type`.

**A `Deserializer` is an abstraction that advances position in the type/property graph and tries
to produce a value — but the base, generic `Deserializer<T>` cannot do that advancing itself.**
Updating the `ReaderPath`/`ReaderPosition` (matching the next raw token against a property,
marking it read, moving position to reflect that, deciding when an object's properties are
exhausted) depends on reading real tokens, which only the concrete media reader can do — property
order in the media need not match declaration order, attributes vs. elements are visited
differently, and so on. So `Deserializer<T>.TryReadValue` is a thin, generic pass-through; the
actual walk — reading, marking properties read, and updating the path/position — lives entirely
inside the abstract `Deserializer<T>.TryReadNullableValue` that a concrete package like
`Forestry.Deserialize.Xml` implements. See §4.3 and `Forestry.Deserialize/CLAUDE.md`'s Task #110
entry for the current, still-settling shape of that walk.

**`DeserializeOptions` is the seam a concrete media plugs into.** It supplies the reflective
instantiators that turn a `Type` into a `TypeDefinition`/`PropertyDefinition`, an
`IDeserializerProvider` of built-in and factory `Deserializer`s, naming/ignore/include-field
policies, user-defined `Deserializer`s for special-cased types, and — the newer piece — a way to
create a concrete reader's storable continuation state from an abstract shape
(`CreateReaderState<TState>` where `TState : struct, IReaderState<TState>`). A concrete reader is never
constructed once and held; it's reconstructed cheaply from `(buffer, state)` on demand (see §4.4)
— `DeserializeOptions` is what a media package hooks into to make that possible for its own reader.

---

## 3. Module Boundaries

- **`Forestry.Deserialize`** — the media-agnostic core: `Value`/`Value<T>`, `TypeDefinition`/
  `PropertyDefinition`, `Deserializer`/`Deserializer<T>`, `DeserializeOptions`, the buffering/
  reader-state contracts, and the walk machinery that ties them together.
- **`Forestry.Deserialize.Xml`** — the first concrete media provider, targeting StanForD XML. A
  JSON or other format provider is a sibling package built the same way, not a variant of this one.
  **Currently non-compiling** — see §5.

**Stability:** Experimental — the walk contract (§4.3) is still actively changing; treat every
type name below as more likely to be renamed than not.
**Dependencies:** None outside the BCL for the core package; `Forestry.Deserialize.Xml` depends on
`Forestry.Deserialize`.

---

## 4. Core Contracts

### 4.1 `Value` / `Value<T>` — two separate hierarchies, not one

`Value` (a class) is what actually flows out of a read: a name, raw bytes, and a `Dimensions` bag
of metadata about *how* it was read (§4.2) — never business data. `Value<T>`/`NullableValue<T>` is
a **separate** hierarchy — it does not inherit from `Value` — representing a deserialized `T`
paired with the `Value` it came from (`GetValue()` returns that wrapped `Value`;
`DeserializedValue<T>` is the concrete pairing). Treating a `Value<T>` as a `Value` requires
unwrapping through `GetValue()`/`HasValue`; there is no cast between them. This tripped a real bug
during development (a silent-always-null `as Value`) — worth keeping visible precisely because the
two hierarchies looking related by name invites assuming they're related by inheritance.

### 4.2 `Value.Dimensions`

Built-in dimension names (`Dimension.Names`): `Date`, `Raw-Value-Type`, `Raw-Value-Length`,
`Depth` (nesting depth in the source media), `Namespace` (e.g. an XML namespace URI). The walk is
expected to also stamp ancestry dimensions — which object instance (which `Log`, which `Machine`)
a leaf value was read from — so a flat stream of leaf values stays traceable to its logical parent
without ever materializing that parent as an object. This ancestry-stamping is not implemented yet
(see §5).

### 4.3 The walk: `Deserializer<T>`, `ReaderPath`, and `ReaderPosition`

Reading one `Value` is a step function, not a return of a fully assembled object: given the current
position in the type/property structure, either read a leaf value or move (into a property, or back
out to a parent once one is exhausted) and try again. The current position is tracked outside any
C# call stack specifically so it can survive being paused between one `MoveNextAsync` and the next —
an ordinary recursive walk can't do that, because its state disappears the moment the method
returns.

That position lives under `Forestry.Deserialize.Reading`, in two types:

- **`ReaderPath`** — a subsection of the `TypeDefinition` hierarchy currently active for a read:
  the chain of `TypeDefinition`s from the root down to wherever reading currently is, each with its
  properties already expanded and indexed. Deliberately named *path*, not *graph* — there is never
  more than one active chain at a time, so "graph" overstated the generality of what's actually
  tracked.
- **`ReaderPosition`** — one level within a `ReaderPath`: a `TypeDefinition` together with which of
  its properties the deserialization will act on next. `ReaderPath.Position` is the current (last)
  one — where the active `Deserializer<T>.TryReadNullableValue` is reading and updating position.

See §4.4 for `IReaderState<TState>`, the type a concrete reader's continuation state implements.

### 4.4 Buffering and reader construction

A concrete reader (e.g. one wrapping `System.Text.Json.Utf8JsonReader`) is never held across steps
— some concrete readers *can't* be, being `ref struct`s that can't live in an ordinary class field.
Instead, each media package defines a small, ordinary (non-ref) storable state type
(`IReaderState<TState>`) plus a buffering strategy (`IBuffering<TBuffering, TStream>`) that can fill from
a `Stream` sync or async. A reader is constructed fresh from `(buffer, state)` for each step, used,
and its updated state captured back out before the step returns — mirroring
`Utf8JsonReader`/`JsonReaderState`, not inventing a new pattern.

`PipeReaderBuffering` is the first real `IBuffering` implementation — async-only, built directly
against `System.IO.Pipelines.PipeReader`. Whatever implements `TryReadNullableValue` (§4.3) **must**
call `Advance` — even with `0` bytes consumed — before returning `ReadingStatus.Partial`, or the
next buffering `ReadAsync` violates `PipeReader`'s own invariant (no read before `AdvanceTo` on the
prior result) and throws. Sync/stream-based buffering (for testing without a real pipe) is
deferred, not built.

### 4.5 `DeserializeOptions`

Per-media subclass (e.g. `DeserializeXmlOptions`) supplying: `TypeDefinitionReflectiveInstantiator`,
`PropertyDefinitionReflectiveInstantiator`, `IDeserializerProvider` (built-in + factory
`Deserializer`s), `CreateReaderState<TState>`, naming/ignore/include-field policies, and a list of
user-defined `Deserializer`s for types that need custom handling. `TypeDefinition`s are cached per
distinct `DeserializeOptions` instance once read-only (`SetReadOnly()`); the cache is keyed by an
options-equality comparer that currently treats every instance as equal (see §5).

---

## 5. Status / POC Debt

- **No concrete `TryReadNullableValue` implementation exists yet, so nothing actually advances
  `ReaderPath`/`ReaderPosition`.** `Deserializer<T>.TryReadValue` is just a pass-through to it now
  (see §2/§4.3) — the real walk (reading, marking properties read, updating position) has to live
  in a media-specific override, and none has been written. This is the actively-in-progress piece;
  see `CLAUDE.md`'s Task #110 entry for the fuller design history and open questions (dimension
  stamping, `Enumerable`/`Dictionary` kinds, the async hand-off shape).
- **`Forestry.Deserialize.Xml` does not compile.** `ObjectDeserializer.cs` and
  `DeserializeXmlOptions.cs` predate the current core shape; `DeserializeXmlOptions` references
  `XmlTypeDefinition`/`XmlPropertyDefinition`/`XmlDeserializerProvider`, none of which exist
  anywhere in the repository yet.
- **No concrete reader exists for any media in this repository yet, though one is in progress.** A
  custom, `Utf8JsonReader`-shaped ref-struct XML reader (not `System.Xml.XmlReader`), scoped to
  StanForD's actual (narrow, single-default-namespace, no-CDATA/entities/mixed-content) dialect per
  `CLAUDE.md`'s decision, has been started but not reviewed here. Concrete `Deserializer<T>`
  subclasses implementing `TryReadNullableValue` for XML types haven't been started. This work is
  being tracked by new GitHub issues going forward rather than only this document.
- **`Deserialization.Deserialize<T>(string, DeserializeOptions)` is an explicit
  `NotImplementedException` stub.** Its replacement is the `AsAsyncEnumerable<T>`-shaped entry
  point described in `CLAUDE.md`, not yet built.
- **The `DeserializeOptions` cache's options-equality comparer treats every instance as equal** —
  distinct options with different policies would currently collide in the shared `TypeDefinition`
  cache.
- **Naming/ignore/include-field policies are inert defaults** — nothing is ignored or included
  today regardless of policy configuration.
- **Element/attribute position within a document isn't modeled anywhere** — only names are tracked,
  not order — flagged by a pending test in `TypeDefinitionReflectionTests`.

---

## 6. Stability & Volatility Map

| Module | Stability | Notes |
|---|---|---|
| `Value`/`Value<T>`/`Dimensions` | Fairly stable | Shape settled; ancestry-dimension stamping still to come |
| `TypeDefinition`/`PropertyDefinition` reflection | Stable-ish | Reflecting `[Element]`/`[Collection]` into a shape works and is tested |
| `Deserializer`/`Deserializer<T>` walk | **Highly volatile** | Actively being redesigned turn by turn; see CLAUDE.md |
| `ReaderPath`/`ReaderPosition` naming | Settled | Renamed from `Graph`/`Node` |
| `IReaderState` naming | Settled | Renamed from `IState` |
| `IBuffering`/reader-state pattern | Settling | Direction agreed (mirror `Utf8JsonReader`/`JsonReaderState`), not yet exercised by a real reader |
| `Forestry.Deserialize.Xml` | **Broken** | Does not compile; effectively unstarted against the current core |

---

## 7. Anti-Goals

- This package does not parse any concrete media itself — XML, JSON, or otherwise — that's every
  media package's own job.
- It does not hold a whole document, or even a whole object instance, in memory as an intermediate
  step — the streaming guarantee is the point; a "give me the whole `Log`" convenience API, if one
  is ever added, sits on top of this, not inside it.
- It does not assume a media format maps cleanly onto its own token boundaries the way JSON does —
  the walk is schema-driven precisely because something like XML (an element encloses any of
  object/value/enumerable/dictionary shape, indistinguishable from the tag alone) can't be
  interpreted from syntax without the `TypeDefinition`/`PropertyDefinition` telling it what to
  expect.
- It does not target arbitrary POCO graphs — the schema is meant to describe what a document
  reflects, driven by `[Element]`/`[Collection]`, not a general-purpose object mapper.

---

## 8. Related Work Items

- Feature #105 — Chat POC external
  - User Story #106 — Deserialize
    - Task #110 — Review current code base (see `CLAUDE.md` for the full design history this
      document summarizes)
