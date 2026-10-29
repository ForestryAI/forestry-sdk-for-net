# Research: Walking skeleton

Every item below is a **proposal** (constitution C5) unless marked **Confirmed** by the developer.
Items confirmed in the design discussions of 2026-10-26 to 2026-10-29 say so. Statuses:

- **Confirmed** / **Rejected**: decided by the developer.
- **Proposed**: waiting for the developer's decision.
- **Deferred (spec 00x)**: not needed for this slice; decided in that later spec, with its own tests.
- **Deferred (implementation)**: a name or API detail decided while implementing and confirmed in
  code review (C5's board review), not here (developer, 2026-10-29).

`/speckit-tasks` should not run until every item is Confirmed, Rejected or Deferred.

---

## R0. Existing deserialization code is not a design basis

- **Finding (developer, 2026-10-22)**: The current deserialization design is wrong. Nothing in `src/`
  may be reused, reshaped or used as a precedent unless R10 says so. What is wrong so far:
  - `Deserialize` returns Values; it must return rows.
  - The typed `Value<T>` model (`Value<T>`, `NullableValue<T>`, `DeserializedValue<T>`).
  - `DeserializerKind` / `TypeDefinitionKind` mirror the document's hierarchy instead of shredding
    into flat rows.
  - XML attributes (`ElementAttribute`, `CollectionAttribute`) live in the deserialization package
    (C0).
- **Also superseded**: `ARCHITECTURE.md`, `README.md` and `sdk/deserialize/CLAUDE.md` describe the
  old design and carry a banner saying so.
- **Status**: Confirmed (developer)

---

## R1. Split between the deserialization and media packages (C0)

- **Decision**: No media-neutral token or Value model. The deserialization package owns the entry
  points, the abstract options, the base enumerator and buffering, type, property and parameter
  definitions, policies and `Row`. A media package extends the options and supplies its own reader
  plus a synchronous, resumable step (R2) that turns buffered bytes into rows using the core's
  definitions and policies. Shape follows System.Text.Json: the serializer owns buffering and runs
  converters synchronously over `Utf8JsonReader`.
- **Rationale**: Media grammars differ too much to harmonize into one Value with dimensions: XML has
  an end element and attributes, JSON has property names apart from values, arrays and typed
  literals. What every media shares is the outcome: rows.
- **Rejected**:
  - A generic Value with dimensions read through an `IValueReader` the media implements (first R1
    proposal): the token mismatch above.
  - Deserialization consuming `IEnumerable<Value>` / `IAsyncEnumerable<Value>` (constitution 1.0.0).
  - Entry points and the read loop in the media package (interim proposal of 2026-10-26).
- **Status**: Confirmed (developer, 2026-10-26)

## R2. Reading layers: media row enumerators and two core pipelines

- **Decision (developer, 2026-10-28)**: Each media supplies a row enumerator (sync and async). Its
  move drives the media's own reader over buffered bytes and hands C# types and members to the
  core's two pipelines; it returns when the core has completed a row, which becomes `Current`. The
  media decides when a row is complete (its own end token for the row) and when its document is
  done, and says so through `Pipeline()` and the row state. The core owns the entry
  points, the base enumerators, the pipelines and `DeserializationState`, and is driven, never
  driving.
- **Layers**:

  ```
  Media enumerator (e.g. XmlRowEnumerator : RowEnumerator, XmlRowAsyncEnumerator : RowAsyncEnumerator)
     |  base (core): MoveNext / MoveNextAsync, buffering, advance, cancellation
     |  override (media): read tokens from the buffered bytes, return a RowState
     |  Pipeline<T>()              a row of type T (T : Row) starts here: push it; or Pipeline(Type)
     |  Pipeline(member, utf8, context)  this property or field of the top open row has this UTF-8 value
     |  Pipeline()                 the top open row is complete (the media's end token): pop it
     v
  Core                             type pipeline + member pipeline (R7), one DeserializationState
                                   composes rows; the override returns Complete with one
  ```

  | Part | Bytes and buffers | Tokens, names, paths | C# types and members | Definitions, converters, policies, rows |
  |---|---|---|---|---|
  | Base `RowEnumerator` / `RowAsyncEnumerator` (core) | yes | no | no | no (`Row` is opaque) |
  | Media override (media package) | its own slice only | yes | yes, hands them over | no |
  | Pipelines + `DeserializationState` (core) | no | no | yes, receives them | yes |

- **Who writes the moves (developer, 2026-10-28)**: The core's abstract base enumerators write the
  moves; the media overrides one read method. Transport rules are written once and cannot drift
  between media: buffering, advancing the pipe by the bytes consumed (even 0), cancellation,
  disposal (below).
  - **`RowEnumerator` (core, sync)**: implements `IEnumerator<Row>` over `ReadOnlyMemory<byte>`
    (`string` encoded to UTF-8 first). The memory is wrapped in a single-segment
    `ReadOnlySequence<byte>` (no copy); `MoveNext` calls the override with the final-block flag set.
  - **`RowAsyncEnumerator` (core, async)**: implements `IAsyncEnumerator<Row>` over a `PipeReader`
    (`Stream` via `PipeReader.Create`). `MoveNextAsync` refills, calls the override on the unconsumed
    bytes with the final-block flag from the pipe's `IsCompleted`, advances the pipe, and refills
    again on `Partial`.
  - Both: `Current` is set by the base from the override's row; one row per move; the move returns
    `false` on `None`. `Reset()` throws `NotSupportedException`: the input is read forward only (C2).
  - **Override (media)**: `protected abstract RowState Read(ReadOnlySequence<byte>
    bytes, bool isFinalBlock, out Row? row, out long consumed)`, the same signature on both bases, so
    a media writes its reading once and its two subclasses share it (e.g. both delegate to one
    media reading class).
- **`RowState` (developer, 2026-10-28)**: `None`, `Partial`, `Complete`:
  - `Complete`: the core composed a row during this call; it becomes `Current`.
  - `Partial`: no row yet; more bytes are needed.
  - `None`: no row and nothing more to read; the move returns `false`.
- **Completing rows (developer, 2026-10-29)**: The media decides when a row is complete. Both XML
  and JSON have an end token for a row (XML's end element, JSON's end of object); on it, the media
  calls the core's `Pipeline()`, which acts on the **top** row of the stack of open rows: it
  completes that row and runs the type pipeline's policies for it (by default: hand out, retention).
  The override then returns `Complete` with the row. The media holds no reference to rows; they live
  in the core.
- **End of document (developer, 2026-10-28/29)**: The media knows when its document is done (its
  grammar is complete, e.g. the root element closed) and sets the row state itself: rows still open
  then are completed by `Pipeline()` one per call, so one per move, and the override returns `None`
  once nothing is left (or directly, e.g. for an empty document). Replaces the earlier `Final()`
  called by the base enumerator.
- **Media override details**:
  - The media's reader continuation (where it stopped in its grammar, plus any depth or path its
    association needs) lives in the media's own fields; it rebuilds its reader from them on each
    call (unavoidable for a `ref struct` reader, sync and async alike), associates tokens with types
    and members (R6), calls the three `Pipeline` overloads, and saves the reader state again.
  - It hands over raw UTF-8 text only, as one contiguous `ReadOnlySpan<byte>`, as `Utf8JsonReader`
    callers do (`HasValueSequence ? ValueSequence.ToArray() : ValueSpan`). Conversion happens in
    the core (R14), so there is no `TValue` (developer, 2026-10-28). A value split across segments,
    or needing escapes or entities resolved, is copied into a scratch buffer (`stackalloc` for short
    values, a rented `ArrayPool<byte>` buffer for long ones) instead of `ToArray()`, and released
    right after the call, since the core converts synchronously inside it.
  - A value is never split across calls from the core's view: if its bytes are incomplete, the
    override returns `Partial` without consuming them, and the reader sees the whole value next
    time.
  - It returns as soon as the core has composed a row (`Complete`), so each move hands out one row
    and US3-2 holds (a row is out before the next row's data is read).
  - Contract: report bytes consumed, even 0, before returning `Partial`, or `PipeReader` throws on
    the next read. With the final-block flag set and its grammar incomplete (e.g. an unclosed
    element), it throws.
- **Entry points (R3)**: The core's static `Deserialize` / `DeserializeAsync` build the definitions
  and lock the options before returning, so FR-008 errors throw at the call. They get the media's
  enumerator from the options and expose it as `IEnumerable<Row>` / `IAsyncEnumerable<Row>`. Each
  returned enumerable can be enumerated once: a second `GetEnumerator()` / `GetAsyncEnumerator()`
  throws, since a pipe or stream cannot restart; sync and async behave the same. To read again, call
  `Deserialize` again.
- **End tokens**: The mismatch that started this design was about values (an XML leaf element has
  start, text and end; JSON has a property name and a value), not rows: both media have an end token
  for a row, and the media maps it to `Pipeline()`. Values need no end: a member is handed over with
  its value in one call.
- **Not a token model**: The rejected design (R1) made the reader's tokens uniform as a stream of
  Values with dimensions. Here each media keeps its own tokens and hands over C# types and members,
  calling the core directly inside the same synchronous call. Nothing is collected or streamed
  between the two.
- **`DeserializationState`**: One per deserialization, in the core. It holds what spans
  read cycles (a row often starts in one buffer and gets its fields in the next):
  - a **stack of open rows**: `Pipeline<T>()` pushes, `Pipeline()` pops the top; a `Log` is pushed
    on top of its open (partial) `Stem`; rows are kept across cycles until popped;
  - retained rows: completed rows a constructor needs, and rows kept for the whole deserialization
    (e.g. `Contract`) (R5, R7);
  - a composed row not yet handed out.
- **New implementation**: `RowEnumerator`, `RowAsyncEnumerator`, `RowState` and
  `DeserializationState` are written new. The existing `ReadingStatus`, `IReaderState`,
  `ValueAsyncEnumerable` and `ValueAsyncEnumerator` are deleted. The existing `Deserializer` /
  `Deserializer<T>` are not the enumerator: their folder is rewritten as converters (R10, R14).
  None of them is a design basis (R0).
- **`Dispose` / `DisposeAsync`**: Cleanup only, in the base enumerators, whether the
  document ended or the caller stopped early (`break`, `First()`, an exception): clear the
  `DeserializationState` (retained rows such as `Contract`, any open row never handed out), release
  the pipe's buffer through `PipeReaderBuffering`, drop the media's reader state. It hands out no row
  and runs no policies: a row produced there could never reach the caller, and on an early stop the
  document did not end.
- **Buffering**: `PipeReaderBuffering` stays as an internal helper of `RowAsyncEnumerator` (UTF-8 BOM
  skip, `IsCompleted` as the final block, doubling the minimum read when a call consumes nothing).
  `RowEnumerator` needs no buffering. `IBuffering<TBuffering, TStream>` goes away.
- **Rejected**:
  - Iterator methods behind each entry point, as System.Text.Json's `DeserializeAsyncEnumerableCore`
    (2026-10-26): the core would be described as driving; the developer's model is the media's
    enumerator driving (2026-10-28).
  - Media enumerators writing `MoveNext` / `MoveNextAsync` in full: transport rules would drift
    between media (2026-10-28).
  - `Final()` called by the base enumerator when the input is exhausted (2026-10-28): the media
    knows when its document is done and sets the row state.
  - One base `Deserializer` implementing both moves, with `DeserializerStatus { None, Value,
    Partial }` (2026-10-28): renamed and split into `RowEnumerator` / `RowAsyncEnumerator` and
    `RowState`.
  - A shredder class (`RowShredder`) with `Start` / `Value` / `End` / `Finish` (2026-10-26), replaced
    by the two pipelines (R7) and the media's association (R6).
  - No end moment per row, with the open row handed out when the next row's type arrives
    (2026-10-28): the media decides when a row is complete (2026-10-29).
- **Status**: Confirmed (developer, 2026-10-28), all of R2: media enumerator drives, core passive;
  core bases write the moves and the media overrides one read method (signature above, shared by a
  media's two subclasses); types and members handed to two pipelines; names
  `RowEnumerator` / `RowAsyncEnumerator` / `RowState` (`None` / `Partial` / `Complete`) and their
  meanings; the media completing its document through `Pipeline()`; the byte-span handover;
  the media completing each row through `Pipeline()` on the top of a stack of open rows (developer,
  2026-10-29);
  `DeserializationState` contents; `Dispose` as cleanup only; `Reset()` unsupported and one
  enumeration per call. Handover names: see R6.

## R3. Entry points and input shapes

- **Decision**: Static synchronous and asynchronous `Deserialize` methods in the deserialization
  package take string, memory, stream or pipe plus options, with no type argument: they return the
  abstract `Row`, and the row types come from the options (R12). The media is chosen by the options
  type (e.g. `XmlDeserializeOptions`).
- **Shapes**:
  - `string`, `ReadOnlyMemory<byte>` (`byte[]` converts implicitly): lazy `IEnumerable<Row>`.
  - `Stream`, `PipeReader`: `IAsyncEnumerable<Row>`.
  - Both wrap the media's `RowEnumerator` / `RowAsyncEnumerator` (R2), obtained from the options.
- **No span entry point**: A span cannot be stored in a lazy enumerator, so a span overload would
  need a `ref struct` enumerator (`foreach` only, no LINQ, no `await`). Memory gives the same
  span-based reading at no cost: it is wrapped in a single-segment sequence (no copy, no
  allocation), and the media's override reads spans from it (R2). System.Text.Json likewise has no lazy
  span overload.
- **Status**: Confirmed (developer, 2026-10-26)

## R4. Row and Field

- **Row (developer, 2026-10-29)**: `Row` is an abstract base class; the developer's classes extend it
  (`class Log : Row`). A derived row has real properties or fields set with real typed values (e.g.
  `Log.Volume` is a `decimal`). The pipelines construct the derived instance (`Pipeline<Log>()`) and
  set its members with typed values (`Pipeline(member, utf8, context)` -> converter -> setter, R14).
  Deserialization returns an enumeration of `Row`.
  - `Type`: the derived class (`GetType()`).
  - `AsType<T>()`: a checked cast from `Row` to `T`; a different class throws naming both classes.
    It is a downcast, not unboxing (rows are classes) and converts nothing: the values are already
    typed.
  - Helpers on the base check the derived type (e.g. `Is<T>()`).
- **Field (developer, 2026-10-29)**: A field is **metadata** about one member of a row, never a value
  and never cast. The values live on the derived class's typed members.
  - Per definition (the same for every row of a type): the member info, the media name, the C#
    type, the converter. Available from the property definition at no cost per row.
  - Per occurrence (different for every row): what the media reported in the `FieldContext` (R15):
    depth, name, path, dimensions.
- **Per-occurrence metadata is opt-in (developer, 2026-10-29)**: It is kept on a row only when a
  policy or option asks for it;
  by default nothing per occurrence is kept, so rows cost no more than their typed members (C1). A
  metadata policy copies what it needs from the `FieldContext` and can intern repeated strings (e.g.
  the same path for every `LogVolume`).
- **Conversion at handover (developer, 2026-10-29)**: A member's text is converted to its type when
  it is handed over (not in
  `AsType<T>()`), so an unconvertible value fails fast (FR-007). Typed end to end, no boxing (R14).
- **Board item**: A row exposing its type definition and field metadata so generic writers (e.g.
  Azure Data Explorer) can write rows without knowing the classes. Beyond the POC.
- **Pending outside research**: the constitution's and the spec's definitions of `Field` still say
  "one named, typed value of a row"; they change to "metadata about a member" once this item is
  confirmed (constitution 2.0.0 is not merged yet).
- **Status**: Confirmed (developer, 2026-10-26/29), all of R4: Row as the typed derived instance,
  `Type`, `AsType<T>()` as a checked cast, type-check helpers, Field as metadata only,
  per-occurrence metadata opt-in, conversion at handover

## R5. Constructing rows

- **Decision (developer, 2026-10-29)**: A type definition's job is to construct a row instance of its
  concrete type: its constructor is called with values for its parameter definitions. It sets no
  properties itself. Its property definitions each set one property or field of the derived row
  instance with a value of the right type, through a setter (R14). A child row's constructor
  receives the rows it needs (e.g. `Log(Stem stem, Contract contract)`) and copies the values it
  needs, so rows stay flat and nothing downstream follows references. The type
  pipeline's construct slot calls this (R7); the media never does. Setters are used by the member
  pipeline (R14).
- **Parameters come from open or retained rows (developer, 2026-10-27/29)**: A parameter looks in the
  `DeserializationState` for a row of its type: first in the stack of open rows, then among
  completed rows retained for it.
  - **Ancestors come from the stack, partial**: a `Stem` is still open, so partial, while the `Log`
    rows inside it are constructed. It needs no retention: it stays on the stack until its own
    `Pipeline()`.
  - **Retention is only for rows that are not open ancestors**: e.g. a `Contract` completed earlier
    in the document, before any `Stem`. Default (POC): the latest completed row of each such type a
    constructor needs is retained; a retention policy can keep rows for the whole deserialization
    instead (R7; keyed lookups come later).
  - No path or structure definitions are needed: the open-row stack follows the media's own nesting.
  - **Known gap**: a `Log` outside any open `Stem` would find no ancestor on the stack and could fall
    back to a retained completed `Stem`; only structure (R13) or a fail-fast policy requiring an open
    ancestor could detect that. Accepted for the POC: in HPR every `Log` is inside its `Stem`.
- **Fail fast when building definitions (developer, 2026-10-26)**: a registered type whose
  constructor cannot be satisfied is rejected before any byte is read. Cases for the ancestors
  slice, Deferred (spec 002/003): a parameter type that does not extend `Row` (and is not a lookup type); a
  parameter type extending `Row` that is not registered (R12); several public constructors with
  none marked as the one to use; more than one constructor marked. A type with no attributes and
  no options is not an error: its
  defaults apply. Detectable only while reading: a row constructed before any row of a parameter's
  type is open or retained (S1, data not read).
- **Choosing the constructor (developer, 2026-10-29)**: A core attribute (proposed name
  `[RowConstructor]`, like System.Text.Json's `[JsonConstructor]`) marks the constructor to use,
  public or non-public. Rules when definitions are built:
  - exactly one constructor marked: it is used, whatever its accessibility;
  - none marked and exactly one public constructor: it is used;
  - otherwise (none marked and several public constructors, or none public): fail fast;
  - more than one constructor marked: fail fast. `AttributeUsage(AllowMultiple = false)` only stops
    the attribute appearing twice on the same constructor, not on two constructors of one class, so
    "once per class" is checked here (an analyzer could flag it at compile time later).
- **Row type lookup (developer, 2026-10-29)**: The core keeps a lookup of the `Row` types the
  options know about, used by these checks. It is built per options when they lock at first use,
  from explicit registrations and any named-assembly scan (R12), and is fixed from then on (an
  immutable, frozen dictionary from `Type` to type definition; reads need no locking). It is not
  global: a process-wide registry would let one options instance see types registered by another,
  breaking S0 per options and isolating nothing between deserializations running at once (R6). The
  same lookup serves `Pipeline(Type)` and the S0 check at read time (R6).
- **Generic property definitions (developer, 2026-10-29)**: A property definition is generic,
  `PropertyDefinition<T> : PropertyDefinition`. Its `T` determines its effective converter, which
  converts the handed-over byte span into a `T`; converters are registered from the options (R14).
- **Delegates (developer, 2026-10-29)**: Created once per definition, so there is no reflection per
  row: the
  type definition's constructor delegate, and each `PropertyDefinition<T>`'s setter, an
  `Action<Row, T>` (`Delegate.CreateDelegate` on property setters; compiled expressions for fields).
  A source generator can come later (trimming/AOT).
- **Slice impact**: This slice has parameterless constructors only: a registered type whose
  constructor has parameters is rejected (FR-008). Parameter definitions exist in the model and get
  retained rows in specs 002-005.
- **Status**: Construction model, fail fast when building definitions, parameters from retained rows
  (open rows first, then retained) Confirmed (developer, 2026-10-26/27/29); latest-of-type retention
  for rows that are not open ancestors as the POC default Confirmed with R7's built-in defaults
  (developer, 2026-10-29); generic
  `PropertyDefinition<T>` choosing its converter by `T` from the options Confirmed (developer,
  2026-10-29); delegates, the per-options row type lookup, choosing the constructor by a core
  attribute (public or non-public, once per class) Confirmed (developer, 2026-10-29); the
  attribute's name Deferred (implementation); the listed constructor-parameter cases Deferred
  (spec 002/003)

## R6. Ownership of definitions and association

- **Decision (developer, 2026-10-27/28)**:
  - The options own and build all definitions: type, property and parameter. They are built
    behind the scenes on first use and cached in the options. Options become read-only when first
    used; a later change throws.
  - Type definitions are found from C# types extending `Row`: registered explicitly or found by
    scanning an assembly the developer names in the options (R12).
  - Type definitions own their property and parameter definitions. A type definition constructs a
    row (R5); its property definitions set the row's typed members (R14).
  - **The core owns looking up a type definition by its concrete C# type**, through the per-options
    row type lookup (R5).
  - **The media owns association**: which token, name or path means which row type and which
    member, read from attributes and options (R8). The media builds its association once per
    options (reflection only then) and hands the core C# identities while reading:
    - `Pipeline<T>() where T : Row` for a row start (developer, 2026-10-28). The media's association
      map stores, per row type, a delegate to the closed generic call, bound once per options
      (reflection only then); the delegate carries that options' type definition, so reading calls
      `Pipeline<Log>()` with no lookup. No process-wide static cache (R5). A non-generic
      `Pipeline(Type type)` exists for simpler media, at the cost of one lookup per row in the
      per-options row type lookup (Deferred (implementation)).
    - `Pipeline(MemberInfo member, ReadOnlySpan<byte> utf8, in FieldContext context)` for a value,
      where `member` is the `PropertyInfo` or `FieldInfo` of the top open row's type (developer,
      2026-10-28) and `context` is what the media reports about where the value sits (R15,
      developer 2026-10-29). The member is a value, not a type, so it is an argument, not a generic
      parameter.
    - `Pipeline()` when the media decides the top open row is complete (its end token), and for
      each row still open at the end of its document (developer, 2026-10-28/29): completes the top
      row and runs its policies (R2, R7).

    All three are named `Pipeline` because each runs a core pipeline with its policies.
  - The core checks every handover: a type not registered throws (S0); a member not belonging to the
    top open row's type definition throws; `Pipeline()` with no open row throws. The core keys its lookups on something stable (the property
    definition built from the member, or metadata token plus module), not on raw `MemberInfo`
    equality, which depends on how the member was obtained.
  - The abstract `DeserializeOptions` carries the generic settings, policies, converters and
    limits; media packages extend it (e.g. `XmlDeserializeOptions`) and supply their
    `RowEnumerator` and `RowAsyncEnumerator` subclasses and default converters (R14) through it.
  - Definitions are immutable and hold no per-document state, so one options instance can serve
    several deserializations at once. Everything per document lives in the `DeserializationState`
    (R2).
- **Status**: Options ownership, finding types, core lookup by type, media association, the three
  `Pipeline` handovers and their names Confirmed (developer, 2026-10-27/28); the delegate binding
  carrying the type definition Confirmed with the row type lookup (developer, 2026-10-29); the
  non-generic `Pipeline(Type)` Deferred (implementation)

## R7. Two policy pipelines

- **Decision (developer, 2026-10-28)**: Two pipelines in the core, where the core's policies apply.
  Each is a fixed order of policy slots; built-in defaults fill every slot; the chain is resolved
  once per definition when definitions are built. A document-specific package (e.g. StanForD) swaps
  individual links; it never adds code to the media's override. The media's association (R6) comes before
  both, so neither has a match slot.

  ```
  Type pipeline      Pipeline<T>()            retain-in -> construct the new row (parameters from open
                                              or retained rows) -> push it on the open-row stack
                     Pipeline()               complete the top open row -> hand out (default)
                                              -> retain-out -> pop it
  Member pipeline    Pipeline(member, utf8, context)  property definition -> convert (Converter<T>, R14)
                                              -> policies on the typed value -> set (Action<Row, T>)
  ```

  Both read and write the `DeserializationState` (R2). The member pipeline is generic in `T` per
  property definition (R14); the type pipeline works on rows.
- **Hand out when the media completes the row (developer, 2026-10-29)**: By default a row is handed
  out when `Pipeline()` completes it, acting on the top of the open-row stack. On HPR:
  `Pipeline<Stem>()` pushes a Stem; `Pipeline<Log>()` pushes Log 1 on top and constructs it with
  the partial Stem; `</Log>` -> `Pipeline()` hands out Log 1; and so on; `</Stem>` -> `Pipeline()`
  hands out the Stem, after its Logs. The timing is a policy slot: e.g. a policy handing a Stem out
  when its first child row starts gives Stems before their Logs (all Stem fields precede the first
  Log in both local HPR samples); a row handed out early must not change afterwards.
- **Attachment and precedence (Confirmed, developer 2026-10-26)**: Policies attach to type, property
  and parameter definitions, and options-wide, by attribute and through the options; options win.
  Most specific first:
  1. option for that property or parameter
  2. option for that type
  3. attribute on the property or parameter
  4. attribute on the type
  5. options-wide default
  6. built-in default
- **Built-in defaults (developer, 2026-10-29)**: what fills each slot when no policy replaces it.
  - **Construct** when the row's type is handed over (`Pipeline<T>()`), and push it on the open-row
    stack. The type definition plays the
    role of System.Text.Json's object converter, next to the converters for `bool`, `string` and
    the primitive types (R14): its parameter definitions are defined in advance, when the options
    lock. Each parameter gets its value from an open or retained row (R5) or, when there is none,
    from the C# parameter's own default value if it declares one; otherwise it fails fast (S1). A construct
    policy registered in the options can replace the default construction for a type, like a custom
    converter.
  - **Convert and set** each member as handed over (`Pipeline(member, utf8, context)`): conversion
    to the member's `T` by its effective converter, then its setter (R14). Creating `Field`
    metadata is a separate slot, off by default (R4, opt-in).
  - **Hand out** the top open row when the media completes it (`Pipeline()`), then pop it.
  - **Retain** the latest completed row of each type a constructor needs that is not an open
    ancestor (e.g. `Contract`; R5). Open ancestors such as a partial `Stem` need no retention: they
    are on the open-row stack.
  - **Drop the core's reference** to every other row once it is handed out: the caller may keep the
    row, but the core holds nothing it does not need, so memory stays flat (C1). This is not
    `Dispose`, which clears whatever is still retained at the end or on an early stop (R2).
- **Retention**: Applies to completed rows only; open rows stay on the stack until their
  `Pipeline()`. A completed row a parameter definition asks for is retained until it is replaced
  (latest of type) or the deserialization ends (whole-document retention). A retention policy may hold a row
  longer, never shorter than a constructor needs; a policy that would release too early fails when
  definitions are built. A row handed out while still retained must not change after it is handed
  out.
- **StanForD as policies, not code**: e.g. `Contract` and other references retained for the whole
  document. All Stem fields precede the first Log in both local Rottne HPR samples (verified
  2026-10-26; check other makers' files before relying on it).
- **Fail-fast policy**: A row constructed while a field it needs from a retained row has not been
  read yet fails fast (S1), instead of silently getting a default. In the POC (developer,
  2026-10-26).
- **Edge cases for later (developer, 2026-10-28), Deferred (spec 002/003)**:
  - A member value for a row already handed out: only possible with an early hand-out policy (e.g. a
    Stem field after its first Log); proposed to fail fast, since a handed-out row must not change.
  - A document cut off inside a row: the media's reader sees it (unclosed elements at the final
    block) and throws; rows still open are cleared by `Dispose`, never handed out.
- **Slot order (developer, 2026-10-29)**:
  - **Minimal order for this slice (Confirmed)**: only the slots with built-in defaults that 001
    uses:

    ```
    Pipeline<T>()                      construct (constructor delegate, no parameters in 001) -> push
    Pipeline(member, utf8, context)    convert (Converter<T>) -> set (Action<Row, T>); Field metadata slot off
    Pipeline()                         complete the top row -> hand out -> pop
    ```

    Spec US4's test policy plugs into one of these slots (attached by attribute and by options, for
    precedence and read-only options).
  - **Full catalogue and order: Deferred (spec 002/003)**: retention in and out (lifetime),
    parameter resolution and the fail-fast check, early hand-out (e.g. a Stem at its first child),
    typed-value policies on members (validate, transform), and whatever else two classes and real
    HPR data bring. Listed from real cases there, not guessed now.
  - **Holds whatever the order**: the type pipeline is about construction: the type definition
    arrives fully configured with its parameter and property definitions, so `Pipeline<T>()`
    constructs and pushes; retention is a lifetime policy around hand-out and pop, not
    construction.
