# Forestry Xml

A forward-only, span-based XML reader: `Utf8XmlReader` reads UTF-8 bytes token by token, over one span
or a sequence of segments, without decoding to UTF-16 or allocating a string per value. When bytes run
out mid-token it stops without committing, and a new reader over more bytes continues from the saved
`ReaderState`, so a document can be read as it arrives from a pipe. StanForD harvester and forwarder
exports are the real documents it is developed against, but it targets XML generally. Parts of the
design are adapted from Microsoft's open-source .NET runtime; see
[THIRD-PARTY-NOTICES.md](../../../THIRD-PARTY-NOTICES.md).

Reading is in the namespace `Forestry.Xml.Reading`; what a later writer would share (`TokenType`,
`XmlException`, the grammar's terminals) is in `Forestry.Xml`. The reader stands on its own; [Forestry.Deserialize.Xml](../../deserialize/Forestry.Deserialize.Xml/README.md)
uses it to shred XML into rows.

```csharp
using Forestry.Xml.Reading;

ReadOnlySpan<byte> utf8 = """
    <?xml version="1.0"?>
    <Log id="7">
      <Species>Pine</Species>
    </Log>
    """u8;

Utf8XmlReader reader = new(utf8);

while (reader.Read())
{
    Console.WriteLine($"{reader.TokenType}: {reader.GetString()}");
}
```

prints

```text
Declaration: <?xml version="1.0"?>
Element: Log
Attribute: id
Value: 7
Element: Species
Value: Pine
ElementEnd: Species
ElementEnd: Log
```

`GetString()` gives every token's value and allocates a string on request. It throws
`InvalidOperationException` before the first token or when the value is not valid UTF-8; `TryGetString`
returns false instead. Character and entity references come back raw (`&amp;` stays `&amp;`), and
`reader.HasEscapedValue` says a value contains some. To avoid the string, read the bytes from
`reader.Value`, or `reader.ValueSequence` when `reader.HasValueSequence` says the value straddles
segments. Either way a value lives only until the next `Read()`.

## Try the preview

Preview packages are attached to this repository's
[GitHub releases](https://github.com/ForestryAI/forestry-sdk-for-net/releases), with no sign-in needed.
Download `Forestry.Xml.<version>.nupkg` from the latest `Forestry.Xml/v…` release into a folder, then:

```bash
dotnet nuget add source <folder> -n forestry-preview
dotnet add package Forestry.Xml --prerelease
```

## Status

Reads declarations, document types, comments, processing instructions, elements (including empty
ones), attributes and text, from one span or split across segments at any byte. Not yet: resolving
entity and character references, CDATA (throws), full comparison of names over 32 bytes, skipping, and
typed value getters beyond `GetString`, `TryGetString` and `TryGetBoolean`. The rules are in the reader's constitution (#64); how a `Read()` cycle works is in
[READER.md](READER.md).

## Tests

```bash
dotnet test test/Forestry.Xml.Tests.csproj
```

Slow performance tests (trait `Category=Performance`) are left out of a plain run; the gate at the end
of every user story runs them: `dotnet test test/Forestry.Xml.Tests.csproj -s test/Performance.runsettings`.
