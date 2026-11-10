# Release History

## 1.0.0-beta.1 (Unreleased)

### Features Added
- The XML media for `Forestry.Deserialize`: `DeserializeXmlOptions` associates elements with row types
  and members by name or by `RowAttribute` and `FieldAttribute`, reading UTF-8 forward with
  `Forestry.Xml`'s `Utf8XmlReader` (spec 001 US1, #57).
- Rows from a string, memory, or asynchronously from a pipe or stream (spec 001 US3, #60).
- XML Schema booleans by default; a converter in the options wins over them.
