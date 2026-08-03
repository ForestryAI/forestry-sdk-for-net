# Task #110 — Review current code base

## User Story
#106 — Deserialize

## Scope
Review only — no files were modified for this task. Files examined across `Forestry.Deserialize` and `Forestry.Deserialize.Xml`: `Deserializer.cs`, `Deserializer.OfType.cs`, `DeserializerFactory.cs`, `DeserializerKind.cs`, `IReader.cs`, `IReadStack.cs`, `ReaderExtensions.cs`, `ValueAsyncEnumerable.cs`, `ValueAsyncEnumerator.cs`, `Value.cs`, `Value.OfType.cs`, `DeserializedValue.OfType.cs`, `DeserializeOptions.cs`, `DeserializeOptions.Caching.cs`, `Deserialization.cs`, `Deserialization.String.cs`, `Deserialization.Span.cs`, `DeserializeXmlOptions.cs`, `Deserializers/ObjectDeserializer.cs` (Xml), plus the `test/Testing*.cs` fixtures.

## What was established

**Two halves exist and both work in isolation, but were never wired together.** The reader → `Value` stream (`IReader.Read()`/`Current`, `ReaderExtensions.AsAsyncEnumerable`, `ValueAsyncEnumerable`/`ValueAsyncEnumerator`) works and is tested (`ValueAsyncEnumerableTests.cs`), but knows nothing about any target type. The `Type` → `TypeDefinition`/`PropertyDefinition` schema reflection (`Deserializer.InitializeTypeDefinition`) also works, per the prior "schema reflection working end-to-end" commit. The bridge between them — `Deserializer<T>.Read`/`ReadAsync` in `Deserializer.OfType.cs` — has zero callers anywhere in the solution, defaults to an empty enumerator, and is never invoked by the public entry point: `Deserialization.Deserialize<T>(string, options)` literally throws `NotImplementedException`, with a comment pointing at the reader path instead.

**`Forestry.Deserialize.Xml/Deserializers/ObjectDeserializer.cs` is stale, not garbage.** Untouched since the commit before the reader/schema rebuild, it calls a `Deserializer(DeserializeOptions options)` constructor and `Options`/`Deserialize(Type, object)` members that no longer exist on the current abstract `Deserializer`. It won't compile against the current base class. But its *intent* — a media-specific `Deserializer` subclass — turned out to be exactly the right direction (see below), so it's the seed to rewrite once the new `Read` shape lands, not something to delete outright.

**Direction settled for the missing bridge:**
- `DeserializeOptions` stays concrete-per-media (already true via `DeserializeXmlOptions`) and additionally becomes responsible for constructing the right `IReader` per source shape (span/stream/etc.) — normalizing away the fact that different media readers have fundamentally different constructors (e.g. a JSON reader over a `ReadOnlySpan<byte>` vs. XML's several `Create` overloads). `Deserialization.String.cs`/`Deserialization.Span.cs` call into that hook rather than knowing about concrete reader types directly.
- A typed streaming entry point, `Deserializer.AsAsyncEnumerable<T>(stream/span, options)`, returns `Value`s shaped by `T` — the typed sibling of the existing untyped `IReader.AsAsyncEnumerable()`.
- Span-backed sources are inherently sync-only (a `ReadOnlySpan<byte>` is a ref struct and can't cross an `await` or live in an async iterator's state machine), so `Deserialization.Span.cs` is necessarily a separate synchronous path from any stream-based async one — they can't share an implementation. (Buffering underneath a reader — e.g. refilling bytes for a `Utf8JsonReader` — can still be async even though the token reader itself is sync/span-based; that's a reader-internal concern, not a reason the public span entry point becomes async.)

**`Deserializer<T>.Read`/`ReadAsync` reshaped from returning `IEnumerator<Value>`/`IAsyncEnumerator<Value>` to a step-function returning a produced/not-produced signal plus a `Value`.** The enumerable-return shape was the wrong hand-off mechanism — decided against in favor of the Enumerator driving a single step call in a loop (mirroring how `ValueAsyncEnumerator` already drives raw `IReader.Read()`/`Current` today). Concretely: sync `Read` can use `bool Read(IReader reader, IReadStack readStack, DeserializeOptions options, out Value value)`. The async counterpart cannot use the same `out Value` shape — **`out`/`ref`/`in` parameters are illegal on any C# method marked `async`**, so `ReadAsync`'s hand-off needs a different encoding (e.g. `ValueTask<Value?>` with `null` meaning "nothing this call," or a small result struct) — not yet decided.