- **Slicing (developer, 2026-10-29)**: Both pipelines, their slots, precedence and read-only options
  land in this slice, tested with a test policy (spec US4). Retention of completed rows and the
  fail-fast policy land with the slice that brings two classes (spec 002/003). This follows from
  001's parameterless constructors (FR-008 rejects constructors with parameters in this slice):
  with no parameters, there is nothing to retain and nothing for the fail-fast policy to check.
- **Status**: Two pipelines, media association before them Confirmed (developer, 2026-10-28);
  hand-out by default when the media completes the top open row, timing as a policy slot Confirmed
  (developer, 2026-10-29); emit on the next type Rejected (2026-10-29); attachment, precedence,
  fail-fast policy Confirmed (2026-10-26); built-in defaults Confirmed (developer, 2026-10-29); edge
  cases Deferred (spec 002/003); slicing and the minimal slot order for this slice Confirmed (developer, 2026-10-29); full policy
  catalogue and order Deferred (spec 002/003)

## R8. Attributes

- **Decision**: Generic attributes (which class is a row, which property is a field, names, policies)
  are declared in the deserialization package as data. Media-specific condition attributes (e.g.
  XML attribute conditions) live in the media package.
- **The media interprets them (developer, 2026-10-29)**: once per options, the media associates
  (R6):
  - each row type `T : Row` with what starts its row in the media, from its attributes (generic and
    media-specific) or, without attributes, by default by name (XML: an element named like the
    class);
  - each property or field of that type with its `PropertyInfo` / `FieldInfo`, by the same process
    (attributes, or by default by name).

  XML reads a name as an element name; JSON would read it as a property name.
