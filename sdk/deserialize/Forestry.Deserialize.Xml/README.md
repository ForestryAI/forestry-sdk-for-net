# Forestry Deserialize Xml

The XML media for [Forestry.Deserialize](../Forestry.Deserialize/README.md): `DeserializeXmlOptions`
chooses XML, and the span-based reader from [Forestry.Xml](../../xml/Forestry.Xml/README.md)
(`Utf8XmlReader`, not `System.Xml.XmlReader`) reads UTF-8 forward and this package feeds its tokens
to the core's pipelines, so each row is handed out as soon as its element ends.
StanForD harvester and forwarder exports are the real documents it is developed and tested against,
but the reader targets XML generally. Parts of the design are adapted from Microsoft's open-source
.NET runtime; see [THIRD-PARTY-NOTICES.md](../../../THIRD-PARTY-NOTICES.md).

```csharp
DeserializeXmlOptions options = new();
options.Rows.Add<Log>();

foreach (Row row in Deserialization.Deserialize(xml, options))
{
    Log log = (Log)row;
}
```

## How elements become rows

- An element named like a row type (its class name, or its `[Row]` name) starts a row, wherever it
  is in the document; its end tag completes the row and hands it out.
- A direct child element named like one of the row's members (or its `[Field]` name) sets that
  member from its text. An empty element sets `null`, or the type's default.
- Everything else is skipped. Paths, roots and nesting rules for matching come with later slices
  ([research R13](../Forestry.Deserialize/specs/001-walking-skeleton/research.md)).
- XML's default converters: `bool` accepts `true`, `false`, `1` and `0`, as XML Schema does.

## Status

Synchronous and asynchronous reading work (spec 001, user stories 1 and 3): the same rows from a
string, bytes, a `Stream` or a `PipeReader`, however the bytes are split. Character and entity
references are not resolved yet: a member's text keeps them as written (e.g. `&amp;`). The reader itself is specified in its own package, [Forestry.Xml](../../xml/Forestry.Xml/README.md).

## Tests

```bash
dotnet test test/Forestry.Deserialize.Xml.Tests.csproj
```

Slow performance tests (trait `Category=Performance`, e.g. the memory test over a million rows) are
left out of a plain run; the gate at the end of every user story runs them:
`dotnet test test/Forestry.Deserialize.Xml.Tests.csproj -s test/Performance.runsettings`.
