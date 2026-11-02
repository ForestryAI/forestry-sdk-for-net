# Forestry Deserialize

Forestry Deserialize reads large documents in one forward pass and shreds them into flat rows: one
instance of your own class per occurrence in the document, handed out as soon as it is complete.
Nothing keeps a row once it is handed out, so a document of a million rows costs about the same
memory as one of a thousand. The need comes from StanForD XML from harvesting and forwarding
machines, which is the real data this is developed and tested against, but neither this package nor
`Forestry.Deserialize.Xml` is limited to StanForD.

The design is specified with spec-kit: the rules are in the
[constitution](.specify/memory/constitution.md) and the current slice is
[spec 001, walking skeleton](specs/001-walking-skeleton/spec.md), with its decisions in
[research.md](specs/001-walking-skeleton/research.md). Some of the design is adapted from
Microsoft's open-source .NET runtime; see [THIRD-PARTY-NOTICES.md](../../../THIRD-PARTY-NOTICES.md).

## Packages

- **Forestry.Deserialize**: the media-agnostic core: rows, options, type definitions, converters and
  the pipelines that build rows. It knows no media.
- **Forestry.Deserialize.Xml**: the XML media. Its options (`DeserializeXmlOptions`) choose XML and
  its reader feeds the core.

## Shredding a document into rows

A row class extends `Row`. Its public properties and fields are set from the document by name:

```csharp
public sealed class Log : Row
{
    public int LogKey { get; set; }

    public decimal LogVolume { get; set; }
}
```

Add the row types to the media's options, then deserialize. Every `<Log>` element becomes one `Log`
row, in document order; elements and members that match nothing are ignored.

```csharp
DeserializeXmlOptions options = new();
options.Rows.Add<Log>();

foreach (Row row in Deserialization.Deserialize(xml, options))
{
    Log log = (Log)row;
    // use log.LogKey and log.LogVolume, then let it go
}
```

`Deserialize` takes a `string` or UTF-8 bytes (`ReadOnlyMemory<byte>`, so a `byte[]` too) and
returns a lazy `IEnumerable<Row>` that can be enumerated once. Rows are classes, so a plain cast
gives the typed row.

### Names, constructors and converters

- **Names**: by default a row matches an element named like its class and a member an element named
  like the member. `[Row("Tree")]` on the class and `[Field("Volume")]` on a property or field use
  the document's name instead.
- **Constructor**: a row type needs exactly one public constructor, or exactly one constructor
  marked `[RowConstructor]`, whatever its accessibility. In this slice it takes no parameters.
- **Converters**: each member's text is converted to its type once, invariant culture: text,
  booleans, integers, decimals, `DateTime`, `DateTimeOffset`, `Guid` and the other types parsable
  from UTF-8, plus their `Nullable<T>`. A media adds its own defaults (XML reads `1` and `0` as
  booleans). Your own `Converter<T>` added to `options.Converters` wins over both.
- **Empty elements**: `<LogVolume />` sets `null` where the member allows it, otherwise the type's
  default.

### Failing fast

- Before any byte is read, at the `Deserialize` call: no row type added, a member whose type no
  converter handles, no constructor that can be chosen, a chosen constructor with parameters, and
  two row types or two members read from the same element.
- While reading: a value that cannot be converted throws a `FormatException` naming the member, its
  path in the document (e.g. `Root/Log/LogKey`) and the class. A document cut off inside a row
  throws, and that row is never handed out.

## Status

Spec 001's first user story is done: the synchronous entry points above. Still to come in this
slice ([tasks.md](specs/001-walking-skeleton/tasks.md)):

- `Row.AsType<T>()`, a checked cast naming both classes when it fails (US2).
- Policies attached to types and members, by attribute or by options (US4).
- Asynchronous entry points over a `Stream` or `PipeReader`, reading without holding the whole
  document (US3).

Later slices add several row types in one pass and rows that take their ancestors (e.g. a `Log`
built with its `Stem`).

## Tests

```bash
dotnet test test/Forestry.Deserialize.Tests.csproj
```

The core's tests need no document: definitions, constructor choice and converter resolution.
Shredding is tested through the real XML reader in `Forestry.Deserialize.Xml`. Slow performance
tests (trait `Category=Performance`) are left out of a plain `dotnet test` and run as the gate at
the end of every user story: `dotnet test -s Performance.runsettings`.
