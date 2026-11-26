# Release History

## 1.0.0-beta.1 (Unreleased)

### Breaking Changes
- Renamed from `Forestry.Deserialize` to `Forestry.Serialization` (and `Forestry.Deserialize.Xml` to `Forestry.Serialization.Xml`); the static `Deserialization` class is now `TelemetrySerialization`, and `DeserializationContext` is now `DeserializeContext` (#142). Entries below keep the names they were written with.
- `Row` is now `Telemetry`, and every name built on it follows (e.g. `ContextRow` -> `ContextTelemetry`, `RowTypes` -> `TelemetryTypes`, `TRow` -> `TTelemetry`), in code and in messages (#142).

### Features Added
- Flatten a document into rows in one forward pass: one instance of your own class per occurrence,
  handed out as soon as it is complete (spec 001 US1, #57).
- Work with rows without knowing their class, through `Row` (`Is<T>()`, `AsType<T>()`) (spec 001 US2,
  #58).
- Synchronous input over memory and strings, asynchronous input over a pipe or stream (spec 001 US3,
  #60).
- Policies: `DefinitionPolicy` chooses a row type's members and `EnumerationPolicy` acts as rows are
  built and complete (`Construct`, `Set`, `Complete`, `Clear`), attached by attribute or through
  `DeserializeOptions.Policies` (spec 001 US4, #59; reshaped by spec 002).
- The deserialization context (`DeserializationContext`), one per enumeration and passed to every
  enumeration policy method: rows are in it while open, and at completion policies decide, each at
  most once, whether a row comes out (`ContextRow.Enumerate`) and how long it stays
  (`ContextRow.Keep` with a `Lifetime`, `DeserializationContext.Release`); queries by row type
  (`Rows<TRow>()`), the set members of a row (`ContextRow.IsSet`), updates through
  `ContextRow.Set`, and a row both enumerated and kept handed out as a copy (spec 002 US1, #100).
- `DeserializeOptions.MaxContextRows` limits the rows kept in the context (default 100); going past
  it throws a `DeserializationContextLimitException` listing the kept rows per type (spec 002 US1,
  #100).
- Rows of several types nested in one pass, each recording its parent
  (`ContextRow.Container`, a `ContainerRelationship`), with `Parent`, `Ancestors`, `Children` and
  `Siblings` on the context (spec 002 US2, #101).
- Flat rows checked when the options lock: every member of a row type must be a single value (a
  primitive, `string`, an enum, or a struct that formats to UTF-8 and parses from text, or a nullable of
  one of these); a row type fails with one exception naming every member that is a nested structure,
  formats only to chars, or has no converter. Each `PropertyDefinition` resolves its own converter
  (spec 003 US1, #126).
- `ReadingInformation`: where a value is read in the document (depth, name, path), generic and in the
  top namespace, with `Utf8Extensions.ToUtf8String` (spec 003 US2, #127).

### Breaking Changes
All before this version's release (spec 003, #125):
- `Field` and `PropertyDefinition.Field` are removed; `PropertyDefinition.Converter` is
  `PropertyDefinition.ResolvedConverter`, with `Member`, `MemberType` and `Name` beside it (#126,
  #127).
- `RowAttribute` and `FieldAttribute` are removed: names in the document belong to the format package
  (`XmlElementNameAttribute` in Forestry.Deserialize.Xml) (#127).
- `Forestry.Deserialize.Reading.FieldContext` is `Forestry.Deserialize.ReadingInformation`, and
  `FieldContextExtensions` is `Forestry.Deserialize.Utf8Extensions` (#127).
- `MediaRowEnumerator`, `MediaRowAsyncEnumerator` and `DeserializeOptions.MediaConverters` are
  `FormatRowEnumerator`, `FormatRowAsyncEnumerator` and `FormatConverters` (#128).
- A row type with a member that is not a single value (e.g. a nested class with its own converter)
  now fails when the options lock (#126).

### Other Changes
- The policy surface changed before this version's release (spec 002): `EnumerationPolicy.Enumerate`
  became `Complete`, every method takes the deserialization context, and `next` passes what the
  step needs. Options with enumeration policies still serve one enumeration at a time.

### Bugs Fixed
- A failed `DeserializeOptions.MakeReadOnly()` remembered that a type had no converter, so adding the
  missing converter and calling it again still failed (#74).
