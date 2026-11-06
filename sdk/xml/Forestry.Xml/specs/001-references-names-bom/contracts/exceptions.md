# Contract: exceptions

**Spec**: [../spec.md](../spec.md) | **Research**: [R2](../research.md#r2-where-a-malformed-reference-is-reported), [R6](../research.md#r6-not-supported-and-limit-throws)

Every throw this spec adds, as the `XmlException.Category` / `Markup` pair a test asserts and the
byte its line and position point at (1-based, READER.md rule 17). Messages are placeholders until
#49; tests assert category, markup, line and position, not message text.

## Categories

| Category | Meaning | Template (placeholder) |
|---|---|---|
| `Incomplete`, `Expected`, `Name`, `Order`, `Unique` | malformed (unchanged) | unchanged |
| `NotSupported` (**new**) | well-formed, but this reader does not read it (C0) | `Not supported: {0}{1}` |
| `Limit` (**new**) | well-formed, but beyond a `ReaderOptions` limit (S1) | `Limit exceeded: {0}{1}` |

## Story 1: references

| Input (in content or a literal) | Category | Markup | Position at |
|---|---|---|---|
| `x & y` | Expected | Reference | the byte after `&` |
| `&amp<`, `&#65"` | Expected | Reference | `<`, `"` |
| `&;`, `&#;`, `&#x;` | Expected | Reference | `;` |
| `&#6A;`, `&#xG;`, `&#X41;` | Expected | Reference | `A`, `G`, `X` |
| `&nbsp;` (no document type) | Name | Reference | `n` |
| `&#x110000;` | Expected | Reference | the last `0` |
| `&#99999999999999999999;` | Expected | Reference | the 7th `9` (9,999,999 > 1,114,111) |
| `&#0;`, `&#xD800;`, `&#xFFFE;` | Expected | Reference | `;` |
| `&e;` after `<!DOCTYPE …>` | NotSupported | EntityReference | `&` |
| `<!DOCTYPE a [<!ENTITY e "x">]>` (an internal subset) | NotSupported | DocumentType | `[` |
| entity name over `MaxNameLength` after a document type | Limit | EntityReference | the first byte beyond |
| `<a>&amp` with reading completed | Incomplete | CharacterData | end of data |
| `<a b="&amp` with reading completed | Incomplete | AttributeValue | end of data |

## Story 2: names

| Input | Category | Markup | Position at |
|---|---|---|---|
| element or attribute name of `MaxNameLength + 1` bytes | Limit | Name | the byte at index `MaxNameLength` |
| PI target of `MaxNameLength + 1` bytes | Limit | ProcessingInstructionTarget | the byte at index `MaxNameLength` |
| `<abc></ab>`, `<ab></abc>`, names equal on 32 bytes then different | Name | EndTag | the end tag's name start (as today) |
| end tag longer than `MaxNameLength` | Name | EndTag | the end tag's name start |
| open names beyond the stack's 2048 bytes (the only NotSupported + ElementStack) | NotSupported | ElementStack | the start tag's name start |
| a start tag at `Depth == MaxDepth` (the 65th open element with the default 64) | Limit | Element | the start tag's name start |
| setting `MaxDepth` to 0, a negative value or above 64; `MaxNameLength` to 0, a negative value or above 2048 | `InvalidOperationException` (not an `XmlException`) | – | at the setter |

## Story 3: BOM and encoding

| Input | Category | Markup | Position at |
|---|---|---|---|
| `FE FF …`, `FF FE …`, `00 00 FE FF …` | NotSupported | ByteOrderMark | line 1, position 1 |
| `EF BB BF` only, or `EF BB` with reading completed | Incomplete | RootElement | end of data |
| `<?xml version="1.0" encoding="ISO-8859-1"?>` | NotSupported | Encoding | `I` |
| `encoding="UTF-16"` | NotSupported | Encoding | `U` |
| `EF BB BF` + `<?xml version="1.0" encoding="ISO-8859-1"?>` (the BOM contradicted) | Expected | Encoding | `I` (the BOM not counted) |
| `EF BB BF<a>&</a>` | Expected | Reference | the same line and position as without the BOM |

## Moved

| Throw | Before | After |
|---|---|---|
| non-ASCII name byte (`ThrowUnsupportedNameCharacter`) | no category | NotSupported + Name, same position |
| `&` in character data (`ReadContentCharacterData` S1) | Expected + CharacterData | removed (FR-007) |
| `&` in a literal (`ReadAttributeValue` S7) | Expected + AttributeValue | removed (FR-007) |
