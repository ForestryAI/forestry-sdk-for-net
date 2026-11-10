# Release History

## 1.0.0-beta.2 (2026-11-10)

### Behavior Changes
- `Utf8XmlReader.GetString()` and `TryGetString()` work on every token, following #28: the text of a
  Value, the name of an element, attribute or end tag, the whole markup of a declaration, document
  type, comment or processing instruction, and an empty string for `/>`. They used to throw on
  anything but a Value.
- `TryGetString()` and `TryGetBoolean()` return false instead of throwing: `TryGetString()` before
  the first token or for invalid UTF-8, `TryGetBoolean()` on any token but a Value.

### Breaking Changes
- `Utf8XmlReader.GetString()` returns a decoded `string` instead of the raw `ReadOnlySpan<byte>`
  (#28). The bytes are still in `Value` and `ValueSequence`. It throws an `InvalidOperationException`
  with a message before the first token or when the value is not valid UTF-8.

## 1.0.0-beta.1 (2026-11-10)

### Features Added
- `Utf8XmlReader`, `ReaderState`, `ReaderOptions` and `XmlException`, moved out of
  `Forestry.Deserialize.Xml` (namespace `Forestry.Deserialize.Xml.Reading`) into this package, so the reader can be used without deserialization to rows (#64). Reading is in
  the namespace `Forestry.Xml.Reading`; `TokenType`, `XmlException` and the grammar's terminals, which a
  later writer would share, are in `Forestry.Xml`.
- Character references and the five predefined entity references in character data and attribute
  values are read as part of the Value, raw, and `Utf8XmlReader.HasEscapedValue` says the Value
  contains some (spec 001 US1, #65). A malformed reference throws at its first known byte; an
  entity other than the five after a document type, and a document type's internal subset, throw
  "not supported".
- `ReaderOptions.MaxNameLength` (default 128 bytes) bounds element names, attribute names,
  processing instruction targets and entity names after a document type; a longer name throws an
  `XmlException` with `Category` "Limit" (spec 001 US2, #66).
- A UTF-8 byte order mark at the start of a document is skipped without counting it in line
  positions; a UTF-16 or UTF-32 one throws "not supported"; a declaration naming an encoding other
  than UTF-8 (in any letter case) throws "not supported", or malformed ("Expected") when a UTF-8
  byte order mark contradicts it (spec 001 US3, #67, superseding #31).

### Behavior Changes
- `&` no longer throws: `GetString()` and the `Forestry.Deserialize.Xml` bridge above it now get
  raw `&amp;` (and other references) with no error unless the caller checks `HasEscapedValue`.
  Unescaping is a follow-up in the bridge (#65).
- Exception positions in content after a start tag are no longer one column short (#30's one-line
  fix).

### Breaking Changes
- End tags are compared with the whole start-tag name, not its first 32 bytes: a document whose
  names differ only after 32 bytes now throws a mismatch (#66).
- `ReaderOptions.MaxDepth` is enforced: a start tag at that depth throws an `XmlException` with
  `Category` "Limit"; setting it to 0 (which used to mean 64) or above 64 now throws
  `InvalidOperationException` (#66).
- A non-ASCII name byte throws an `XmlException` with `Category` "NotSupported" and `Markup` "Name"
  instead of one without a category (#65).
