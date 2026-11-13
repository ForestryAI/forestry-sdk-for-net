# Release History

## 1.0.0-beta.1 (Unreleased)

### Features Added
- The XML media for `Forestry.Deserialize`: `DeserializeXmlOptions` associates elements with row types
  and members by name or by `RowAttribute` and `FieldAttribute`, reading UTF-8 forward with
  `Forestry.Xml`'s `Utf8XmlReader` (spec 001 US1, #57).
- Rows from a string, memory, or asynchronously from a pipe or stream (spec 001 US3, #60).
- XML Schema booleans by default; a converter in the options wins over them.
- Attributes and element text as members, declared with this package's own attributes:
  `XmlAttributeValueAttribute` reads an attribute of the row's own element (never a child element of
  the same name), `XmlTextValueAttribute` or `DeserializeXmlOptions.TextValue<TRow>` the element's
  text (the last text wins; none sets the member empty). Two members on one attribute, or two text
  members, fail when the options lock (spec 002 US3, #102).
- Rows nested in rows, each element starting a row wherever it is; members stay direct children of
  their row's element, and a value inside a wrapper is reached by adding the wrapper as a row type
  whose policy sets the parent (spec 002 US2, #101).
