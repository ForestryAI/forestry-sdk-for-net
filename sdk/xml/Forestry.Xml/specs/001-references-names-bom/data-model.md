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
(spec Key Entities). Checked by `ReferenceConstraint` (`internal`, tested as a unit, R8), owned by
`CharacterDataMarkup` or `LiteralMarkup`.

| Field | Type | Meaning |
|---|---|---|
| `_state` | `byte` enum | `None` (in text), `Ampersand`, `Hash`, `Decimal`, `Hex`, `HexMarker` (`&#x` seen), `EntityName` |
| `_codePoint` | `int` | value so far; never above `0x10FFFF` (checked per digit) |
| `_digits` | `int` | digits seen, leading zeros included (empty number check) |
| `_entityLength` | `int` | bytes of the entity name seen |
| `_entity` | `byte` enum | which predefined name is being matched: `None` until it is chosen, then `Ampersand`, `LessThan`, `GreaterThan`, `Quote` or `Apostrophe` (compared with `EBNF.AmpersandEntityName`, `LessThanEntityName`, `GreaterThanEntityName`, `QuoteEntityName`, `ApostropheEntityName`); `Other` only after a document type, for a name no predefined name accepts |
| `HasReferences` | `bool` | at least one complete reference in this Value |
| `IsDocumentType` | `bool` (init) | the reader's `_documentType`: entity names are scanned as whole names |
| `MaxNameLength` | `int` (init) | bound on an entity name when `IsDocumentType` |

**Transitions**:

```text
None --'&'--> Ampersand
Ampersand --'#'--> Hash
Ampersand --'l' | 'g' | 'q'--> EntityName         (_entity = LessThan | GreaterThan | Quote)
Ampersand --'a'--> EntityName                      (_entity = None: the second byte chooses)
EntityName --'m' | 'p' as 2nd byte after 'a'--> EntityName  (_entity = Ampersand | Apostrophe)
EntityName --byte == entity name[_entityLength]--> EntityName  (the chosen EBNF constant, byte by byte)
Ampersand | EntityName --any other byte--> no document type: Malformed (UndeclaredEntity) at this byte;
                                            with one: _entity = Other, scanned as NameChar,
                                            length > MaxNameLength -> Limit
Hash --'x'--> HexMarker
Hash --[0-9]--> Decimal
HexMarker --[0-9a-fA-F]--> Hex
Decimal --[0-9]--> Decimal | Hex --[0-9a-fA-F]--> Hex  (value > 0x10FFFF -> Malformed at this digit)
Decimal | Hex --';'--> None                        (not a legal Char -> Malformed at ';')
EntityName --';'--> None                           (only once the whole chosen constant has matched -> HasReferences;
                                                     before that, no document type: Malformed (UndeclaredEntity) at ';';
                                                     with one (Other, or a partial match like `&am;`):
                                                     NotSupported at '&')
Ampersand | Hash | HexMarker --';'--> Malformed    (empty reference)
any reference state --other byte--> Malformed      (missing ';', non-digit, 'X', '<', quote, spacing)
any reference state --end of span--> Unknown       (halt, or Incomplete when reading is completed)
```

**Validation**: FR-001, FR-005, FR-005a; positions in [research.md R2](research.md#r2-where-a-malformed-reference-is-reported).

## Markup structs (one evaluation)

| Struct | Change |
|---|---|
| `CharacterDataMarkup` | holds a `ReferenceConstraint`; `&` no longer ends character data; `<` ends it only outside a reference |
| `LiteralMarkup` | holds a `ReferenceConstraint`; `&` no longer malformed; the matching quote ends the literal only outside a reference |
| `NameMarkup` | new `MaxLength` init property; a name byte at index `MaxLength` is Malformed with reason "too long" |
| `ProcessingInstructionTargetConstraint` (renamed from `ProcessingInstructionTarget`) | an `IConstraint` (R8); new `MaxLength`; a target byte at that index is Malformed with reason "too long" |
| `NoConstraint` (new) | an `IConstraint` that is always well-formed: comment and document type |
| `DeclarationEncodingConstraint` (new) | an `IConstraint` (R8); scans the declaration for `encoding`, `Eq`, quote, name; `OffendingIndex` = the name's first byte when it is not `UTF-8` |

The `out bool unsupportedCharacter` of `IMarkup.Evaluate` becomes an `out` reason (`None`,
`UnsupportedCharacter`, `MalformedReference`, `UndeclaredEntity`, `NotSupportedEntity`, and `TooLong` from US2) so a delegate picks the category without another
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
| `Push(ReadOnlySpan<byte>)` → `TryPush` | packs, truncates | copies the whole name at the current end, records `_ends[_depth++]`; returns false and changes nothing when it does not fit (bytes past 2048, or `_depth` already 64), and `ReadStartTag` throws NotSupported + ElementStack (the stack has no position to report) |
| `TryPush(ReadOnlySequence<byte>)` | – | **new**: same, with `CopyTo` straight into `_names` (no `stackalloc`) |
| `TryPeek(name)` | compares packed 32-byte prefixes | lengths first, then `SequenceEqual` (span) or `SequenceReader<byte>.IsNext` (sequence); no copy |
| `TopLength` | – | **new**: `_ends[_depth - 1]` minus the previous end (0 for one name), the end tag's budget (FR-011) |
| `TryPop(name)` | peek then pop | unchanged contract, full comparison |
| `Pop()` | unpacks into a caller buffer | `_depth--` behind the empty-stack guard (`InvalidOperationException` when `_depth == 0`), returns nothing |
| `PackedNameLength` | 4 | removed; `NameBytesCapacity = 2048` |

Worked layout inside `<ab><Log><LogDiameter>`: `_names: "ab" "Log" "LogDiameter"`, `_ends: 2 5 16`,
`_depth: 3`; the top name is `_names[_ends[1] .. _ends[2]]` = `LogDiameter` ([research.md
R4](research.md#r4-names-compared-in-full-the-element-stack)).

**Invariants**: `_ends[0 .. _depth]` is strictly increasing and at most 2048; the top name is
`_names[_ends[_depth - 2] .. _ends[_depth - 1]]` (from 0 when `_depth == 1`); `0 <= _depth <= 64`,
and `Pop()` never takes `_depth` below 0; no name is longer than `MaxNameLength` (checked before `Push`).

## ReaderOptions (options)

| Option | Type | Default | Set | Constructors |
|---|---|---|---|---|
| `MaxDepth` | `int` | 64 | enforced: a start tag at `Depth == MaxDepth` throws Limit + Element (**new**); `<= 0` or above `ElementStack.NonAllocatingMaxDepth` (64) throws (**changed**: an explicit 0 used to mean 64) | a field still 0 (never set) → default |
| `MaxNameLength` | `int` | 128 | `<= 0` or above `ElementStack.NameBytesCapacity` (2048) throws (**new**) | a field still 0 (never set) → default |

`ReaderOptions` is a struct, so a field at 0 can only mean "never set": the setters reject an
explicit 0 and the constructors default an unset one, so `default(ReaderOptions)` keeps working
(research.md R5).

## Throwing (exceptions)

| Enum | New members |
|---|---|
| `Throwing.Category` | `NotSupported`, `Limit` |
| `Throwing.Markup` | `Reference`, `EntityReference`, `ByteOrderMark`, `Encoding`, `ElementStack` |

`ThrowUnsupportedNameCharacter` moves onto `NotSupported` + `Name`. See
[contracts/exceptions.md](contracts/exceptions.md).