- **Names, Deferred (implementation)**: e.g. `[Row("Log")]`, `[Field("LogKey")]`; default names are
  the class and property names. `[RowConstructor]` marks the constructor to use (R5); unlike the
  others it is read by the core, not interpreted by the media.
- **Status**: Placement Confirmed (developer, 2026-10-26); the media interpreting them for types and
  members Confirmed (developer, 2026-10-29); names Deferred (implementation)

## R9. Public documentation (D0)

- **Finding**: `GenerateDocumentationFile` is off; with SDK-wide `TreatWarningsAsErrors`, turning it
  on makes every undocumented public member a build error, including existing code.
- **Options**: A. Enable it in this package now and document the slice's public surface plus existing
  members. B. Enable it with CS1591 as a warning only until a later documentation task. C. Postpone
  (second justified deviation).
- **Decision (developer, 2026-10-29)**: **B** for now. **A** becomes mandatory once a real StanForD
  document can be deserialized end to end (the working example of C6): from then on, missing XML
  docs fail the build.
- **Status**: Confirmed (developer, 2026-10-29)

## R10. Disposition of existing code

Per class in `src/`; decisions are the developer's (R0, C5). "Keep" and "Reshape" reuse a class's
role and name, never its design as a precedent (R0).

| Class(es) | Disposition | Why | Status |
|---|---|---|---|
| `Value`, `Dimension`, `Dimensions`, `Dimension.Defaults` | Delete | No media-neutral Value model (R1) | Confirmed (developer, 2026-10-29) |
| `Value<T>`, `NullableValue<T>`, `DeserializedValue<T>` | Delete | Old typed-Value model; rows replace it | Confirmed (developer, 2026-10-29) |
| `Deserialization.Pipe/Span/String`, `Deserialization` | Reshape | Entry points stay in the core but return rows (R3) | Confirmed (developer, 2026-10-29) |
| `ValueAsyncEnumerable`, `ValueAsyncEnumerator` | Delete | Replaced by the core's `RowEnumerator` / `RowAsyncEnumerator` (R2) | Confirmed (developer, 2026-10-26) |
| `IBuffering` | Delete | One async path only; the sync path needs no buffering (R2) | Confirmed (developer, 2026-10-29) |
| `PipeReaderBuffering` | Keep, internal | Async path's buffering helper (R2) | Confirmed (developer, 2026-10-29) |
| `ReadingStatus`, `IReaderState` | Delete | Replaced by `RowState` (`None` / `Partial` / `Complete`); the reader continuation lives in the media subclass (R2) | Confirmed (developer, 2026-10-26) |
| `Deserializers/` (`Deserializer`, `Deserializer<T>`, `DeserializerFactory`, `IDeserializerProvider`) | Rewrite as converters | The folder's concept is what R14 calls converters: a typed converter per `T` (`Deserializer<T>`), a factory for families of types (`DeserializerFactory`, like System.Text.Json's `JsonConverterFactory`), and their lookup from the options (`IDeserializerProvider`), used with type, parameter and property definitions to create typed instances. Written new (R0); the name ("Converter" or "Deserializer") Deferred (implementation). Not the media's enumerator: that is `RowEnumerator` / `RowAsyncEnumerator` (R2) | Confirmed (developer, 2026-10-29) |
| `ReaderPath`, `ReaderPosition` | Delete | The media keeps its own position (depth, path); the core keeps the open-row stack in `DeserializationState` (R2) | Confirmed (developer, 2026-10-29) |
| `DeserializerKind`, `TypeDefinitionKind` | ? | Mirror the hierarchy; depends on R13 (structure definitions or not) | Deferred (spec 002/003) |
| `TypeDefinition*`, `PropertyDefinition*`, `DeserializeOptions(.Caching)`, providers | Keep, extend | Options own and cache definitions (R6); add parameter definitions (R5) | Confirmed (developer, 2026-10-29) |
| `NamingPolicy`, `IIgnorePropertyPolicy`, `IIncludeFieldPolicy` | Reshape | Fold into the policy pipeline (R7) | Confirmed (developer, 2026-10-29) |
| `Attributes/ElementAttribute`, `CollectionAttribute` | Delete from core | XML vocabulary in core (C0); generic attributes replace them (R8) | Confirmed (developer, 2026-10-29) |
| `ObservableList<T>` | Keep | Used for definitions' and options' lists before the type definitions' configuration is completed (then read-only, R6) | Confirmed (developer, 2026-10-29) |
| `ReadErrorHandling` | Delete | Not needed | Confirmed (developer, 2026-10-29) |
| `ReflextionExtensions` | Keep, rename | Typo in the name | Confirmed (developer, 2026-10-29) |

