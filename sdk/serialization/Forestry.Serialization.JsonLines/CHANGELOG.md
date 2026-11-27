# Release History

## 1.0.0-beta.1 (Unreleased)

### Features Added
- Enums written as their names (or `[EnumMemberName]`), or as numbers with `[JsonLinesEnumForm(EnumForm.Number)]` on the member; text escaped only as JSON requires (non-ASCII stays plain UTF-8); a NaN or infinite `double` refused, naming the member (spec 005 US3, #151).
- The JSON Lines format for `Forestry.Serialization`: `SerializeJsonLinesOptions` writes each telemetry as one JSON object on its own line (spec 005 US3, #151).
