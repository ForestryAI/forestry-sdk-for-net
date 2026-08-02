# Forestry Deserialize

Forestry Deserialize reads large documents — the primary case today is StanForD XML from
harvesting and forwarding machines — and streams data out rather than loading a whole file into
memory. A multi-gigabyte harvester log should cost roughly the same, memory-wise, to process as a
small one.

For how the package is put together internally — the walk, the buffering model, the split between
this package and a concrete media provider — see [ARCHITECTURE.md](ARCHITECTURE.md). For the
in-progress design history behind the current shape, see [CLAUDE.md](CLAUDE.md).

## Packages

- **Forestry.Deserialize** — the media-agnostic core. Install this to describe a document's shape
  with `[Element]`/`[Collection]` and drive it against a concrete reader.
- **Forestry.Deserialize.Xml** — StanForD XML support. Not usable yet — see Status below.

## Describing a document's shape

A schema class tells a reader what to expect — which elements/attributes, and eventually in what
order — so a mismatch between the schema and the actual media can be caught while streaming.

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

`[Collection("Machine")]` names a class for when it appears as an item in a list. `[Element(...)]`
names a property explicitly, independent of the C# member name.

## Status

**There is no working end-to-end read yet.** Schema reflection (the sample above) works today.
Actually streaming `Value`s out of a real document does not — the walk that would drive it is
still being built (`Deserializer<T>`'s graph/node update is unimplemented), and
`Forestry.Deserialize.Xml` does not currently compile. See ARCHITECTURE.md §5 for the full list of
what's missing, and CLAUDE.md for why each gap exists and what's already been decided about
closing it.

Until then, [`TypeDefinitionReflectionTests`](test/TypeDefinitionReflectionTests.cs) and
[`SchemaGuidedReadingTests`](test/SchemaGuidedReadingTests.cs) are the best reference for what
actually works, exercised against `TestingMachine`, a fake StanForD-shaped fixture standing in
until a real `Forestry.StanForD` project exists.