## R11. No fake media; tests through the XML reader (C3)

- **Decision**: No fake media and no fake token producer. Shredding is tested through
  `Forestry.Deserialize.Xml` with small hand-written XML documents, in `Forestry.Deserialize.Xml.Tests`.
  The core's own tests cover only what needs no document: definitions, policy precedence, read-only
  options, `Row.AsType<T>()`.
- **Consequence**: The slice's acceptance tests depend on the XML reader reading elements and text
  (`Forestry.Deserialize.Xml/BLOCKING.md`, phase 2, still open). Test shells (C4) can be written now;
  they go green only after phase 2.
- **Rejected**: A fake text media in `test/Fakes/` or a `Forestry.Deserialize.Testing` project.
- **Status**: Confirmed (developer, 2026-10-26)

## R12. Finding row types

- **Decision**: `Deserialize` takes no type argument and returns the abstract `Row` (developer,
  2026-10-26), so the row types come from the options.
- **Decision (developer, 2026-10-27)**: Two ways, both through the options and fixed at first use:
  - explicit registration (e.g. `options.Rows.Add<Log>()`);
  - scanning a specific assembly the developer names (e.g. `options.Rows.AddFromAssembly(typeof(Log).Assembly)`)
    for types extending `Row`. Never blind scanning of loaded assemblies.

  Either way the developer chooses the types; the input never does (S0). No row type found fails
  before any byte is read. This slice uses one type; spec 002 several.
