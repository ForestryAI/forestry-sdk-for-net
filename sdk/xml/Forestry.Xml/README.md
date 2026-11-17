# Forestry Xml

`Utf8XmlReader` is a forward-only XML reader over UTF-8 bytes. It reads token by token from one span
or a sequence of segments, without decoding to UTF-16 or allocating a string per value. When the bytes
run out in the middle of a token it stops without committing, and a new reader over more bytes
continues from the saved `ReaderState`, so a document can be read as it arrives from a pipe.

It is part of the [Forestry SDK for .NET](../../../README.md). StanForD harvester and forwarder
documents are the real documents it is developed against, but it targets XML generally. Parts of the
design are adapted from Microsoft's open-source .NET runtime; see
[THIRD-PARTY-NOTICES.md](../../../THIRD-PARTY-NOTICES.md).

## Why not System.Xml?

.NET already reads XML: `XmlSerializer` into classes, `XDocument` into a tree, `XmlReader` forward.
Each turns every value into a UTF-16 string. `XmlReader` also blocks a thread on a `Stream`, and the
others hold the whole document in memory. That is fine for small documents into classes. It is not for
this SDK's goal: turning StanForD documents, which come daily from every machine and hold every stem
and log, into flat rows for big data. There the cost per value decides ingestion speed.

`Utf8XmlReader` is to XML what `Utf8JsonReader` is to JSON: it reads UTF-8 bytes in place, allocates
nothing per value, and resumes when more bytes arrive from a pipe, so no thread waits. If you want
small XML documents as classes, `XmlSerializer` is the better fit.

Forestry.Xml only reads. No writer is planned while the SDK's goal is getting forestry metrics into big
data formats.

## Phase and status

**Phase:** POC · **Status:** Partial — reads well-formed UTF-8 documents such as StanForD 2010; the
gaps below throw or are left to the caller.

**Supported**: the XML declaration, document type declarations (without an internal subset), comments,
processing instructions, elements (including empty ones), attributes and character data, from one span
or split across segments at any byte. Character references and the five predefined entity references
are read, checked and returned raw, with `HasEscapedValue` set. A UTF-8 byte order mark is skipped.

**Not yet supported**, with the section of the [XML 1.0 specification](https://www.w3.org/TR/xml/)
each concerns:

| Gap | XML 1.0 | Today |
| :--- | :--- | :--- |
| Resolving references in values | [4.1](https://www.w3.org/TR/xml/#sec-references), [4.6](https://www.w3.org/TR/xml/#sec-predefined-ent) | Returned raw: `&amp;` stays `&amp;`; `HasEscapedValue` says a value has some |
| CDATA sections | [2.7](https://www.w3.org/TR/xml/#sec-cdata-sect) | Throws |
| A document type's internal subset, and entities other than the five predefined | [2.8](https://www.w3.org/TR/xml/#sec-prolog-dtd), [4.2](https://www.w3.org/TR/xml/#sec-entity-decl) | Throws not supported |
| Encodings other than UTF-8 | [4.3.3](https://www.w3.org/TR/xml/#charencoding) | Throws not supported |
| Non-ASCII names | [2.3](https://www.w3.org/TR/xml/#NT-Name) | Throws not supported |
| A lone carriage return as a line end | [2.11](https://www.w3.org/TR/xml/#sec-line-ends) | Not handled |
| `--` in a comment, `]]>` in character data, a second document type | [2.5](https://www.w3.org/TR/xml/#sec-comments), [2.4](https://www.w3.org/TR/xml/#syntax), [2.8](https://www.w3.org/TR/xml/#sec-prolog-dtd) | Not rejected |
| Typed getters (converters) | – | Only `GetString`, `TryGetString` and `TryGetBoolean` |
| Skipping an element and its content | – | Not yet |
| A limit on value length | – | Not yet; names (`MaxNameLength`, 128 bytes) and depth (`MaxDepth`, 64) are limited |

Line positions in exceptions count bytes, not characters.

## Example

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
`InvalidOperationException` before the first token or when the value is not valid UTF-8;
`TryGetString` returns false instead. To avoid the string, read the bytes from `reader.Value`, or
`reader.ValueSequence` when `reader.HasValueSequence` says the value straddles segments. Either way a
value lives only until the next `Read()`.

Reading is in the namespace `Forestry.Xml.Reading`. Types not specific to reading (`TokenType`,
`XmlException`, the grammar's terminals) are in `Forestry.Xml`.

## Install the preview

Preview packages are attached to this repository's
[GitHub releases](https://github.com/ForestryAI/forestry-sdk-for-net/releases), with no sign-in needed.
Download `Forestry.Xml.<version>.nupkg` from the latest `Forestry.Xml/v…` release into a folder, then:

```bash
dotnet nuget add source <folder> -n forestry-preview
dotnet add package Forestry.Xml --prerelease
```

## Design

- [READER.md](READER.md): how a `Read()` cycle works, and the decisions behind it
- [READ-TABLE.md](READ-TABLE.md): which token each state reads next
- [Constitution](.specify/memory/constitution.md): the rules no design decision may override

## Tests

```bash
dotnet test test/Forestry.Xml.Tests.csproj
```

Slow performance tests (trait `Category=Performance`) are left out of a plain run; the gate at the end
of every user story runs them:

```bash
dotnet test test/Forestry.Xml.Tests.csproj -s test/Performance.runsettings
```
