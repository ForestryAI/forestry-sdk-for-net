# Release History

## 1.0.0-beta.1 (Unreleased)

### Features Added
- `Utf8XmlReader`, `ReaderState`, `ReaderOptions` and `XmlException`, moved out of
  `Forestry.Deserialize.Xml` (namespace `Forestry.Deserialize.Xml.Reading`) into this package (namespace
  `Forestry.Xml`), so the reader can be used without deserialization to rows (#64).
