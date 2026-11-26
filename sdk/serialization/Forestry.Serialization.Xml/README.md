# Forestry Serialization Xml

The XML format for [Forestry.Serialization](../Forestry.Serialization/README.md): `DeserializeXmlOptions`
chooses XML, and the span-based reader from [Forestry.Xml](../../xml/Forestry.Xml/README.md)
(`Utf8XmlReader`, not `System.Xml.XmlReader`) reads UTF-8 forward and this package feeds its tokens
to the core's pipelines, so each telemetry is handed out as soon as its element ends.
StanForD harvester and forwarder exports are the real documents it is developed and tested against,
but the reader targets XML generally. Parts of the design are adapted from Microsoft's open-source
.NET runtime; see [THIRD-PARTY-NOTICES.md](../../../THIRD-PARTY-NOTICES.md).

On the path from a device document to big data, this package sits between the reader and the core:
Forestry.Xml tokenizes the bytes, this package matches elements and attributes to telemetry types and
members, and Forestry.Serialization builds the telemetries that feed a client's pipeline. The SDK's
[README](../../../README.md#big-data-clients) explains the whole path.

```csharp
DeserializeXmlOptions options = new();
options.Telemetries.Add<Log>();

foreach (Telemetry telemetry in TelemetrySerialization.Deserialize(xml, options))
{
    Log log = (Log)telemetry;
}
```

## Try the preview

Preview packages are attached to this repository's
[GitHub releases](https://github.com/ForestryAI/forestry-sdk-for-net/releases), with no sign-in needed.
Download `Forestry.Serialization.Xml.<version>.nupkg` from the latest `Forestry.Serialization.Xml/v…` release, and the same from the latest `Forestry.Xml/v…` and `Forestry.Serialization/v…` releases it depends on, into a folder, then:

```bash
dotnet nuget add source <folder> -n forestry-preview
dotnet add package Forestry.Serialization.Xml --prerelease
```

## Dependencies

- **.NET 10** (`net10.0`), the only target framework.
- **[Forestry.Serialization](../Forestry.Serialization/README.md)**: the format-agnostic core: telemetries,
  options, policies and converters.
- **[Forestry.Xml](../../xml/Forestry.Xml/README.md)**: the forward-only UTF-8 XML reader.
- **No third-party packages**, and nothing from `System.Xml`.

## How elements become telemetries

- An element named like a telemetry type (its class name, or its `[XmlElementName]` name) starts a telemetry,
  wherever it is in the document; its end tag completes the telemetry and hands it out.
- A direct child element named like one of the telemetry's members (or its `[XmlElementName]` name) sets
  that member from its text. An empty element sets `null`, or the type's default.
- Everything else is skipped. Paths, roots and nesting rules for matching come with later slices
  ([research R13](../Forestry.Serialization/specs/001-walking-skeleton/research.md)).
- XML's default converters: `bool` accepts `true`, `false`, `1` and `0`, as XML Schema does.

### Where a member comes from

Names in the document belong to the format, so this package declares them, with one family of
attributes named after the part of the document (in `Forestry.Serialization.Xml`):

| Attribute | On | Reads |
| :--- | :--- | :--- |
| `[XmlElementName("Tree")]` | a telemetry type | the element its occurrences have |
| `[XmlElementName("Volume")]` | a property or field | the child element its value comes from |
| `[XmlAttributeName("currency")]` | a property or field | an attribute of the telemetry's own element |
| `[XmlElementText]` | a property or field | the text of the telemetry's own element, never a child's |

```csharp
[XmlElementName("Price")]
public sealed class Price : Telemetry
{
    [XmlAttributeName("currency")]
    public string? Currency { get; set; }   // <Price currency="EUR">

    [XmlElementText]
    public decimal Amount { get; set; }     // <Price ...>0.59</Price>
}
```

- Without an attribute, a telemetry type is the element named like its class, and a member the child
  element named like the member.
- A member has one source: two of the three attributes on it fail when the options lock.
- `options.ElementText<TTelemetry>(memberName)` makes a member the text, as `[XmlElementText]` does, and
  wins over the attribute.
- Two telemetry types or two members read from the same name fail when the options lock, naming the
  attribute to change.

## Status

Synchronous and asynchronous reading work (spec 001, user stories 1 and 3): the same telemetries from a
string, bytes, a `Stream` or a `PipeReader`, however the bytes are split. Attributes and element text
as members came with spec 002, and their attribute names, `[XmlElementName]` replacing the core's
`[Telemetry]` and `[Field]`, with spec 003. Character and entity references are not resolved yet: a member's
text keeps them as written (e.g. `&amp;`). The reader itself is specified in its own package,
[Forestry.Xml](../../xml/Forestry.Xml/README.md).

## Tests

```bash
dotnet test test/Forestry.Serialization.Xml.Tests.csproj
```

Slow performance tests (trait `Category=Performance`, e.g. the memory test over a million telemetries) are
left out of a plain run; the gate at the end of every user story runs them:
`dotnet test test/Forestry.Serialization.Xml.Tests.csproj -s test/Performance.runsettings`.
