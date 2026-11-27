# Data Model: JSON Lines

**Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

## Converters (Forestry.Serialization)

| Type | Kind | Holds / does | Validation |
|---|---|---|---|
| `EnumForm` | public enum | `Name`, `Number` (R59) | — |
| `EnumConverter<TEnum>` | public abstract, `Converter<TEnum>` | `Form`; reads and writes one form; protected `Members`, `TryGetValue`, `TryGetUtf8Name` (R59) | Unknown name or malformed number: `FormatException`. Undefined value in `Name` form: refused when written |
| `EnumFields<TEnum>` | internal static cache | Per member: value, C# name, name, UTF-8 name; name → value (ignoring case, span lookup); value → UTF-8 name | Names non-empty and unique ignoring case, else the options fail at lock (R62) |
| `EnumDefaultConverter<TEnum>` | internal sealed | `Name` form; created by `EnumConverterFactory` among the primitives | — |
| `DateOnlyConverter`, `TimeOnlyConverter`, `TimeSpanConverter` | internal sealed | Lenient invariant read, writer overload write (R61) | Malformed text: `FormatException` |
| `ConverterFactory` | public abstract (changed) | Adds `UsesMember` and `CreateConverter(type, member, options)` (R58) | — |
| `NullableConverterFactory` | internal (changed) | Member-aware: passes the member to the inner type's resolution | — |

## Attributes

| Attribute | Package | Target | Meaning |
|---|---|---|---|
| `EnumMemberNameAttribute(string name)` | Forestry.Serialization | enum field | The name the member is read from and written as, every format (R62) |
| `XmlEnumFormAttribute(EnumForm form)` | Forestry.Serialization.Xml | telemetry property or field | How XML documents write that member's enum; default `Name` (R64) |
| `JsonLinesEnumFormAttribute(EnumForm form)` | Forestry.Serialization.JsonLines | telemetry property or field | How lines write that member's enum; default `Name` (R65) |

## Converter resolution (R58)

```text
PropertyDefinition<T>  --ResolveConverter(typeof(T), member)-->  options
   1. developer's Converters   (StanForDEnumConverter<CuttingReason> wins here)
   2. format's FormatConverters (XmlEnumConverterFactory / JsonLinesEnumConverterFactory: UsesMember)
   3. primitives               (EnumConverterFactory: names; Date/Time/TimeSpan; Nullable (UsesMember); UTF-8 parsable)
   factory.UsesMember ? CreateConverter(type, member, options)   (not cached by type)
                      : CreateConverter(type, options)           (cached by type)
```

## Writer (Forestry.Serialization.Writing)

| Member | Change | Default |
|---|---|---|
| `WriteValue(DateOnly)` | new (R60) | `O` (`yyyy-MM-dd`) |
| `WriteValue(TimeOnly)` | new | `O` (`HH:mm:ss.fffffff`) |
| `WriteValue(TimeSpan)` | new | `c` |
| `WriteEnumName(ReadOnlySpan<byte>)` | new | the bytes |
| `GetSpan`, `GetMemory`, `Advance` | now `virtual` (R66) | `Output`'s |
| base defaults | write through `Output`, not `this` | unchanged output |

## XML format (Forestry.Serialization.Xml)

| Type | Kind | Does |
|---|---|---|
| `XmlEnumConverterFactory` / `XmlEnumConverter<TEnum>` | internal | Member-aware; `XmlEnumForm` or `Name`; one instance per enum type and form |
| `XmlTimeSpanConverter` | internal sealed | Reads `xs:duration` (`-`, `P`, `nD`, `T`, `nH`, `nM`, `n[.n]S`); `Y` or `M` before `T` fail |

## JSON Lines format (Forestry.Serialization.JsonLines)

| Type | Kind | Holds / does | Lifecycle |
|---|---|---|---|
| `SerializeJsonLinesOptions` | public sealed, `SerializeOptions` | `FormatConverters` (JSON Lines enum factory); JSON-encoded member names per type definition, by `PropertyDefinition.Index` | Mutable until first serialize; names built in `OnMakingReadOnly` |
| `JsonLinesTelemetryWriter` | internal sealed, `TelemetryWriter` | One `Utf8JsonWriter` (not indented, relaxed escaping, validated); the member being written, for messages | One per serialize call (`CreateWriter`); `Utf8JsonWriter` created on first `OnReset`, `Reset` after each line |
| `JsonLinesEnumConverterFactory` / `JsonLinesEnumConverter<TEnum>` | internal | Member-aware; `JsonLinesEnumForm` or `Name` | Per options |

Line framing: `{` → per member `"name":value` (or `"name":null`) → `}` → `Flush` → `\n` → `Reset`.

## StanForD (Forestry.StanForD)

| Type | Change |
|---|---|
| `StanForDEnumConverter<TEnum>` | Derives from `EnumConverter<TEnum>` (`Name`); `Read` adds the space-less, case-insensitive C# name fallback; `Write` from the base |
| `CuttingReason` | `[EnumMemberName]` on `GradeBreak`, `PulpwoodGrade`, `TopBreak`, `OtherManual` |
| `HarvestingOptionsExtensions` | Adds `AddHarvesting(this SerializeOptions)`: `Stem`, `Log`, StanForD's enum converters |
