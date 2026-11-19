# Research: Big data orientation

Spec 003 (#125). Numbering continues from spec 002 (R20-R31). Every item is **Proposed** until the
developer marks it **Confirmed**, **Rejected** or **Deferred** (C5); `/speckit-tasks` waits until none is
Proposed. Constitution 3.1.0.

## R32. Recognizing a single value (Confirmed)

- **Status**: Confirmed (developer, 2026-11-19; the UTF-8 formatting requirement was the developer's question)
- **Question**: How does the package tell a single value (C7, FR-001) from anything else?
- **Decision**: A member type is a single value when, after unwrapping `Nullable<T>`, it is:
  1. a primitive (`Type.IsPrimitive`: `bool`, `char`, the integer and floating-point types), or
  2. `string`, or
  3. an enum, or
  4. a value type that:
     - **formats to UTF-8**: implements `IUtf8SpanFormattable` (required), and
     - **parses from text**: implements `IUtf8SpanParsable<TSelf>` or `ISpanParsable<TSelf>` (either).

     This covers `decimal`, `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`,
     `TimeSpan`, `Int128`, `Half` and a developer's own struct that implements both sides.
- **Rationale**:
  - **Shape, not conversion path**: the interfaces are only checked once per type, when the options
    lock. Nothing calls `Parse` or `TryFormat` through them at runtime, so the check adds no GC
    pressure whichever interfaces it names. Reading still goes through converters, which parse
    UTF-8 bytes directly (`Converter<T>.Convert(ReadOnlySpan<byte>)`).
  - **Formatting to UTF-8 is required for writing**: spec 004 writes rows as JSON Lines, and later
    Parquet. A writer that allocates no strings needs every member to format straight to UTF-8,
    which `IUtf8SpanFormattable` promises.
  - **Parsing may be either**: C7 says a single value "parses from and formats to a byte span or
    string". Requiring UTF-8 parsing would reject `DateTimeOffset`, which C7 names and the
    converters already read.
  - **No list of types**: the BCL's own contracts accept a developer's struct on the same terms as
    the BCL's.
- **Cost for developers**: a developer's own struct must implement `IUtf8SpanFormattable`, not just
  `ISpanFormattable`. That is the right bar for a package whose goal is getting values into big
  data formats without allocating (developer, 2026-11-19).
- **Verified** (2026-11-19, by reflection on the installed runtime):

  | Type | `ISpanParsable` | `ISpanFormattable` | `IUtf8SpanParsable` | `IUtf8SpanFormattable` |
  | :--- | :--- | :--- | :--- | :--- |
  | `bool` | yes | no | no | no |
  | `decimal`, `Guid` | yes | yes | yes | yes |
  | `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan` | yes | yes | no | yes |

  `bool` implements neither UTF-8 interface, which is why primitives are their own case.
- **Alternatives considered**:
  - *`ISpanParsable<TSelf>` and `ISpanFormattable`* (this item's first proposal): accepts the same
    BCL types, but promises nothing a UTF-8 writer can use, so a developer's struct could pass and
    then force a string per value when written.
  - *`IUtf8SpanParsable<TSelf>` and `IUtf8SpanFormattable`*: rejects `DateTime`,
    `DateTimeOffset`, `DateOnly`, `TimeOnly` and `TimeSpan`, which have no UTF-8 parsing.
  - *A fixed list of allowed types*: rejects a developer's own single-value struct, which the spec
    allows (edge cases).
  - *Any type with a converter*: rejected by FR-003; a converter can turn text into a composite
    object.
- **Known consequence**: a struct with several values that still has a text form (e.g.
  `System.Numerics.Complex`, `"<1; 2>"`) is accepted. It is one value in one column, written as
  text, which C7 allows.

## R33. Where and when the flat check runs: the property definition (Confirmed)

- **Status**: Confirmed (developer, 2026-11-19; moving the check to the property definition was the
  developer's idea)
- **Question**: Where is a member checked for being a single value (R32), and how does that sit
  with resolving its converter?
- **Decision**: the property definition owns both, as System.Text.Json's `JsonPropertyInfo` /
  `JsonPropertyInfo<T>` do (`Configure` calling the abstract `DetermineEffectiveConverter`). When a
  type definition is built, for each member a definition policy includes:
  1. **Single value (base, before creation)**: `PropertyDefinition` checks the member's type
     (R32). If it is not a single value, it reports the failure and nothing is created. This comes
     first because some member types cannot be a generic argument at all (e.g. a `Span<int>`
     property): `MakeGenericType` would throw its own, unhelpful `ArgumentException`.
  2. **Create**: `PropertyDefinition<T>` is created for the member and builds its setter, as today.
  3. **Configure**: the base's `Configure(options)` calls `private protected abstract
     ResolveConverter(options)`, which `PropertyDefinition<T>` implements to resolve its typed
     `Converter<T>` through `DeserializeOptions.ResolveConverter(Type)`, with no cast handed in from
     outside. If no converter handles the type, it reports the failure. `private protected` keeps
     format packages from overriding it.
  4. **Gather**: `TypeDefinition.Create` only decides inclusion (definition policies) and gathers
     the failures from steps 1 and 3 into one exception per row type.
- **Resolution stays in Forestry.Deserialize (developer, 2026-11-19)**: how a converter is resolved
  is self-contained in the deserialization package, driven by the options (and a future converter
  attribute, spec Clarifications). A format package takes no part in resolution; it only supplies
  default converters through `FormatConverters`, which the options consult after the developer's
  own (R38), as today.
- **Resolved converter (developer, 2026-11-19)**: the converter a property definition uses is its
  **resolved converter**, exposed as `ResolvedConverter` (today `Converter`) and set by
  `ResolveConverter(options)`. The package's earlier term "effective converter" is replaced: it
  was vague, and "resolve" is already the options' verb (`DeserializeOptions.ResolveConverter`).
- **Ignored members**: a member a definition policy leaves out never becomes a property definition,
  so it is never checked. A developer can keep a non-flat member on a class and exclude it.
- **One message per type**: the type definition throws once, naming the type and each failing
  member with its type and one of three reasons: a nested structure, a struct that formats only
  to chars, or no converter.
  This answers the spec's edge case "several failing members", and improves today's behavior,
  where a type with two members without converters fails one at a time.
- **Messages (developer, 2026-11-19)**: two new resources in `Formatting.Resource.cs`, beside
  today's `MemberWithoutConverter`, each saying exactly what is wrong:

  | Resource | When | Says |
  | :--- | :--- | :--- |
  | `MemberTypeIsNestedStructure` | Several values in one member: a class, another row type, an array, a collection, or a struct with no text form | The flat-row rule and the two fixes: copy the values onto the row as single values, or make it its own row type |
  | `MemberTypeNotUtf8Formattable` | A value type implementing `ISpanFormattable` without `IUtf8SpanFormattable`: one value that formats only to chars (R32) | The fix: implement `IUtf8SpanFormattable`, so rows can be written without a string per value |

  Every rejected type gets the first message except a value type implementing `ISpanFormattable`
  without `IUtf8SpanFormattable`, which gets the second. Both, and `MemberWithoutConverter`, are
  gathered into the one `InvalidOperationException` per row type. The first name was
  `MemberNotSingleValue`; the developer asked for more explicit names.
- **Later paths**: any future way to create property definitions (e.g. members configured through
  options, as spec 001 foresaw) gets the check and the resolution without remembering to call
  them.
- **Existing tests affected**:
  - `DefinitionValidationTests` asserts "converter" for `LogWithNestedProperty` (a nested
    `Measurement` class). It changes to assert `MemberTypeIsNestedStructure`'s message.
  - `ConverterResolutionTests` reads `PropertyDefinition.Converter`; it reads `ResolvedConverter`.
- **Alternatives considered**:
  - *The check in `TypeDefinition.Create`'s loop* (this item's first proposal): works only while
    that loop is the one path creating property definitions, and leaves the type definition
    resolving converters for members it does not own.
  - *The check inside `PropertyDefinition<T>`*: too late for member types `MakeGenericType`
    rejects.
  - *Checking at first enumeration*: later than needed; S1 wants failing before any byte.
  - *Throwing on the first failing member*: simpler, but a developer fixes one member at a time.

## R34. Complex (owned) members, spec 001 R16 (Confirmed: Rejected)

- **Status**: Confirmed (developer, 2026-11-19): owned members are rejected by C7
- **Question**: Spec 001 deferred "complex (owned) members": a member whose type is a nested class
  read from a structure, such as StanForD's coordinates or log measurements.
- **Decision**: Rejected by C7. A row's members are single values; a nested structure becomes
  members copied onto the row, or its own row type related through the deserialization context
  (spec 002). Forestry.StanForD already does this (`StemMetricParts`, `LogMeasurements`).
- **Rationale**: Owned members would give rows that no flat big data format accepts without a
  second flattening step after deserialization; C7 puts flattening in deserialization.
- **Record**: spec 001's research stays as written (history); this item supersedes R16.

## R35. Removing `Field` (Confirmed)

- **Status**: Confirmed (developer, 2026-11-19)
- **Decision**: `PropertyDefinition` holds its resolved converter itself, as `ResolvedConverter`
  (today `Converter`, forwarding to `Field.Converter`), set when it is configured (R33). The
  `Field` property and `src/Rows/Field.cs` are removed. `Field`'s other members already have a
  home on `PropertyDefinition`: `Member`, `MemberType` (`Field.Type`) and `Name`.
- **Rationale**: Nothing reads `Field` except `PropertyDefinition.Converter`. Its remarks promised
  format names that never moved there; names now belong to the format package (R37).
- **Alternatives considered**: keeping `Field` as a converter holder: one more type for one
  property, and the developer agreed the class goes (#125).

## R36. `ReadingInformation` (Confirmed)

- **Status**: Confirmed (developer, 2026-11-19); the name changed from `FormatInformation` the same
  day
- **Decision**:
  - `FieldContext` is renamed `ReadingInformation` and `FieldContextExtensions` `Utf8Extensions`.
  - Both move from `Forestry.Deserialize.Reading` to `Forestry.Deserialize`, with their files at
    the top of `src/` (FR-009).
  - Their members are unchanged: `Depth`, `Name`, `Path`. It stays a `readonly ref struct`, so it
    still costs nothing per value.
- **Parameter names**: parameters of this type are renamed from `context` to `information`
  (`RowBuilder.Set`, `PropertyDefinition.ReadAndSet`, the XML association). Spec 002 gave
  "context" to `DeserializationContext`, and two kinds of "context" in one signature confuse
  readers.
- **`Utf8Extensions` (developer, 2026-11-19)**: `FieldContextExtensions` holds one method,
  `ToUtf8String(this ReadOnlySpan<byte>)`. It extends a UTF-8 span (what `Name` and `Path` return),
  not the position itself, so it is named after what it extends. FR-009 changed to match (it first
  said `FormatInformationExtensions`).
- **Later, after the POC (developer, 2026-11-19)**: depth, name and path are generic reading
  information, not specific to one format. The developer wants them known when the options lock,
  as type and property definitions are built (e.g. each property definition knowing where its
  member is read), rather than only reported by the format per value. Not in this spec; recorded
  so the name and shape chosen here do not block it.
- **Why "reading" (developer, 2026-11-19)**: the name describes what it holds, where a value is
  read in the document, rather than who reports it today (the format). It stays true once the
  definitions know it at lock time. "Information" is the developer's suffix elsewhere. The type
  lives in the top namespace although `Forestry.Deserialize.Reading` exists: developers and
  policies use it, while `Reading` holds the machinery for format package authors (R38).
- **Alternatives considered**:
  - *`FormatInformation`* (this item's first proposal and #125's first wording): names who reports
    it today, which stops being true when the definitions know it.
  - *`ValueLocation`* and *`RowMemberContext`*, both from the #125 discussion.

## R37. The XML naming attributes and option (Confirmed)

- **Status**: Confirmed (developer, 2026-11-19)
- **Decision**:
  - **`XmlElementNameAttribute(string name)`**: on a class or a member (`AttributeTargets.Class |
    Property | Field`).
    - On a row type, it names the element its occurrences have, replacing `[Row("…")]`.
    - On a member, it names the child element its value comes from, replacing `[Field("…")]`.
    - The name is required: null or empty throws, as `XmlAttributeValue` does today. A missing
      attribute keeps the class or member name, so an empty one has no use.
  - **`XmlAttributeNameAttribute(string name)`**: renames `XmlAttributeValueAttribute`. Same
    targets and behavior.
  - **`XmlElementTextAttribute`**: renames `XmlTextValueAttribute`. Same targets and behavior: the
    text of the row's own element.
  - **The options**: `DeserializeXmlOptions.TextValue<TRow>(memberName)`, the options counterpart
    of the text attribute, is renamed `ElementText<TRow>(memberName)`. Options still win over
    attributes.
- **Reading**: `XmlAssociation` reads `XmlElementNameAttribute` where it reads `RowAttribute` and
  `FieldAttribute` today; it is their only reader.
- **Exclusive sources (FR-006a)**: a member with two or more of the three attributes fails when
  the options lock, with one new XML resource message naming the member and the attributes found.
  `ElementText<TRow>` set by options on a member carrying `XmlElementName` or `XmlAttributeName`
  fails the same way.
- **Messages**: the XML package's duplicate-name messages (today "give one a different [Field]
  name") name the new attributes.
- **Alternatives considered**: `XmlName` alone (option B before the family was named);
  `XmlElement`, `XmlAttribute`, `XmlText` (clash with System.Xml.Serialization, FR-006b).

## R38. "Media" to "format" in code (Confirmed)

- **Status**: Confirmed (developer, 2026-11-19)
| Today | After |
| :--- | :--- |
| `MediaRowEnumerator` | `FormatRowEnumerator` |
| `MediaRowAsyncEnumerator` | `FormatRowAsyncEnumerator` |
| `DeserializeOptions.MediaConverters` | `DeserializeOptions.FormatConverters` |
| `#region Media` in `DeserializeOptions.cs` | `#region Format` |
| `s_mediaConverters` in `DeserializeXmlOptions` | `s_formatConverters` |

- The enumerators stay in `Forestry.Deserialize.Reading`. The spec moves only `Row` and format
  information to the top, and the enumerators are for format package authors, not developers.
- XML doc comments and resource messages in both packages say "format", and "flatten" for
  "shred".

## R39. Moving `Row` (Confirmed)

- **Status**: Confirmed (developer, 2026-11-19)
- **Decision**: `src/Rows/Row.cs` moves to `src/Row.cs`; `src/Rows/` (then empty, after R35)
  is deleted. The namespace is already `Forestry.Deserialize`, so no caller changes.

## R40. Forestry.StanForD (Confirmed)

- **Status**: Confirmed (developer, 2026-11-19)
- **Finding**: StanForD references Forestry.Deserialize.Xml and uses `XmlAttributeValue` (8) and
  `XmlTextValue` (4) in `LogMeasurements.cs` and `StemMetricParts.cs`. After R37 it no longer
  builds without the renames.
- **Decision**: the 12 renames are a task of this spec, in the same change, so every package
  builds and tests at every commit. They are mechanical: StanForD's tests pass unchanged (SC-004),
  and StanForD's constitution is not touched by them (no rule is about attribute names).
  StanForD's documentation and constitution wording ("media", "shreds") stays with #118.
- **Spec corrected**: the spec's assumption said StanForD needed no code change; corrected during
  planning. #125's "Follow-on" should say the same.

## R41. Documentation scope (Confirmed)

- **Status**: Confirmed (developer, 2026-11-19)
- **Changed in this spec**:
  - Forestry.Deserialize: `README.md`, `ARCHITECTURE.md`, `CHANGELOG.md` (Unreleased section).
  - Forestry.Deserialize.Xml: `README.md`, `ARCHITECTURE.md`, `CHANGELOG.md`.
  - XML doc comments and resource messages in both packages' source.
  - The SDK `README.md`: "through a media" and "XML media".
- **Excluded from SC-003's search**:
  - specs 001 and 002 (history)
  - the constitution's amendment record
  - `sdk/deserialize/CLAUDE.md` (a superseded design note, kept as history)
  - `.claude/` (spec-kit's own tooling, which says "media" in its generic sense)
  - generated release notes under `artifacts/`
- **Released changelog entries**: none; both changelogs only have an Unreleased `1.0.0-beta.1`,
  so their wording is updated in place. The breaking changes are listed under that section's
  Breaking Changes (FR-014).
- **README content beyond words** (what a row is for a reader) belongs to #116; this spec
  only fixes words in the existing README, plus the row definition FR-004 asks for.

## R42. Tests (Confirmed)

- **Status**: Confirmed (developer, 2026-11-19)
- **Flat check (US1)**: in `Forestry.Deserialize.Tests` (`DefinitionValidationTests`), since
  definitions need no document (C3). One test per kind in SC-001: class, row type, array, list,
  dictionary, composite struct, composite type with a converter. One accepting test over every
  allowed kind (primitives, `string`, enum, `decimal`, `Guid`, `DateTimeOffset`, `DateOnly`,
  nullables, a developer's own struct implementing `IUtf8SpanFormattable` and
  `ISpanParsable<TSelf>`). One rejecting test for a developer's struct that formats only to chars
  (`ISpanFormattable` without `IUtf8SpanFormattable`). Plus one test where a definition policy
  excludes the non-flat member and the options lock.
- **XML attributes (US2)**: the existing fixtures move to the new attributes. New tests cover the
  exclusive-source failure (FR-006a) and a required, non-empty `XmlElementName`.
- **Fixture names**: fixtures named after "Field" (`LogWithRenamedField`, `LogWithDuplicateField`)
  are renamed after "member" (`LogWithRenamedMember`, `LogWithDuplicateMember`), so test names do
  not keep the removed term.
- **Renames (US3)**: no new behavior tests; the existing suites of the three packages are the
  check (SC-004). A public-surface check (SC-002) is a test in `Forestry.Deserialize.Tests` that
  reflects over the assembly's exported types and members for the removed names.
- **Test-first (C4)**: shells per story before code, reviewed in a board task. Each story's gate
  runs the performance tests (`Performance.runsettings`), as in specs 001 and 002.

## R43. Folders in Forestry.Deserialize.Xml (Confirmed)

- **Status**: Confirmed (developer, 2026-11-19; the developer's request while reading the plan)
- **Decision**: Forestry.Deserialize.Xml gets the same `Attributes/` and `Converters/` folders as
  Forestry.Deserialize, with the same namespace conventions:

  | Folder | Files | Namespace |
  | :--- | :--- | :--- |
  | `src/Attributes/` | `XmlElementNameAttribute.cs`, `XmlAttributeNameAttribute.cs`, `XmlElementTextAttribute.cs` (R37) | `Forestry.Deserialize.Xml`, unchanged, as Forestry.Deserialize's `Attributes/` keeps `Forestry.Deserialize` |
  | `src/Converters/` | `XmlBooleanConverter.cs` | `Forestry.Deserialize.Xml.Converters`, as Forestry.Deserialize's `Converters/` uses `Forestry.Deserialize.Converters` |

- **Rationale**: one layout across the core and its format packages, so a reader finds attributes
  and converters in the same place in each. Attributes keep the top namespace because developers
  write them on their row types: `using Forestry.Deserialize.Xml;` stays enough.
  `XmlBooleanConverter` is internal, so its new namespace changes no public surface.
- **Not moved**: options, association, row reading and enumerators stay at the top of `src/`; this
  item only adds the two folders that have a counterpart in Forestry.Deserialize.
