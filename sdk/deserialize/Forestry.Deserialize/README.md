# Forestry Deserialize

Forestry Deserialize reads large documents — the primary case today is StanForD XML from
harvesting and forwarding machines — and streams data out rather than loading a whole file
into memory. A multi-gigabyte harvester log should cost roughly the same, memory-wise, to
process as a small one. Because the media is ordered, a reader can also validate as it goes and
either fail fast or set a bad value aside and keep going, instead of only finding out something
was wrong after loading everything.

The package is split by concern:

- **Forestry.Deserialize** — the media-agnostic core.
- **Forestry.Deserialize.Xml** — the first concrete media provider, reading StanForD XML.
  JSON or another custom format would be a sibling provider built the same way.

Within the core there are two mostly-separate tracks; don't assume one is a layer on top of the
other yet:

1. **Streaming `Value` iteration** — `IReader` → `ValueAsyncEnumerable`/`ValueAsyncEnumerator` →
   `Value`. This is the working, tested path described below.
2. **Reflection-driven POCO mapping** — `TypeDefinition`/`PropertyDefinition`/
   `TypeDefinitionProvider`/naming & inclusion policies, aimed at eventually mapping a stream onto
   arbitrary C# types. It compiles but is not behaviorally complete or exercised by tests — see
   Status.

## Concepts (streaming path)

- **`Value`** — a single unit read from the media: a name, raw bytes, and a `Dimensions` bag of
  metadata about *how* it was read — not its business data. Built-in dimensions include `Date`,
  `RawValueType`, `RawValueLength`, `Depth` (nesting depth in the source), and `Namespace`.
- **`IReader`** — the extension point a concrete media format implements: `Read()` advances and
  exposes `Current`. A reader that fails on a malformed value must still leave itself positioned
  so the next `Read()` continues past it.
- **`ValueAsyncEnumerable`** / **`ValueAsyncEnumerator`** — wraps an `IReader` so its values can
  be streamed with `await foreach`, applying a `ReadErrorHandling` policy
  (`ShortCircuit`, the default — stop and rethrow the first failure; or `ShuntAside` — skip the
  failed value and keep reading) to the "ordered media, catch errors early" idea above.

```csharp
IReader reader = new StanForDXmlReader(stream); // media-specific, not part of this package yet

await foreach (Value value in reader.AsAsyncEnumerable(ReadErrorHandling.ShuntAside))
{
    int? depth = value.Dimensions.Depth;
    string? ns = value.Dimensions.Namespace;
    // ...
}
```

[`ValueAsyncEnumerableTests`](test/ValueAsyncEnumerableTests.cs) exercises this end-to-end
(order, metadata, both error-handling policies, cancellation) against a fake `IReader`, and is
the reference for how a concrete reader is meant to plug in.

## One `Value` at a time — being reconsidered

Reading currently yields a single `Value` per step. That's a reasonable default for "don't load
the whole document," but in practice most consumers don't want isolated scalar values — they
want a field group that only makes sense together, e.g. everything StanForD reports for one
machine, or one full log telemetry entry. The likely direction is to yield clumps — a record made
of the fields that belong to one logical unit — instead of one value at a time, while keeping the
same streaming guarantee. Treat the per-`Value` enumeration API as unstable until that lands.

A further step past that — iterating *types* picked out of the media by element or attribute
(e.g. "give me every Machine", "give me every Log entry") — is later work still, flagged by a
pending test in `ValueAsyncEnumerableTests`.

## Status

- **Streaming path** (`IReader`, `ValueAsyncEnumerable`/`ValueAsyncEnumerator`, `Value`
  `Depth`/`Namespace` dimensions): working and tested against a fake reader. No concrete
  `IReader` ships yet — `Forestry.Deserialize.Xml` is where a real StanForD XML reader belongs.
- **Reflection/POCO path** (`TypeDefinition`, `PropertyDefinition`, `TypeDefinitionProvider`,
  naming/ignore/include-field policies, the `DeserializeOptions` type-definition cache): compiles,
  but is unfinished and untested — notably, the cache's options-equality comparer currently
  treats every `DeserializeOptions` instance as equal, and `Deserialization.Deserialize<T>(string,
  DeserializeOptions)` is an explicit not-yet-implemented stub. Treat this path as a separate,
  larger effort from the streaming path above, not something to build the XML reader against yet.
- `Forestry.Deserialize.Xml`: `ObjectDeserializer` predates the current `Deserializer` base shape
  in core and doesn't compile. Worth rebuilding against `IReader` (the streaming path) rather than
  the reflection path, given the direction above.
- StanForD 2010 and StanForD Classic: no format-specific mapping yet — this section will
  document field/dimension mappings once a real XML `IReader` exists.