- **Member names**: the names above are examples, Deferred (implementation).
- **Status**: No type argument, both ways, no blind scanning Confirmed (developer, 2026-10-26/27);
  member names Deferred (implementation)

## R13. Matching and the document's hierarchy

- **Question**: How does a type definition know where its rows are in a hierarchical document (root,
  intermediate elements such as `HarvestedProduction` or `SingleTreeProcessedStem`)? This is where
  the first design got stuck (developer, 2026-10-26).
- **Largely answered by R6 (2026-10-28)**: Matching is association, and association belongs to the
  media. Root, intermediate elements, paths and skipping unknown subtrees are all the media's
  concern; the core's pipelines receive only C# types and members and stay flat. Parameters do not
  need the hierarchy either (R5, retained rows). No structure definitions in the core.
- **Left to the XML package** (spec 002/003): how it associates, e.g.
  - **A. Name only**: a row or member matches its name anywhere (this slice). Simple; no validation
    of roots or nesting; no skipping.
  - **B. Paths**: absolute or relative to a parent row (e.g. `Log` under `Stem` as
    `SingleTreeProcessedStem/Log`), by attribute or options; allows checking that registered types
    agree on the root once per options.
  - **C. An internal structure tree** built once per options, allowing validation and skipping whole
    subtrees (the parts of StanForD never read).
