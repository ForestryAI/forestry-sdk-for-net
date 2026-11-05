# Release History

## 1.0.0-beta.1 (Unreleased)

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

### Behavior Changes
- `&` no longer throws: `GetString()` and the `Forestry.Deserialize.Xml` bridge above it now get
  raw `&amp;` (and other references) with no error unless the caller checks `HasEscapedValue`.
  Unescaping is a follow-up in the bridge (#65).
- Exception positions in content after a start tag are no longer one column short (#30's one-line
  fix).

### Breaking Changes
- A non-ASCII name byte throws an `XmlException` with `Category` "NotSupported" and `Markup` "Name"
  instead of one without a category (#65).
