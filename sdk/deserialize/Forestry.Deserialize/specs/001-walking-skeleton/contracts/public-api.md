# Contract: Public API of the walking skeleton

Shapes from [research.md](../research.md). Names marked *Deferred (implementation)* there are
examples here, settled in code review (C5). Every public member gets an XML doc comment (D0, R9).

## Entry points (`Forestry.Deserialize`, `Deserialization`, R2, R3)

| Member | Input | Output | Spec |
|---|---|---|---|
| `Deserialize(string, DeserializeOptions)` | Text (converted to UTF-8) | `IEnumerable<Row>` (lazy) | US1, FR-001 |
| `Deserialize(ReadOnlyMemory<byte>, DeserializeOptions)` | UTF-8 bytes (`byte[]` converts implicitly) | `IEnumerable<Row>` (lazy) | US1, FR-001 |
| `DeserializeAsync(Stream, DeserializeOptions, CancellationToken)` | Stream (wrapped in a pipe) | `IAsyncEnumerable<Row>` | US3, FR-001, FR-010 |
| `DeserializeAsync(PipeReader, DeserializeOptions, CancellationToken)` | Pipe | `IAsyncEnumerable<Row>` | US3, FR-001, FR-010 |

No type argument and no span overload (R3). The row types come from the options (R12); the media is
chosen by the options type:

```csharp
var options = new DeserializeXmlOptions();
options.Rows.Add<Log>();                      // member name Deferred (implementation), R12
foreach (Row row in Deserialization.Deserialize(xml, options)) { ... }
```

Behavior:
- Before returning, the entry point builds the definitions and locks the options, so FR-008 errors
  throw at the call: no row type found; a row type not extending `Row`, with a non-built-in
  property, or (this slice) whose chosen constructor has parameters; no constructor can be chosen
  (R5).
- It gets the media's enumerator from the options and exposes it. Each returned enumerable can be
  enumerated once; a second `GetEnumerator()` / `GetAsyncEnumerator()` throws (R2).
- One forward pass (C2). A row is handed out when the media completes it (its end token), by default
  (FR-009, R7).
- An unconvertible value throws naming the member, its place and the class (FR-007). A document cut
  off inside a row throws from the media's reader (R7).

## Options (`DeserializeOptions`, R6, R7, R12, R14)

| Member | Behavior | Spec |
|---|---|---|
| Adding row types, e.g. `Rows.Add<T>()` (`T : Row`), chainable: `Rows.Add<Log>().Add<Stem>()` | Adds a row type; its rows are handed out | FR-001, FR-002, R12 |
| Adding several, e.g. `Rows.Add(params Type[] types)` | Same; a type not extending `Row` throws when added | FR-002, R12 |
| Constructor parameter types | Added automatically and transitively; their rows are not handed out unless added explicitly (spec 002/003) | R5, R12 |
| Policy registration for a type, property or parameter, and options-wide | Wins over attributes (R7 precedence) | US4, FR-012 |
| Converter registration | Converters by target type, with factories (R14) | FR-007 |
| Read-only once used | Any change after first use throws | US4, FR-013 |
| Media enumerators (abstract, supplied by the media's options) | The media's `RowEnumerator` / `RowAsyncEnumerator` subclasses, plus its default converters | C0, R2 |

`DeserializeXmlOptions` (in `Forestry.Deserialize.Xml`) extends it. On first use the options build a
fixed lookup of their row types (R5); it is per options, never global.

## Row and Field (`Forestry.Deserialize`, R4)

| Member | Behavior | Spec |
|---|---|---|
| `Type` | The row's derived class (`GetType()`) | US2, FR-006 |
| `AsType<T>()` | Checked cast to `T`; throws naming both classes if the row is not a `T` | US2, FR-006 |
| `Is<T>()` (name Deferred) | Whether the row is a `T` | US2 |
| Fields | Metadata per member (member info, media name, C# type, converter); per-occurrence metadata (depth, name, path, dimensions) only when a policy or option asks | R4 |

A row's values are the derived class's typed properties and fields (e.g. `Log.LogVolume` is a
`decimal`), set by the pipelines; a `Field` is never a value.

## Attributes (`Forestry.Deserialize`, R5, R8)

| Attribute (names Deferred) | On | Meaning |
|---|---|---|
| `[Row("Log")]` | Class | Row name; default: class name. Interpreted by the media for association |
| `[Field("LogKey")]` | Property or field | Member name; default: member name. Interpreted by the media |
| `[RowConstructor]` | Constructor (public or non-public) | The constructor to use; read by the core; more than one per class fails when definitions are built |
| Policy attributes | Class, property, parameter | Attach a policy to a pipeline slot (R7) |

## Media enumerators (core bases, implemented by media packages, R2)

| Member | Behavior |
|---|---|
| `RowEnumerator : IEnumerator<Row>` | Sync base over memory; writes `MoveNext`, `Current`, `Dispose`; `Reset()` throws |
| `RowAsyncEnumerator : IAsyncEnumerator<Row>` | Async base over a pipe; writes `MoveNextAsync`, `Current`, `DisposeAsync`, buffering and advancing |
| `protected abstract RowState Read(ReadOnlySequence<byte> bytes, bool isFinalBlock, out Row? row, out long consumed)` | The media's override, the same on both bases: reads its tokens, calls the `Pipeline` overloads, reports bytes consumed (even 0) |
| `RowState` | `Complete` (a row is completed; it becomes `Current`), `Partial` (more bytes needed), `None` (nothing more; the move returns `false`) |

The XML package supplies `XmlRowEnumerator` and `XmlRowAsyncEnumerator`, sharing one XML reading
class.

## Pipelines (core, called by the media's override, R2, R6, R7, R15)

| Call | Pipeline | Behavior |
|---|---|---|
| `Pipeline<T>() where T : Row` | Type | Constructs a `T` (constructor delegate; no parameters in this slice) and pushes it on the open-row stack; a type not in the options' row type lookup throws (S0) |
| `Pipeline(Type)` | Type | Non-generic form for simpler media; Deferred (implementation) |
| `Pipeline(PropertyInfo property, ReadOnlySpan<byte> utf8, in FieldContext context)`, `Pipeline(FieldInfo field, ReadOnlySpan<byte> utf8, in FieldContext context)` | Member | Converts `utf8` by the member's converter and sets it on the top open row; a member not of that row's type throws |
| `Pipeline()` | Type | Completes the top open row, hands it out (default), pops it; no open row throws |

`FieldContext` is a `readonly ref struct` with `Depth`, `Name` and `Path`; text members are UTF-8
byte spans, with extensions that turn them into strings when asked. Media dimensions (a
`Dimensions` member built from rented bytes, R15) are Deferred to the slice that needs them.

## Converters (core, R14; name "Converter" or "Deserializer" Deferred)

| Member | Behavior |
|---|---|
| `Converter<T>.Convert(ReadOnlySpan<byte> utf8)` | Converts UTF-8 text to `T`; fails fast (FR-007). Independent of any reader |
| Converter factory | Creates converters for families of types (e.g. `Nullable<T>`, enums) |
| Built-ins | One generic converter over `IUtf8SpanParsable<T>` (invariant culture), plus `string` and `bool` |

Each `PropertyDefinition<T>` resolves its converter by `T` once, from the options (options,
attributes, media defaults, built-ins).