- **Rule whatever the media chooses**: structure is only for association, skipping and validation.
  It never builds hierarchical objects; rows stay flat (the R0 trap with `DeserializerKind` /
  `TypeDefinitionKind`).
- **Status**: Core side Confirmed by R6 (developer, 2026-10-28); the XML package's approach
  Deferred (spec 002/003); this slice uses A

## R14. Converters and setters

- **Finding**: System.Text.Json's `DefaultJsonTypeInfoResolver` gives each property an effective
  converter resolved by type (`options.Converters` first, then the built-in primary converters) and
  a setter built once by a member accessor. Its converters read from `Utf8JsonReader`, so every
  converter is tied to JSON; that is what must differ here (developer, 2026-10-28).
- **Decision (developer, 2026-10-29)**:
  - **Converters take UTF-8 text, not a reader**: `abstract class Converter<T> { T Convert(ReadOnlySpan<byte> utf8); }`.
    The media hands over plain UTF-8 text (R2), so one converter serves every media, with only the
    target type from the property definition.
  - **Factories**: a converter factory creates converters for families of types (e.g. `Nullable<T>`,
    enums), as System.Text.Json's `JsonConverterFactory` does; this is the role of today's
    `DeserializerFactory` (R10). The names "Converter" and "Deserializer" are both candidates,
    Deferred (implementation) (developer, 2026-10-29).
  - **Built-ins**: one generic converter over `IUtf8SpanParsable<T>` (.NET 8+) covers `int`, `long`,
    `decimal`, `double`, `DateTime`, `Guid` and the other built-in types, with invariant culture;
    `string` and `bool` get their own.
  - **Resolved once by type** into each `PropertyDefinition<T>`'s effective converter, by its `T`,
    from the converters registered in the options (developer, 2026-10-29), with the policy
    precedence (R7): options, attributes, media defaults, built-ins. Media defaults exist because
    grammars disagree on text (XML Schema accepts `1` / `0` for `bool`, JSON does not); the media
    registers them through its options without the core knowing why.
  - **A failed conversion fails fast** naming the field, its place and the class (FR-007); the place
    is supplied by the media.
  - **Setters**: a member accessor builds, once per property definition, an `Action<Row, T>` for a
    property (setter delegate) or a field (compiled expression). No reflection per value.
  - **No boxing**: `PropertyDefinition<T>` holds `Converter<T>` and `Action<Row, T>`; a non-generic
    entry on the base `PropertyDefinition` (e.g. `ReadAndSet(Row row, ReadOnlySpan<byte> utf8)`,
    called by `Pipeline(member, utf8, context)`)
    dispatches into it, so the member pipeline calls any property without knowing `T`.
  - **Parameters**: today resolved from retained rows (type level, R5). If constructors ever take
    scalar values from the document (System.Text.Json allows it), they reuse these converters.
