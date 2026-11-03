# Contract: public API changes

**Spec**: [../spec.md](../spec.md) | **Data model**: [../data-model.md](../data-model.md)

What a caller of `Forestry.Xml` sees change. Everything else in the public surface stays as it is.
Each member gets an XML doc comment (D0); the wording below is the doc's substance, not final text.

## `Utf8XmlReader`

```csharp
namespace Forestry.Xml.Reading;

public ref partial struct Utf8XmlReader
{
    /// The current Value's raw bytes contain at least one character reference or predefined
    /// entity reference (&amp; &lt; &gt; &quot; &apos;), not resolved: the caller unescapes before
    /// using the value. False for every other token and for a Value without references. Like
    /// Value, it lives for one Read(). When true, every reference in the Value is well-formed.
    public bool HasEscapedValue { get; }
}
```

The behaviour follows `Utf8JsonReader.ValueIsEscaped` (the precedent the spec cites); the name
`HasEscapedValue` is the developer's ([research.md R3](../research.md#r3-the-references-flag)).

| Behaviour | Before | After |
|---|---|---|
| `&` in character data | text before it is a Value, then throws | text and references up to `<` are one Value; `HasEscapedValue` set |
| `&` in an attribute value | throws | the whole literal is the Value; `HasEscapedValue` set |
| End tag vs start tag | first 32 bytes compared | whole names compared |
| Name over `MaxNameLength` | read (truncated on the stack) | throws `Limit` at the first byte beyond |
| UTF-8 BOM at the start | throws "starts no markup" | skipped, not counted in line positions |
| UTF-16/32 BOM, non-UTF-8 `encoding` | misread or generic throw | throws `NotSupported` |
| `Read()` allocation | none per token | none per token (C2), references included |

## `ReaderOptions`

```csharp
namespace Forestry.Xml.Reading;

public struct ReaderOptions
{
    /// Largest name, in bytes, the reader reads: element names in start tags, attribute names,
    /// processing instruction targets, and entity names in references (only reached after a
    /// document type, since the five predefined names are short). A longer name throws where it is
    /// first read, at the first byte beyond the limit, before more of it is buffered. End tags are
    /// not bounded by it: an end tag longer than its start tag is a mismatch. Default 128 when
    /// never set; setting 0, a negative value or more than 2048 (the element stack's name bytes)
    /// throws InvalidOperationException.
    public int MaxNameLength { readonly get; set; }
}
```

`DefaultMaxNameLength = 128` is `internal const`, like `DefaultMaxDepth`.

`MaxDepth` changes too: setting 0 now throws `InvalidOperationException`, like a negative value
(it used to mean 64), and so does a value above 64 (the element stack's depth cap; a 65th open
element throws `NotSupported`). An unset `MaxDepth` still defaults to 64, so `default(ReaderOptions)` reads
as before. Unlike `JsonReaderOptions`, an explicit 0 is never "default" (research.md R5).

## `ReaderState`

No public change. A new internal field, `_isByteOrderMarkChecked`, is carried like the other
fields (READER.md rule 22); `ReaderOptions` already carries `MaxNameLength`.

## `XmlException`

No new members. `Category` gains two values a caller can test: `"NotSupported"` and `"Limit"` (see
[exceptions.md](exceptions.md)). The message wording stays #49's.

## Not changed

- `Extensions.Pack` / `Unpack` stay public and unchanged (whether `Extensions` stays public is out
  of scope); their doc no longer refers to the element stack.
- `GetString()`, `TryGetString()`, `TryGetBoolean()` return the raw bytes; unescaping getters are
  out of scope (C3).
- `TokenType`: no new token; a reference is never a token.
