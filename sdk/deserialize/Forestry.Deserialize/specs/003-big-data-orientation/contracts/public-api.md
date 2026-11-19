# Public API changes: Big data orientation

Spec 003. Every change is breaking; nothing is released, so no obsolete members or type forwards
are kept (spec Assumptions). Each change is listed in its package's `CHANGELOG.md` under Breaking
Changes (FR-014).

## Forestry.Deserialize

### Removed

| Type or member | Instead | Research |
| :--- | :--- | :--- |
| `Forestry.Deserialize.Field` | `PropertyDefinition.ResolvedConverter`, `.Member`, `.MemberType`, `.Name` | R35 |
| `PropertyDefinition.Field` | The same members on `PropertyDefinition` | R35 |
| `Forestry.Deserialize.RowAttribute` | `Forestry.Deserialize.Xml.XmlElementNameAttribute` on the class | R37 |
| `Forestry.Deserialize.FieldAttribute` | `Forestry.Deserialize.Xml.XmlElementNameAttribute` on the member | R37 |

### Renamed or moved

| Before | After | Research |
| :--- | :--- | :--- |
| `Forestry.Deserialize.Reading.FieldContext` | `Forestry.Deserialize.ReadingInformation` | R36 |
| `Forestry.Deserialize.Reading.FieldContextExtensions` | `Forestry.Deserialize.Utf8Extensions` | R36 |
| `Forestry.Deserialize.Reading.MediaRowEnumerator` | `Forestry.Deserialize.Reading.FormatRowEnumerator` | R38 |
| `Forestry.Deserialize.Reading.MediaRowAsyncEnumerator` | `Forestry.Deserialize.Reading.FormatRowAsyncEnumerator` | R38 |
| `DeserializeOptions.MediaConverters` (protected) | `DeserializeOptions.FormatConverters` | R38 |
| `DeserializeOptions.CreateRowEnumerator` / `CreateRowAsyncEnumerator` | Same names; return the renamed enumerators | R38 |
| `PropertyDefinition.Converter` | `PropertyDefinition.ResolvedConverter` | R33 |
| `Row` in `src/Rows/Row.cs` | `Row` in `src/Row.cs`, same namespace | R39 |

### Changed behavior

| Where | Change | Research |
| :--- | :--- | :--- |
| Options lock (property definitions, gathered by `TypeDefinition`) | Throws one `InvalidOperationException` per row type naming every included member that is not a single value or has no converter | R32, R33 |

## Forestry.Deserialize.Xml

### Added

| Type or member | Research |
| :--- | :--- |
| `XmlElementNameAttribute(string name)`: class, property or field; name required | R37 |
| Lock failure: a member with two or more sources (attributes or `ElementText<TRow>`) | R37 |

### Renamed

| Before | After | Research |
| :--- | :--- | :--- |
| `XmlAttributeValueAttribute(string name)` | `XmlAttributeNameAttribute(string name)` | R37 |
| `XmlTextValueAttribute` | `XmlElementTextAttribute` | R37 |
| `DeserializeXmlOptions.TextValue<TRow>(string memberName)` | `DeserializeXmlOptions.ElementText<TRow>(string memberName)` | R37 |

## Forestry.StanForD (consumer, no public change)

`LogMeasurements` and `StemMetricParts` switch to `XmlAttributeName` and `XmlElementText`
(12 places, R40). Rows and values are unchanged.
