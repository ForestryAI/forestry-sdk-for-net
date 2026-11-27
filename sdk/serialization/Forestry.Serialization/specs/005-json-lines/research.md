# Research: JSON Lines

**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-11-27

Numbering continues from spec 004 (R44-R57). Each decision is a proposal until the developer
confirms it (C5).

## R58 - Choosing a converter per member

- **Decision**: `ConverterFactory` gains an overridable member-aware creation:
  `public virtual Converter CreateConverter(Type type, MemberInfo member, TelemetrySerializationOptions options)`,
  defaulting to the existing `CreateConverter(type, options)`, and `public virtual bool UsesMember => false`.
  `TelemetrySerializationOptions.ResolveConverter(Type type, MemberInfo? member)` keeps today's
  precedence (the developer's converters, then the format's, then the primitives, R14). When the
  converter found is a factory that `UsesMember`, it is asked per member and its result is not
  cached by type; every other result is cached by type as today. `PropertyDefinition<T>` passes
  its member; the object converter (`TypeDefinition<T>`) passes none. `NullableConverterFactory`
  becomes member-aware so that `XmlEnumForm`/`JsonLinesEnumForm` on a `CuttingReason?` member
  reach the inner enum converter.
- **Rationale**: Clarification Q4: format attributes decorate the telemetry member, and two members
  of the same enum type can differ, so the type alone can no longer pick the converter. Keeping
  the precedence means a developer's registered converter (StanForD's) still wins (FR-002e). A
  factory deciding for itself whether it needs the member keeps the cache for the common case.
