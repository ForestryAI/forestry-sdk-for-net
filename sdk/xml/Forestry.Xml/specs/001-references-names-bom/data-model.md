# Data model: References, names compared in full, BOM

**Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md) | **Date**: 2026-11-03

The reader has no persisted data; its "entities" are the state it keeps for one `Read()` and the
state it carries from one reader to the next (`ReaderState`). This page lists what this spec adds or
changes, with the lifetime of each piece, because lifetime is what C1 and READER.md rules 22–23 turn
on.

## Lifetimes

| Lifetime | Where | Rule |
|---|---|---|
| One evaluation | an `IMarkup` struct on the stack | re-created when a halted cycle is read again |
| One `Read()` | a reader field cleared in step 0 | READER.md rule 23 |
| Across readers | a `ReaderState` field, copied by both constructors | READER.md rule 22 |
| Options | `ReaderOptions`, inside `ReaderState` | set by the caller, defaulted by the constructors |

## Reference (one evaluation)

A character reference or one of the five predefined entity references inside a Value; never a token
(spec Key Entities). Scanned by `ReferenceScanner`, owned by `CharacterDataMarkup` or
`LiteralMarkup`.

| Field | Type | Meaning |
|---|---|---|
| `_state` | `byte` enum | `None` (in text), `Ampersand`, `Hash`, `Decimal`, `Hex`, `HexMarker` (`&#x` seen), `EntityName` |
| `_codePoint` | `int` | value so far; never above `0x10FFFF` (checked per digit) |
| `_digits` | `int` | digits seen, leading zeros included (empty number check) |
| `_entityLength` | `int` | bytes of the entity name seen |
| `_entityCandidates` | `byte` flags | which of `amp`, `lt`, `gt`, `quot`, `apos` still match (no document type) |
| `HasReferences` | `bool` | at least one complete reference in this Value |
| `IsDocumentType` | `bool` (init) | the reader's `_documentType`: entity names are scanned as whole names |
| `MaxNameLength` | `int` (init) | bound on an entity name when `IsDocumentType` |

**Transitions**:

```text
None --'&'--> Ampersand
Ampersand --'#'--> Hash
Ampersand --NameStartChar--> EntityName            (no document type: must match a predefined name)
Hash --'x'--> HexMarker
Hash --[0-9]--> Decimal
HexMarker --[0-9a-fA-F]--> Hex
Decimal --[0-9]--> Decimal | Hex --[0-9a-fA-F]--> Hex  (value > 0x10FFFF -> Malformed at this digit)
EntityName --NameChar--> EntityName                (no document type: no predefined name continues -> Malformed;
                                                     with one: length > MaxNameLength -> Limit)
Decimal | Hex --';'--> None                        (not a legal Char -> Malformed at ';')
EntityName --';'--> None                           (predefined -> HasReferences;
                                                     with a document type and not predefined -> NotSupported at '&')
Ampersand | Hash | HexMarker --';'--> Malformed    (empty reference)
any reference state --other byte--> Malformed      (missing ';', non-digit, 'X', '<', quote, spacing)
any reference state --end of span--> Unknown       (halt, or Incomplete when reading is completed)
```

**Validation**: FR-001, FR-005, FR-005a; positions in [research.md R2](research.md#r2-where-a-malformed-reference-is-reported).

## Markup structs (one evaluation)

| Struct | Change |
|---|---|
| `CharacterDataMarkup` | holds a `ReferenceScanner`; `&` no longer ends character data; `<` ends it only outside a reference |
| `LiteralMarkup` | holds a `ReferenceScanner`; `&` no longer malformed; the matching quote ends the literal only outside a reference |
| `NameMarkup` | new `MaxLength` init property; a name byte at index `MaxLength` is Malformed with reason "too long" |
| `ProcessingInstructionTarget` | new `MaxLength`; a target byte at that index is Malformed with reason "too long" |
| `DeclarationEncoding` (new) | scans the declaration for `encoding`, `Eq`, quote, name; `OffendingIndex` = the name's first byte when it is not `UTF-8` |

The `out bool unsupportedCharacter` of `IMarkup.Evaluate` becomes an `out` reason (`None`,
`UnsupportedCharacter`, `TooLong`, `NotSupported`) so a delegate picks the category without another
scan. The span paths (`_isMultipleSegments == false`) call the same structs.

## Reader fields

| Field | Lifetime | Change |
|---|---|---|
| `HasEscapedValue` | one `Read()` | **new**, public get; cleared in step 0, set by `ReadContentCharacterData` and `ReadAttributeValue` from `HasReferences` |
| `_isByteOrderMarkChecked` | across readers | **new** `ReaderState` field (rule 22's six places); set once the Skip BOM step has decided |
| `_elementStack` | across readers | layout changes (below) |
| `_readerOptions` | options | gains `MaxNameLength` |

## ElementStack (across readers)

| Member | Today | After |
|---|---|---|
| storage | `ulong[64 × 4]` packed names, 32 bytes each, truncated | `_names`: `[InlineArray(2048)] byte`, names end to end; `_ends`: `[InlineArray(64)] ushort`, end offsets; `_depth` |
| `Push(ReadOnlySpan<byte>)` | packs, truncates | copies the whole name at the current end, records `_ends[_depth++]`; throws NotSupported + ElementStack when it does not fit |
| `Push(ReadOnlySequence<byte>)` | – | **new**: same, with `CopyTo` straight into `_names` (no `stackalloc`) |
| `TryPeek(name)` | compares packed 32-byte prefixes | lengths first, then `SequenceEqual` (span) or `SequenceReader<byte>.IsNext` (sequence); no copy |
| `TopLength` | – | **new**: `_ends[_depth - 1]` minus the previous end (0 for one name), the end tag's budget (FR-011) |
| `TryPop(name)` | peek then pop | unchanged contract, full comparison |
| `Pop()` | unpacks into a caller buffer | `_depth--`, returns nothing |
| `PackedNameLength` | 4 | removed; `NameBytesCapacity = 2048` |

Worked layout inside `<ab><Log><LogDiameter>`: `_names: "ab" "Log" "LogDiameter"`, `_ends: 2 5 16`,
`_depth: 3`; the top name is `_names[_ends[1] .. _ends[2]]` = `LogDiameter` ([research.md
R4](research.md#r4-names-compared-in-full-the-element-stack)).

**Invariants**: `_ends[0 .. _depth]` is strictly increasing and at most 2048; the top name is
`_names[_ends[_depth - 2] .. _ends[_depth - 1]]` (from 0 when `_depth == 1`); `_depth <= 64`; no
name is longer than `MaxNameLength` (checked before `Push`).

## ReaderOptions (options)

| Option | Type | Default | Set | Constructors |
|---|---|---|---|---|
| `MaxDepth` | `int` | 64 | negative throws | `<= 0` → default (unchanged) |
| `MaxNameLength` | `int` | 128 | negative throws | `<= 0` → default (**new**, same pattern) |

## Throwing (exceptions)

| Enum | New members |
|---|---|
| `Throwing.Category` | `NotSupported`, `Limit` |
| `Throwing.Markup` | `Reference`, `EntityReference`, `ByteOrderMark`, `Encoding`, `ElementStack` |

`ThrowUnsupportedNameCharacter` moves onto `NotSupported` + `Name`. See
[contracts/exceptions.md](contracts/exceptions.md).