- **Status**: Converters independent of the reader, by target type Confirmed (developer,
  2026-10-28); `PropertyDefinition<T>` resolving its converter by `T` from the options Confirmed
  (developer, 2026-10-29); signature, factories, built-ins, media defaults, accessor and no-boxing
  shape Confirmed (developer, 2026-10-29); the name "Converter" or "Deserializer" Deferred
  (implementation)

## R15. Field context and dimensions

- **Decision (developer, 2026-10-29)**: With each value, the media hands over a `FieldContext`: a
  `readonly ref struct` of generic information every media can report. A `ref struct` cannot be a
  base class, so the members are generic, not media-specific:
  - `Depth`: the value's depth in the media's hierarchy;
  - `Name`: the media name (e.g. `LogVolume`), as a `ReadOnlySpan<byte>`;
  - `Path`: the media path (e.g. `HarvestedProduction/Machine/Stem/SingleTreeProcessedStem/Log/LogVolume`),
    as a `ReadOnlySpan<byte>`;
  - `Dimensions`: media-specific name-value pairs, as one delimited `ReadOnlySpan<byte>`.
- **Strings are byte spans**: Text members are UTF-8 spans pointing at bytes the media already holds
  (its reader's buffer, its name stack), so building a context allocates nothing. Extension methods
  in the core turn them into a `string`, or the dimensions into a dictionary, only when someone asks.
- **Dimensions builder**: A dimensions builder (or creator) uses rented bytes (`ArrayPool<byte>`) to
  construct the read-only, delimited span of name-value pairs that are specific to the media; the
  media owns one per enumerator, clears and refills it per value, and passes the written span in
  the context. The context itself stays read-only: nothing downstream can change what the media
  reported.
- **Why not `IDictionary<string, string>`**: building one per value allocates about 450 bytes (the
  dictionary, its arrays and the strings), roughly 5-7 MB of short-lived garbage per 1 MB of HPR XML,
  paid even when no policy looks at it; kept on rows, about 7 KB per `Log`, 40-70 times the row
  itself (developer accepted the analysis, 2026-10-29). It also turns depth into a string.
- **Delimiters, Deferred (implementation)**: suggested delimiters that cannot occur in a value:
  0x1F between name and value, 0x1E between pairs (forbidden in XML 1.0 content; a JSON value
  containing one throws in the extension). The core keeps nothing from the context by default; a
  metadata policy copies what it needs (R4).
- **Status**: The four members, byte spans with extensions, `Dimensions` naming and a rented-byte
  builder Confirmed (developer, 2026-10-29); delimiters and the builder's API Deferred
  (implementation)