- **Alternatives considered**: A cache keyed by type and member for everything (simpler, but every
  member creates its own converter); a core `[Converter(typeof(...))]` attribute like
  System.Text.Json's `JsonConverterAttribute` (rejected by Q4: per-format attributes); passing the
  `PropertyDefinition` instead of the `MemberInfo` (it is not built yet when the converter is
  resolved, and a factory only needs the member's attributes).

## R59 - The base enum converter

- **Decision**: In `Forestry.Serialization.Converters`, a public `enum EnumForm { Name, Number }` and
  `public abstract class EnumConverter<TEnum> : Converter<TEnum> where TEnum : struct, Enum`:
  - `protected EnumConverter(EnumForm form)`, `public EnumForm Form { get; }`.
  - The enum's fields are resolved once per `TEnum` (a static generic cache, internal): each
    member's value, C# name, name (from `[EnumMemberName]`, R62, or the C# name), its UTF-8 name,
    a value → UTF-8 name lookup, and a name → value lookup ignoring case (a
    `Dictionary<string, TEnum>` with `StringComparer.OrdinalIgnoreCase` and its
    `GetAlternateLookup<ReadOnlySpan<char>>()`, so reading does not allocate).
  - Subclasses get them through protected members: `Members` (value, C# name, name),
    `TryGetValue(ReadOnlySpan<char> name, out TEnum value)` and
    `TryGetUtf8Name(TEnum value, out ReadOnlySpan<byte> utf8Name)`.
  - `Read` (overridable): `Name` decodes the UTF-8 into a stack buffer and looks the name up
    ignoring case; a `[Flags]` enum also reads comma-separated names. `Number` parses the
    underlying integer type. Anything else is a `FormatException` naming the text and the enum.
  - `Write` (overridable): `Name` writes `writer.WriteEnumName(utf8Name)` (R60); a `[Flags]`
    combination writes its names joined with `", "`. `Number` writes `writer.WriteValue(long)`
    (`ulong` above `long.MaxValue` through `WriteValue(decimal)`, exact and unquoted).
  - **An undefined value in `Name` form is refused** with an exception naming the value and the
    enum, like a NaN in JSON (FR-011a): a value the form cannot represent.
- **Built-in**: an internal `EnumConverterFactory` among the primitives creates an internal
  `EnumDefaultConverter<TEnum>` in `Name` form (FR-002: without a format's enum converter, names).
- **Rationale**: The developer asked for a base abstraction resolving the fields once, as
  System.Text.Json's `EnumConverter<T>` does with its name caches, but with the form given at
  creation instead of taken from a token kind, which XML has none of (clarification Q3).
- **Spec change on confirmation**: the spec's edge case "an undefined value … written as its number"
  contradicts US4's "a number fails" in `Name` form; refusing undefined values removes the
  contradiction. The edge case is rewritten once this is confirmed.
- **Alternatives considered**: `Name` form also reading numbers (STJ's `AllowNumbers`): guesses the
  form from the text, which Q3 ruled out. `Enum.Parse`/`ToString` per value: allocates and cannot
  use member-name attributes.

## R60 - New TelemetryWriter value methods

- **Decision**: `TelemetryWriter` gains `virtual WriteValue(DateOnly)` (default `O`, `yyyy-MM-dd`),
  `virtual WriteValue(TimeOnly)` (default `O`, `HH:mm:ss.fffffff`), `virtual WriteValue(TimeSpan)`
  (default `c`, `[-][d.]hh:mm:ss[.fffffff]`) and `virtual WriteEnumName(ReadOnlySpan<byte> utf8Name)`
  (default: the bytes as they are). Enum numbers use the integer overloads.
- **Rationale**: Constitution 4.0.1 lists "dates and times, enums" among the kinds a format decides
  (C0). An enum name is not text in general (it comes from a fixed set, and Parquet may
  dictionary-encode it), so it gets its own method; it cannot be a `WriteValue` overload because
  `WriteValue(ReadOnlySpan<char>)` already takes text and a byte span would be confused with
  `WriteRaw`. Enum numbers are integers to every format.
- **Alternatives considered**: `WriteValue<TEnum>(TEnum)` (the writer would have to resolve names
  itself, duplicating R59); routing names through `WriteValue(ReadOnlySpan<char>)` (forces a
  UTF-8 → UTF-16 → UTF-8 round trip and loses the "this is an enum" signal).

## R61 - Built-in date, time and duration converters

- **Decision**: Internal `DateOnlyConverter`, `TimeOnlyConverter` and `TimeSpanConverter` in
  `Converters/Primitive`, listed before `Utf8SpanParsableConverterFactory`. `Read` decodes UTF-8 into
  a stack buffer and calls `TryParse` with the invariant culture (accepting `2026-11-27`,
  `13:20:00`, `13:20:00.5`, and `TimeSpan`'s `c` and `g` forms), throwing a `FormatException` like
  the other converters; `Write` calls the writer's overload (R60).
- **Rationale**: FR-001/FR-003. Lenient reading accepts what XML Schema's `xs:date` and `xs:time`
  write (fractions optional); strict round-trip writing (`O`, `c`) keeps every tick.
- **Alternatives considered**: `TryParseExact` with `O` (rejects `13:20:00`, which XML documents
  use); the generic UTF-8 parsable factory (these types do not all implement
  `IUtf8SpanParsable<T>`).

## R62 - The enum member-name attribute

- **Decision**: `[EnumMemberName(string name)]` in namespace `Forestry.Serialization`
  (`src/Attributes/`), on enum fields. Names must be non-empty and unique ignoring case within the
  enum; otherwise resolving the enum's fields fails, which happens while definitions are built, so
  the options fail at lock naming the enum and the duplicate (S1).
- **Rationale**: Clarification Q1. A5 allows it: it names a value, not a part of a document or an
  output, and it applies in every format and both directions, like System.Text.Json's
  `JsonStringEnumMemberName`.

## R63 - StanForD's enum converter

- **Decision**: `StanForDEnumConverter<TEnum>` derives from `EnumConverter<TEnum>` in `Name` form.
  `Read` first looks the text up by name (base), then, with spaces removed, by C# name ignoring case
  (today's behavior, so "OtherManual" and "other manual" still read). `Write` is the base's: the
  declared StanForD text. `CuttingReason` members get `[EnumMemberName]` where the text differs from
  the C# name ("Grade break", "Pulpwood grade", "Top break", "Other manual").
  `ProcessingCategory` (documents write `SingleTreeProcessing`, the C# name) and
  `CoordinateReferenceSystem.WGS84` need none.
- **Serialize options for StanForD**: a new `AddHarvesting(this SerializeOptions options)` extension
  adds `Stem` and `Log` and StanForD's enum converters to any format's serialize options, the
  writing counterpart of `AddHarvesting(this DeserializeXmlOptions)` (FR-007).
- **Rationale**: Clarification Q2 (keep a tolerant StanForD converter); registering it in the options
  makes it win over any format's enum converter (R58, FR-002e).

## R64 - The XML enum and duration converters

- **Decision**: In Forestry.Serialization.Xml:
  - `[XmlEnumForm(EnumForm form)]` on properties and fields (`src/Attributes/`).
  - An internal, member-aware `XmlEnumConverterFactory` (R58) in `FormatConverters` beside
    `XmlBooleanConverter`, creating an internal `XmlEnumConverter<TEnum>` (`EnumConverter<TEnum>`)
    in the member's form, `Name` without the attribute; one instance per enum type and form.
  - An internal `XmlTimeSpanConverter` reading `xs:duration` with its own parser over the UTF-8
    bytes: optional `-`, `P`, optional `nD`, optional `T` with `nH`, `nM`, `n[.n]S`; at least one
    part; years and months (`Y`, or `M` before `T`) fail with a format error (FR-003a). `Write`
    delegates to the writer (core constant form).
- **Rationale**: Clarifications Q3 and Q4 and the `XmlBooleanConverter` pattern. Not named
  `XmlEnumAttribute`, which `System.Xml.Serialization` already uses for member names. An own parser
  avoids allocating and refuses years and months, which `XmlConvert.ToTimeSpan` approximates.

## R65 - The JSON Lines package

- **Decision**: `sdk/serialization/Forestry.Serialization.JsonLines/`, laid out like the XML package
  (`.slnx`, `Directory.Build.props` with `IsShippingLibrary`, `src/`, `test/`, README, CHANGELOG,
  ARCHITECTURE), version `1.0.0-beta.1`, referencing only Forestry.Serialization (System.Text.Json is
  in the shared framework, A1). Namespace `Forestry.Serialization.JsonLines`:
  - `public sealed class SerializeJsonLinesOptions : SerializeOptions` (named like
    `DeserializeXmlOptions`): `FormatConverters` holds the JSON Lines enum converter factory;
    `OnMakingReadOnly` builds, per type definition, the JSON-encoded member names
    (`JsonEncodedText[]` by `PropertyDefinition.Index`) once; `CreateWriter` returns a new writer.
  - `[JsonLinesEnumForm(EnumForm form)]` on properties and fields, with an internal member-aware
    `JsonLinesEnumConverterFactory` and `JsonLinesEnumConverter<TEnum>` (as R64).
  - An internal sealed `JsonLinesTelemetryWriter : TelemetryWriter` keeping one `Utf8JsonWriter`
    (`Indented = false`, `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, validation on),
    created on the first `OnReset` and `Reset` to `Output` on every later one.
- **Writer mapping**: `WriteStartTelemetry` → `WriteStartObject`; `WriteName` →
  `WritePropertyName(names[index])` (remembering the member for messages); `WriteNull` →
  `WriteNull(names[index])`; text → `WriteStringValue`; `bool` → `WriteBooleanValue`; `int`, `long`,
  `decimal` → `WriteNumberValue`; `double` → `WriteNumberValue` after checking `double.IsFinite`,
  else a `NotSupportedException` naming the member and the value (FR-011a); `Guid`, `DateTime`,
  `DateTimeOffset` → their `WriteStringValue` overloads; `DateOnly`, `TimeOnly`, `TimeSpan` and the
  generic `WriteValue<T>` → formatted (`O`, `O`, `c`, invariant) into a stack buffer, then
  `WriteStringValue(utf8)`; `WriteEnumName` → `WriteStringValue(utf8)`; `WriteRaw` →
  `WriteRawValue(utf8)` (validated: a complete JSON value); `WriteEndTelemetry` →
  `WriteEndObject`, `Flush`, a `\n` written to `Output`, `Reset(Output)`.
- **Rationale**: Clarifications on the writer and escaping. `Utf8JsonWriter` owns commas, escaping
  and validation; `Reset` after each object lets the next line be another top-level value; flushing
  per telemetry keeps the order of bytes right and costs one `Advance`.
- **Alternatives considered**: writing the bytes ourselves with `JavaScriptEncoder` (option c,
  declined); a JSON array document (not JSON Lines).

## R66 - Refusing direct IBufferWriter use in JSON Lines

- **Decision**: In the core `TelemetryWriter`, `GetSpan`, `GetMemory` and `Advance` become `virtual`,
  and the base's own defaults (`WriteValue(ReadOnlySpan<char>)`, `WriteRaw`, `WriteFormatted`)
  write through `Output` instead of `this`, so overriding the three never changes the defaults. The
  JSON Lines writer overrides the three to throw a `NotSupportedException` telling the converter to
  use the value methods (FR-011b).
- **Rationale**: Option (b). Today the three are non-virtual and write straight to `Output`, past
  `Utf8JsonWriter`'s buffer, so a format cannot refuse them without this small core change. Making
  them virtual is also the door to option (a) later.
- **Alternatives considered**: a `protected virtual bool AllowsDirectWrites` checked by the base
  (one more concept); leaving them working and documenting the risk (bytes out of order, silently).

## R67 - Tests, the round trip and CI

- **Decision**:
  - Core (`Forestry.Serialization.Tests`): new converters, enum forms, member-name attribute,
    member-aware resolution and the new writer methods, through the string test format (C3); the
    string test format gains `WriteEnumName` and the three new overloads with their defaults.
  - XML (`Forestry.Serialization.Xml.Tests`): `XmlEnumForm` and `xs:duration` over hand-written
    documents (A4).
  - JSON Lines (`Forestry.Serialization.JsonLines.Tests`, new): every member kind, escaping, nulls,
    NaN, enum forms, `Stream`/`PipeWriter` equality, collection writer count, direct `IBufferWriter`
    refusal; lines parsed with `JsonDocument`.
  - StanForD (`Forestry.StanForD.Tests`): enum texts both ways and the round trip (US5) on a
    committed fragment and, with `[RealDocumentFact]`, on real documents. The test project (not the
    package) references Forestry.Serialization.JsonLines. Each parsed line is compared member by
    member through the type definition's properties: strings, booleans and numbers by value
    (`decimal` via `GetDecimal`), dates, times, durations and enums by reading the JSON string with
    the same converters, nulls as JSON `null`.
  - CI: `Forestry.Serialization.JsonLines` added to the `build-test` and `pack` matrices in
    `.github/workflows/ci.yml` and to `release.yml`'s package list and folder switch. Not to the
    `performance` matrix yet: writing performance work comes after the round trip (C6).
    The developer adds `build-test (Forestry.Serialization.JsonLines)` to the main ruleset's
    required checks.
- **Rationale**: C3 (each format tests its own writer), C6 (the round trip in a project combining
  XML and JSON Lines), C4 (shells first per story).