**`ref` dropped from `IReader reader`/`IReadStack readStack` parameters.** Both are interfaces (reference types); nothing described needs to reseat which instance the caller holds, only to mutate/advance the existing one. `ref` was also the thing directly blocking any override from using idiomatic `async`/iterator syntax internally, since the async-method restriction on `ref`/`out` applies regardless of return shape.

**`IReadStack` needs real members — it's currently an empty marker interface.** It exists to carry resumable "where am I in the type hierarchy" state across the walk (which property/frame is active), which is what lets a single `Deserializer<T>.Read` call recurse down through nested objects to a leaf value-kind deserializer and, eventually, resume correctly across calls.

**The Enumerator (sync and async) owns `IReadStack` creation and disposal — not raw `IReader` driving.** It creates the stack up front, loops the Deserializer's step function until it signals "produced," surfaces that as its own `Current`, and disposes the stack as soon as the step function signals exhaustion (not deferred to `DisposeAsync`). It does **not** call `reader.Read()` itself for the typed case — that's now internal to the Deserializer's own walk, a deliberate contrast with today's untyped `ValueAsyncEnumerator`, which does drive `IReader.Read()` directly.

**`Deserializer` stays entirely synchronous — no `ReadAsync` on `Deserializer` at all.** All genuine awaiting (e.g. refilling a buffer from a stream for a JSON reader) is pushed down into the `IReader`/buffering layer, never into the Deserializer's walk. This is what fully resolves the `ref`/`async` conflict above, rather than just working around it.

**Because a media-specific `Deserializer` implementation is required, not incidental, the generic `Forestry.Deserialize` core supplies no default walking behavior for any `DeserializerKind`.** The answer to "can the reader run out of buffered data mid-walk, or does a refill always top up before `Read` is invoked" is genuinely different per media (JSON vs. XML vs. a future StanForD reader), so the core can only define the contract (`Deserializer`/`Deserializer<T>` shape, `TypeDefinition`/`PropertyDefinition` schema reflection, `DeserializeOptions`/`AsAsyncEnumerable<T>` wiring). Each media package must supply its own concrete `Deserializer` subclasses per kind (Object/Value/Enumerable/Dictionary), walking however fits its own reader's buffering behavior.

**Open, unresolved question:** whether a reader can be exhausted of buffered bytes *partway through* a single `Deserializer.Read` call (not just between top-level calls) — if so, the walk needs to pause and resume via `IReadStack` state around an async refill, a pattern `System.IO.Pipelines.PipeReader` (await a fill, run a sync parser over the buffered sequence, report bytes consumed, loop if not enough) was built for. Not decided whether that's the right primitive here, or whether refills are guaranteed to always happen before a `Read` call so this never comes up.

**Concurrency requirement clarified: multiple documents genuinely in flight at once, sharing a limited thread pool** — not just "process a batch faster with parallelism." That's what makes reader unification a real problem rather than a nice-to-have: `System.Xml.XmlReader` and `System.Text.Json.Utf8JsonReader` are built on opposite assumptions, and the mismatch matters once true non-blocking concurrency is a requirement.
- `Utf8JsonReader` is a `ref struct` doing zero buffering itself — it only ever sees the span it's handed, `Read()` is sync-only (can't be async, can't cross an `await`), and it exposes `BytesConsumed`/`IsFinalBlock`/`JsonReaderState` specifically so the *caller* can refill a buffer and resume a fresh reader from saved state. This shape is naturally async-friendly: await a refill, then run a synchronous parse burst over what's buffered.
- `XmlReader` owns its buffering internally over a `Stream`/`TextReader`, offering `Read()` *or* `ReadAsync()` — but no way to ask "how many raw bytes did that last `Read()` actually consume," so there's no way to bound a synchronous parse burst to only what's already buffered. Wrapping it in a custom async-fed buffering `Stream` doesn't fully fix this: its sync `Read()` would still be able to block waiting on the buffer if `XmlReader` asks for more before a fill catches up.

**Resolved direction: drop `System.Xml.XmlReader`, write a custom span-based XML reader shaped like `Utf8JsonReader`** — sync-only, operates over a caller-supplied buffer, exposes its own consumed-bytes/resumable-state equivalent. This makes `IReader`'s contract genuinely uniform across both media (not just similar effort) and reopens true non-blocking concurrency as tractable, since XML gains the same buffer-in/token-out shape JSON already has.

