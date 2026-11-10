# Forestry Deserialize

Forestry Deserialize reads large documents in one forward pass and shreds them into flat rows: one
instance of your own class per occurrence in the document, handed out as soon as it is complete.
Nothing keeps a row once it is handed out, so a document of a million rows costs about the same
memory as one of a thousand. The need comes from StanForD XML from harvesting and forwarding
machines, which is the real data this is developed and tested against, but neither this package nor
`Forestry.Deserialize.Xml` is limited to StanForD.

The design is specified with spec-kit: the rules are in the
[constitution](.specify/memory/constitution.md) and the current slice is
[spec 001, walking skeleton](specs/001-walking-skeleton/spec.md), with its decisions in
[research.md](specs/001-walking-skeleton/research.md). Some of the design is adapted from
Microsoft's open-source .NET runtime; see [THIRD-PARTY-NOTICES.md](../../../THIRD-PARTY-NOTICES.md).

## Try the preview

Preview packages are attached to this repository's
[GitHub releases](https://github.com/ForestryAI/forestry-sdk-for-net/releases), with no sign-in needed.
Download `Forestry.Deserialize.<version>.nupkg` from the latest `Forestry.Deserialize/v…` release into a folder, then:

```bash
dotnet nuget add source <folder> -n forestry-preview
dotnet add package Forestry.Deserialize --prerelease
```

## Packages

- **Forestry.Deserialize**: the media-agnostic core: rows, options, type definitions, converters and
  the pipelines that build rows. It knows no media.
- **Forestry.Deserialize.Xml**: the XML media. Its options (`DeserializeXmlOptions`) choose XML and
  its reader feeds the core.

## Shredding a document into rows

A row class extends `Row`. Its public properties and fields are set from the document by name:

```csharp
public sealed class Log : Row
{
    public int LogKey { get; set; }

    public decimal LogVolume { get; set; }
}
```

Add the row types to the media's options, then deserialize. Every `<Log>` element becomes one `Log`
row, in document order; elements and members that match nothing are ignored.

```csharp
DeserializeXmlOptions options = new();
options.Rows.Add<Log>();

foreach (Row row in Deserialization.Deserialize(xml, options))
{
    Log log = (Log)row;
    // use log.LogKey and log.LogVolume, then let it go
}
```

`Deserialize` takes a `string` or UTF-8 bytes (`ReadOnlyMemory<byte>`, so a `byte[]` too) and
returns a lazy `IEnumerable<Row>` that can be enumerated once. Rows are classes, so a plain cast
gives the typed row.

### Checking and casting rows

`Row.Is<T>()` checks a row's class and `Row.AsType<T>()` gives the typed row. They matter most once
several row types come from one pass (a later slice), but work the same today:

```csharp
foreach (Row row in Deserialization.Deserialize(xml, options))
{
    if (row.Is<Log>())
    {
        Log log = row.AsType<Log>();
        // use log.LogKey and log.LogVolume
    }
}
```

- `Is<T>()` works as C#'s `is`: it is true for `T` and for classes deriving from it.
- `AsType<T>()` returns the same instance, with its values already typed: nothing is copied or
  converted. When the row is not a `T`, it throws an `InvalidCastException` naming both classes
  by their full names (e.g. `A row of type 'MyApp.Stem' is not a 'MyApp.Log'.`).
- A plain cast, or C#'s own `is` and `as`, works too; `AsType<T>()` only adds the clearer message.

### Reading a stream or pipe asynchronously

A large document does not need to be in memory: `DeserializeAsync` reads a `Stream` or a
`PipeReader` and returns a lazy `IAsyncEnumerable<Row>`, enumerable once, with the same rows as
`Deserialize`:

```csharp
await using FileStream file = File.OpenRead("harvest.hpr");

await foreach (Row row in Deserialization.DeserializeAsync(file, options, cancellationToken))
{
    Log log = (Log)row;
}
```

- Each row is handed out as soon as its end is read, without waiting for more input.
- The bytes can be split anywhere, even inside a name or a value: the rows are the same.
- A stream is left open; a `PipeReader` is the caller's to complete.
- The options are checked and locked at the call, as with `Deserialize`. The cancellation token,
  and one passed to `WithCancellation`, cancel a move that waits for bytes.

### Names, constructors and converters

- **Names**: by default a row matches an element named like its class and a member an element named
  like the member. `[Row("Tree")]` on the class and `[Field("Volume")]` on a property or field use
  the document's name instead.
- **Constructor**: a row type needs exactly one public constructor, or exactly one constructor
  marked `[RowConstructor]`, whatever its accessibility. In this slice it takes no parameters.
- **Converters**: each member's text is converted to its type once, invariant culture: text,
  booleans, integers, decimals, `DateTime`, `DateTimeOffset`, `Guid` and the other types parsable
  from UTF-8, plus their `Nullable<T>`. A media adds its own defaults (XML reads `1` and `0` as
  booleans). Your own `Converter<T>` added to `options.Converters` wins over both.
- **Empty elements**: `<LogVolume />` sets `null` where the member allows it, otherwise the type's
  default.

### Changing behavior with policies

Policies change what happens while rows are built, without touching the media. Two kinds, in
`Forestry.Deserialize.Policies`:

- **Enumeration policies** (`EnumerationPolicy`) act per row: when it is constructed (`Construct`),
  when each member is set (`Set`), when a completed row may be enumerated (`Enumerate`), and when the
  enumeration ends (`Clear`). Override only what you need; a policy takes part in exactly the methods
  it overrides. Each method gets the next step as `next`: call it to keep the default.
- **Definition policies** (`DefinitionPolicy`) act once, when the options lock, and decide which
  public properties and fields of a row type are members (`IncludeMember`).

```csharp
public sealed class AuditingPolicy : EnumerationPolicy
{
    public override void Set<T>(Row row, PropertyDefinition<T> property, T value, Action<Row, T> next)
    {
        // inspect or change the value, then let the setter run
        next(row, value);
    }
}

DeserializeXmlOptions options = new();
options.Rows.Add<Log>();
options.Policies.Add<Log>(new AuditingPolicy());                 // every member of Log
options.Policies.Add<Log>(nameof(Log.LogKey), new AuditingPolicy()); // only LogKey
```

- **Attaching**: `[Policy<TPolicy>]` on a row class or member, or through `options.Policies`: for
  every row type (`Add(policy)`), one type (`Add<TRow>(policy)`) or one member
  (`Add<TRow>(memberName, policy)`, `Set` only). The options win over attributes; the most specific
  target wins, and policies added through the options for the same target run in the order added.
- **Lifetime**: a policy lives as long as its options and may keep state for one enumeration; it is
  cleared when each enumeration ends. So options with enumeration policies serve one enumeration at
  a time: use one options instance per concurrent deserialization.
- Policies are configuration: once the options are used, adding one throws.

### Failing fast

- Before any byte is read, at the `Deserialize` call: no row type added, a member whose type no
  converter handles, no constructor that can be chosen, a chosen constructor with parameters,
  two row types or two members read from the same element, and a policy that cannot apply (one on
  a member overriding more than `Set`, or registered for a type or member the options do not have).
- While reading: a value that cannot be converted throws a `FormatException` naming the member, its
  path in the document (e.g. `Root/Log/LogKey`) and the class. A document cut off inside a row
  throws, and that row is never handed out.

## Status

Spec 001's four user stories are done ([tasks.md](specs/001-walking-skeleton/tasks.md)): the
synchronous and asynchronous entry points, `Row.Is<T>()` / `Row.AsType<T>()` (a type check and a
checked cast naming both classes when it fails), and policies attached by attribute or by options.
The policy model is good enough for the POC and expected to move with the next slices (research R19).

Later slices add several row types in one pass and rows that take their ancestors (e.g. a `Log`
built with its `Stem`).

## Tests

```bash
dotnet test test/Forestry.Deserialize.Tests.csproj
```

The core's tests need no document: definitions, constructor choice and converter resolution.
Shredding is tested through the real XML reader in `Forestry.Deserialize.Xml`. Slow performance
tests (trait `Category=Performance`) are left out of a plain `dotnet test` and run as the gate at
the end of every user story: `dotnet test -s Performance.runsettings`.
