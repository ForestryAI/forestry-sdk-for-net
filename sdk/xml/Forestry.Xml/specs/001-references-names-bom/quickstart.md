# Quickstart: validating spec 001

**Spec**: [spec.md](spec.md) | **Contracts**: [contracts/](contracts/)

How to prove each story works once it is built. Run from `sdk/xml/Forestry.Xml/`. Test names are
given by file; the shells are written and reviewed first (C4).

## Prerequisites

- .NET 9 and .NET 10 SDKs (A0); the SDK's shared `Directory.Build.props` above this folder (A3).
- No third-party packages beyond the test project's xunit (A1).

## Every story

```bash
dotnet build Forestry.Xml.slnx
```

```bash
dotnet test Forestry.Xml.slnx
```

```bash
dotnet test Forestry.Xml.slnx -s test/Performance.runsettings
```

Expected: the build has no missing-XML-doc warnings for new public members (D0); the default run
passes with performance tests left out; the performance run passes (SC-005).

## 1. References (US1)

Tests: `test/Utf8XmlReaderTests.ReadReferences.cs`, updated `ReadCharacterData` and
`ReadAttributeValue`.

| Check | Document | Expected |
|---|---|---|
| text with a reference | `<a>Spruce &amp; pine</a>` | Element `a`, Value `Spruce &amp; pine` with `HasEscapedValue`, ElementEnd `a` |
| text without | `<a>Spruce and pine</a>` | `HasEscapedValue` false |
| attribute value | `<a b="&lt;&#60;&#x3C;"/>` | Value `&lt;&#60;&#x3C;`, flagged |
| each reference alone | the eight of scenario 4, as text and as a value | one flagged Value each |
| split inside a reference | `<a>x &am` + `p; y</a>` | halts, nothing committed; with both parts, the whole document's tokens |
| malformed / not supported | [exceptions.md, Story 1](contracts/exceptions.md#story-1-references) | category, markup, line and position as listed |

Every document is also read split at every byte and in parts (SC-001). Allocation:
`test/ReadAllocationTests.cs` (Performance) shows no more bytes allocated with references than
without (SC-003).

## 2. Names (US2)

Tests: `test/Utf8XmlReaderTests.ReadNameLength.cs`, updated `ReadEndTag`, `ReadStartTag`,
`ElementNameStackTests`.

| Check | Document | Expected |
|---|---|---|
| 35-byte name | `<ButtEndProfileExtrapolationFunction>1</ButtEndProfileExtrapolationFunction>` | Element, Value, ElementEnd with the full name |
| equal on 32 bytes | `…FunctionX>` closed by `…FunctionY>` | Name + EndTag at the end tag |
| prefix / extension | `<abc></ab>`, `<ab></abc>` | Name + EndTag |
| at and beyond the limit | names of 32, 33, 128, 129 bytes; `MaxNameLength = 16` with 17 | read up to the limit, Limit + Name one byte beyond |
| long end tag | an end tag of 129 bytes | Name + EndTag, never Limit |

## 3. BOM and encoding (US3)

Tests: `test/Utf8XmlReaderTests.ByteOrderMark.cs`, `test/Utf8XmlReaderTests.DeclarationEncoding.cs`.

| Check | Document | Expected |
|---|---|---|
| UTF-8 BOM | `EF BB BF` + `<?xml version="1.0"?><a/>` | the same tokens as without; the declaration still first |
| positions unchanged | `EF BB BF` + `<a>&</a>` | the same line and position as without |
| split BOM | `EF` + `BB BF<a/>`, `EF BB` + `BF<a/>` | halts until complete, then the same tokens |
| other BOMs, encodings | [exceptions.md, Story 3](contracts/exceptions.md#story-3-bom-and-encoding) | NotSupported at the listed byte |
| UTF-8 names | `encoding="UTF-8"`, `"utf-8"`, no encoding | read |

## End to end (SC-004)

A hand-written StanForD-shaped document in the tests: a UTF-8 BOM, a declaration with
`encoding="UTF-8"`, `ButtEndProfileExtrapolationFunction`, and `&amp;` in a text field, read to the
end with default options, whole and split at every byte.