**Scope grounded against a real sample, not assumed.** Verified directly (grep, not eyeballing) against `gpx175-sdcgpx2036-3cdb35d7_6108_4b19_8d30_d6dfd2c44e9b-X2.04-2026-01-14 1714.hpr.xml` (935KB, 15,900 lines, one of several real `.hpr`/`.hqc`/`.fpr` StanForD exports under `C:\Users\Dator\Downloads\forestry-blobs\operative\production\11002374\StanForD\`) — a Rottne harvester's HPR (StanForD 2010, schema v3.5) production file:
- Single default namespace (`urn:skogforsk:stanford2010`) declared once on the root element, never reassigned deeper in the tree — `xsd`/`xsi` prefixes appear only as attributes on the root (for `xsi:schemaLocation`), never as element prefixes anywhere. Confirmed zero prefixed elements in the whole file.
- Zero CDATA sections, zero entity/character references (`&...;`), zero comments, confirmed by grep across the entire file.
- Self-closing empty elements used for nulls (`<MachineOwnerID />`, `<Address />`) — 48 instances.
- 278 repeated `<Log>` records, each with ~8 `<LogDiameter>` (2224 total) plus `LogVolume`/`LogMeasurement`/etc. — the real shape of the "give me every Log entry" streaming case from earlier in this review, now with concrete numbers instead of a hypothetical.

Net effect: because there's no per-branch namespace reassignment, "namespace resolution" collapses to a constant check (every element belongs to the one URI declared at the root) rather than needing a real ancestor prefix-scope stack — the single biggest source of general-XML complexity turns out not to apply here. Combined with zero entities/CDATA/mixed content, a custom reader only needs to tokenize: elements, attributes, self-closing empties, and plain text content. Not "compete with `System.Xml`" — a tokenizer for this specific narrow grammar.

## Acceptance criteria (for the follow-up implementation task — none of this is built yet)
- `Deserializer<T>.Read` is `bool` + `out Value`, no `ref` on the `IReader`/`IReadStack` parameters; `Deserializer<T>` has no `ReadAsync` member.
- `IReadStack` carries real, resumable walk-frame state instead of being an empty interface.
- `DeserializeOptions` gains a reader-construction hook per source shape; `DeserializeXmlOptions` implements it.
- `Deserialization.String.cs`/`Deserialization.Span.cs` (or the new `AsAsyncEnumerable<T>` entry point) go through that hook rather than referencing concrete reader types.
- `Forestry.Deserialize.Xml/Deserializers/ObjectDeserializer.cs` is rewritten against the current `Deserializer<T>` shape (or removed if superseded) — it must not reference the removed `Deserializer(options)` constructor/`Options`/`Deserialize(Type, object)`.
- A custom StanForD-scoped XML reader replaces `System.Xml.XmlReader` in `Forestry.Deserialize.Xml`, shaped like `Utf8JsonReader` (sync, buffer-in/token-out, exposes consumed-bytes/resumable state) — scoped to: elements, attributes, self-closing empties, one fixed default namespace, plain text content. No CDATA, no entity/character reference decoding, no mixed content, no namespace-prefix scope stack.

## Out of scope
- Actually implementing any of the above — this task was design review only.
- Deciding `ReadAsync`'s exact hand-off type (`ValueTask<Value?>` vs. a result struct) — flagged, not settled.
- Deciding whether a reader can be exhausted mid-walk and needs `PipeReader`-style pause/resume, or whether refills always precede a `Read` call — flagged, not settled.
- A JSON media package — discussed hypothetically as the second real test of the abstraction, not started.
- Handling XML outside this narrow dialect (namespace reassignment, CDATA, entity decoding, mixed content) — deliberately not supported unless a real StanForD sample surfaces one of these.
- The real `Forestry.StanForD` ingestion project that this whole review is ultimately blocking (Feature #105) — out of scope for this task.

---

# Task #110 (continued) — Core plumbing reaches POC-sufficient shape

## User Story
#106 — Deserialize

## Scope
Implementation this round (the entry above was review-only, nothing built): `Reading/ReaderPath.cs`, `Reading/ReaderPosition.cs`, `Reading/ReadingStatus.cs`, `Reading/IReaderState.cs`, `Reading/IBuffering.cs`, `Reading/PipeReaderBuffering.cs`, `Deserializers/Deserializer.cs`, `Deserializers/Deserializer.OfType.cs`, `ValueAsyncEnumerator.cs`, `ValueAsyncEnumerable.cs`, `ARCHITECTURE.md`.

## What was established

**Everything the review left open about naming and the async/`ref` conflict is now settled and built, not just decided.** `IReadStack`/`Graph`/`Node`/`IState` became, after several rounds of naming discussion: `ReaderPath` (a stack of `ReaderPosition`s tracing one root-to-current chain through the `TypeDefinition` hierarchy — deliberately not "graph," since there's never more than one active chain) and `ReaderPosition` (one `TypeDefinition` plus which property it's currently on), and `IState` became `IReaderState<TState>` (continuation state to recreate a reader, generic per media). Both `ReaderPath`/`ReaderPosition` and `ReadingStatus` had to become `public`, not `internal` as first cut — `DeserializeOptions.UserDefinedDeserializers` already commits external consumers to writing their own `Deserializer<T>`, and a `public abstract` method can't expose a less-accessible type in its signature.

**The walk's step function, `Deserializer.TryReadValue`, returns a three-state `ReadingStatus` (`Value`/`NoValue`/`Partial`), not a `bool`.** A `bool` genuinely can't carry this — it can't distinguish "no value yet, buffer more and retry" from "done forever," which would either cut enumeration short at every buffer boundary or spin forever past real EOF. Went through two intermediate shapes before landing here: first a separate `IsReadable` pre-check the Enumerator called before `TryReadValue` (rejected — two methods that have to agree with each other, real risk of drift), then folding the check into `TryReadNullableValue` itself via an `out Value<T>` + `ReadingStatus` return (settled).

**Corrected mid-build: the generic `Deserializer<T>.TryReadValue` cannot own updating `ReaderPath`/`ReaderPosition` — only the concrete media `TryReadNullableValue` can.** The initial cut had `TryReadValue` responsible for advancing position, assuming a uniform, schema-declaration-order walk. Wrong: whether property order in the media matches declaration order, whether a property is an attribute or a child element, when a property's data is actually complete — all of that can only be determined by reading real tokens, which is exactly the thing that differs per media. So `TryReadValue` is now a thin pass-through; `TryReadNullableValue` (abstract, implemented per media) is fully responsible for reading a property, marking it read, and updating `ReaderPath`/`ReaderPosition` itself, in addition to returning `ReadingStatus`.

**`PipeReaderBuffering` is the first real `IBuffering<TBuffering, TStream>` implementation — async-only, correct against real `PipeReader` semantics.** Uses `ReadAtLeastAsync` with a minimum size that doubles (`_partialReadBytes`) whenever a step consumes zero bytes, avoiding a tight little-by-little read loop for a value spanning a large chunk; `Advance(0)` maps to `AdvanceTo(sequence.Start, sequence.End)` — the correct `PipeReader` idiom for "examined everything, consumed nothing, don't wake me until there's more." Skips a UTF-8 BOM once, including the case where it spans multiple pipe segments. A real contract this depends on, not yet enforced anywhere except a `Debug.Assert`: whatever implements `TryReadNullableValue` **must** call `Advance` — even with `0` — before returning `Partial`, or the next `ReadAsync` call violates `PipeReader`'s own "no concurrent read before `AdvanceTo`" invariant and throws, one call removed from the actual mistake. Worth writing that requirement onto `TryReadNullableValue`'s own doc comment before a concrete media implementation is written against it.

**Declared sufficient for POC purposes as of this note.** Stream-based buffering (sync and async, for testing without a real pipe) is deferred, not built. `IsCompleted` (real end-of-stream, as opposed to `Partial`) is captured on `PipeReaderBuffering` but nothing downstream reads it yet.

## Acceptance criteria
- Satisfied, for the core (`Forestry.Deserialize`) package's plumbing: `ReadingStatus` tri-state wired end to end through `Deserializer`/`Deserializer<T>`/`ValueAsyncEnumerator`; `ReaderPath`/`ReaderPosition` real, public, and correctly scoped (ownership corrected mid-build per above); `PipeReaderBuffering` implemented against real `PipeReader` semantics. All builds clean.
- Not yet satisfied, moving to `Forestry.Deserialize.Xml` next (tracked by new GitHub issues, not yet filed as of this note):
  - The ref-struct XML reader itself (started by the user, not yet reviewed here).
  - Concrete `Deserializer<T>` subclasses implementing `TryReadNullableValue` for real XML types.
  - `Forestry.Deserialize.Xml/Deserializers/ObjectDeserializer.cs` still doesn't compile against any of this — still the seed to rewrite, untouched so far.
  - Stream-based `IBuffering` (sync + async) for testing without a real pipe — explicitly deferred by the user.

## Out of scope
- The ref-struct XML reader and concrete XML `Deserializer`s — next task, to be tracked by forthcoming GitHub issues.
- Stream-based buffering — explicitly deferred ("later for testing").
- A JSON media package — still just the second hypothetical test of the abstraction.
- Wiring `IsCompleted`/true-EOF detection into the walk — `PipeReaderBuffering` captures it, nothing consumes it yet.
