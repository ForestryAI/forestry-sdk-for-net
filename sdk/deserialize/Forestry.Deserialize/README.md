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

Within the core there are two tracks that are meant to connect, even though nothing wires them
together automatically yet:

1. **Streaming `Value` iteration** — `IReader` → `ValueAsyncEnumerable`/`ValueAsyncEnumerator` →
   `Value`. Working and tested.
2. **Schema reflection** — `TypeDefinition`/`PropertyDefinition`, driven by `[Element]`/
   `[Collection]` attributes on a plain C# class that describes a document's shape (element/
   attribute names — not tied to XML, or to any particular media). Working and tested against a
   fake schema class; see Status for what's still missing.

The idea: a schema class reflected into a `TypeDefinition` tells a reader what to expect —
which elements/attributes, and eventually in what order — so mismatches can be caught while
streaming rather than after the fact. `SchemaGuidedReadingTests` shows the shape of that
without a real reader yet.

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

## Concepts (schema path)

- **`[Collection("Machine")]`** on a class names it for when it appears as an item in a list
  (`TypeDefinition.ElementCollection`).
- **`[Element("MachineId", "Machine")]`** on a property names it explicitly — independent of the
  C# member name.
- **`TypeDefinition`** / **`PropertyDefinition`** — the reflected shape of a schema class,
  computed once per `DeserializeOptions` and reused across reads (`GetTypeDefinition` caches by
  type).

```csharp
[Collection("Machine")]
public sealed class Machine
{
    [Element("MachineId", "Machine")]
    public string MachineId { get; set; } = string.Empty;
}

TypeDefinition machine = options.GetTypeDefinition(typeof(Machine));
// machine.ElementCollection == "Machine"
// machine.Properties[0].Name == "MachineId"
```

There's no real `DeserializeOptions`/`Deserializer` implementation shipped yet — a consumer
provides one (see `TestingDeserializeOptions` in tests for the minimal shape: a
`TypeDefinitionReflectiveInstantiator`, `PropertyDefinitionReflectiveInstantiator`, and an
`IDeserializerProvider` mapping types to `Deserializer`s). [`TypeDefinitionReflectionTests`](test/TypeDefinitionReflectionTests.cs)
and [`SchemaGuidedReadingTests`](test/SchemaGuidedReadingTests.cs) exercise this against
`TestingMachine`, a fake StanForD-shaped fixture standing in until a real `Forestry.StanForD`
project exists.

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
- **Schema path** (`TypeDefinition`, `PropertyDefinition`, `TypeDefinitionProvider`, `[Element]`/
  `[Collection]`): reflecting a schema class now works and is tested. Fixed to get there: `[Element]`
  was defined but never actually consulted (names came from the raw C# member name instead);
  the reflective-instantiator fallback for a not-yet-cached member type passed the wrong `Type`
  (the declaring type instead of the member's own type); and a type's self-referential property
  definition (`TypeDefinition.PropertyDefinition`) tripped an assertion in its own
  `ElementTypeDefinition` getter when configuring itself. Still open: the naming/ignore/
  include-field *policies* are inert defaults (nothing ignores properties or includes fields
  yet), the `DeserializeOptions` type-definition cache's options-equality comparer treats every
  options instance as equal, `Deserialization.Deserialize<T>(string, DeserializeOptions)` is an
  explicit not-yet-implemented stub, and element/attribute **position** within the document isn't
  modeled anywhere (`[Element]` only carries a name) — flagged by a pending test in
  `TypeDefinitionReflectionTests`. No actual value-construction step exists yet either (there's
  no `Deserializer.Deserialize(...)` that turns read `Value`s into a real `Machine` instance) —
  `SchemaGuidedReadingTests` only shows names being cross-checked, not object construction.
- `Forestry.Deserialize.Xml`: `ObjectDeserializer` predates the current `Deserializer` base shape
  in core and doesn't compile. Next real step here: a `Forestry.StanForD` project with C# types
  for the harvesting/forwarding/quality documents (no public .NET StanForD representation exists
  to build on), pulled into this project's tests to build a concrete `IReader` and
  `DeserializeOptions` against real schema types instead of `TestingMachine`.
- StanForD 2010 and StanForD Classic: no format-specific mapping yet — this section will
  document field/dimension mappings once a real XML `IReader` exists.
