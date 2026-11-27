# Release History

## 1.0.0-beta.1 (Unreleased)

### Breaking Changes
- Renamed from `Forestry.Deserialize` to `Forestry.Serialization` (and `Forestry.Deserialize.Xml` to `Forestry.Serialization.Xml`); the static `Deserialization` class is now `TelemetrySerialization`, and `DeserializationContext` is now `DeserializeContext` (#142). Entries below keep the names they were written with.
- `Row` is now `Telemetry`, and every name built on it follows (e.g. `ContextRow` -> `ContextTelemetry`, `RowTypes` -> `TelemetryTypes`, `TRow` -> `TTelemetry`), in code and in messages (#142).
- `XmlBooleanConverter` overrides `Read` (was `Convert`), following the core's converters (spec 004, #144).

### Features Added
- Enums read as names (ignoring case) by default, or as numbers with `[XmlEnumForm(EnumForm.Number)]` on the member; the other form fails with a format error (spec 005 US4, #152).
- `TimeSpan` members read an XML Schema duration (`xs:duration`, e.g. `PT1H30M`) exactly; years, months, fractions finer than a tick and durations beyond `TimeSpan` fail (spec 005 US4, #152).
- The XML format for `Forestry.Deserialize`: `DeserializeXmlOptions` associates elements with row types
  and members by name or by `XmlElementNameAttribute`, reading UTF-8 forward with
  `Forestry.Xml`'s `Utf8XmlReader` (spec 001 US1, #57).
- Rows from a string, memory, or asynchronously from a pipe or stream (spec 001 US3, #60).
- XML Schema booleans by default; a converter in the options wins over them.
- Attributes and element text as members, declared with this package's own attributes:
  `XmlAttributeNameAttribute` reads an attribute of the row's own element (never a child element of
  the same name), `XmlElementTextAttribute` or `DeserializeXmlOptions.ElementText<TRow>` the element's
  own text (the last text wins; none sets the member empty). Two members on one attribute, or two text
  members, fail when the options lock (spec 002 US3, #102).
- Rows nested in rows, each element starting a row wherever it is; members stay direct children of
  their row's element, and a value inside a wrapper is reached by adding the wrapper as a row type
  whose policy sets the parent (spec 002 US2, #101).
- One family of attributes for names in the document, named after the part they read:
  `XmlElementNameAttribute` (a row type's element, or a member's child element),
  `XmlAttributeNameAttribute` and `XmlElementTextAttribute`, in `Attributes/`; a member with more than
  one source fails when the options lock (spec 003 US2, #127).

### Breaking Changes
All before this version's release (spec 003, #125):
- `XmlAttributeValueAttribute` is `XmlAttributeNameAttribute`, `XmlTextValueAttribute` is
  `XmlElementTextAttribute`, and `DeserializeXmlOptions.TextValue<TRow>` is `ElementText<TRow>` (#127).
- Names in the document are declared with `XmlElementNameAttribute`, replacing Forestry.Deserialize's
  removed `RowAttribute` and `FieldAttribute` (#127).
- `XmlElementNameAttribute` and `XmlAttributeNameAttribute` require a name (#127).
