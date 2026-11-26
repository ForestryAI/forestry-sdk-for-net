# Forestry Serialization

Forestry Serialization is where the Forestry SDK turns telemetries from forestry devices into big
data (see [Telemetry, monitoring and observability](../../../README.md#telemetry-monitoring-and-observability)). What a device writes, such as a StanForD Harvesting document from a harvester, is deep and
nested; what a big data resource reads is flat. This package turns one into the other: it reads a
document forward, once, and hands out **telemetries**. A telemetry is an abstraction with no nested structures,
made before a big data format is chosen and before any pre-washing or customization of the data.
What matters is that each telemetry is self-contained, so whatever processing comes next needs nothing
else to make sense of it.

A client takes in the whole incoming document and hands it to deserialization, whose telemetries feed the
client's pipeline on the way from device output to big data input: no telemetry reaches the pipeline
without it. Telemetries come out one at a time, so the pipeline is free to gather them into chunks for the
outgoing format rather than turning one document into one output.
The SDK's [README](../../../README.md#big-data-clients) shows how the pieces fit together.

Telemetries go forward from the incoming document to what comes next: one instance of your own class per
occurrence in the document, handed out as soon as it is complete. Nothing keeps a telemetry once it is
handed out unless a policy asks to, so a document of a million telemetries costs about the same memory as
one of a thousand. StanForD from harvesting and forwarding machines was the driving force and is the
real data this is developed and tested against, but the concept is operative metrics from forestry
devices: neither this package nor `Forestry.Serialization.Xml` is limited to StanForD.

The design is specified with spec-kit: the rules are in the
[constitution](.specify/memory/constitution.md), the slices are
[spec 001, walking skeleton](specs/001-walking-skeleton/spec.md),
[spec 002, deserialization context](specs/002-reading-context/spec.md) and
[spec 003, big data orientation](specs/003-big-data-orientation/spec.md), each with its decisions in
its research.md. Some of the design is adapted from
Microsoft's open-source .NET runtime; see [THIRD-PARTY-NOTICES.md](../../../THIRD-PARTY-NOTICES.md).

## Try the preview

Preview packages are attached to this repository's
[GitHub releases](https://github.com/ForestryAI/forestry-sdk-for-net/releases), with no sign-in needed.
Download `Forestry.Serialization.<version>.nupkg` from the latest `Forestry.Serialization/v…` release into a folder, then:

```bash
dotnet nuget add source <folder> -n forestry-preview
dotnet add package Forestry.Serialization --prerelease
```

## Packages

- **Forestry.Serialization**: the format-agnostic core: telemetries, options, type definitions, converters and
  the pipelines that build telemetries. It knows no format.
- **Forestry.Serialization.Xml**: the XML format. Its options (`DeserializeXmlOptions`) choose XML and
  its reader feeds the core.

## Flattening a document into telemetries

A telemetry class extends `Telemetry`. Its public properties and fields are set from the document by name:

```csharp
public sealed class Log : Telemetry
{
    public int LogKey { get; set; }

    public decimal LogVolume { get; set; }
}
```

A telemetry is the unit deserialization hands out, and it is flat: every member is a single value, a
primitive, a `string`, an enum, or a struct that formats to UTF-8 and parses from text (e.g. `Guid`,
`DateTimeOffset`), or a nullable of one of these. Never a nested object, a collection or a composite
struct, so each member can become one column in any big data format, JSON Lines or Parquet alike.
"Telemetry" names that unit, as the SDK's [README](../../../README.md#telemetry-monitoring-and-observability)
defines it; a format's own units keep their names (e.g. a Parquet row group).

Add the telemetry types to the format's options, then deserialize. Every `<Log>` element becomes one `Log`
telemetry, in document order; elements and members that match nothing are ignored.

```csharp
DeserializeXmlOptions options = new();
options.Telemetries.Add<Log>();

foreach (Telemetry telemetry in TelemetrySerialization.Deserialize(xml, options))
{
    Log log = (Log)telemetry;
    // use log.LogKey and log.LogVolume, then let it go
}
```

`Deserialize` takes a `string` or UTF-8 bytes (`ReadOnlyMemory<byte>`, so a `byte[]` too) and
returns a lazy `IEnumerable<Telemetry>` that can be enumerated once. Telemetries are classes, so a plain cast
gives the typed telemetry.

### Checking and casting telemetries

`Telemetry.Is<T>()` checks a telemetry's class and `Telemetry.AsType<T>()` gives the typed telemetry. They matter most when
several telemetry types come from one pass:

```csharp
foreach (Telemetry telemetry in TelemetrySerialization.Deserialize(xml, options))
{
    if (telemetry.Is<Log>())
    {
        Log log = telemetry.AsType<Log>();
        // use log.LogKey and log.LogVolume
    }
}
```

- `Is<T>()` works as C#'s `is`: it is true for `T` and for classes deriving from it.
- `AsType<T>()` returns the same instance, with its values already typed: nothing is copied or
  converted. When the telemetry is not a `T`, it throws an `InvalidCastException` naming both classes
  by their full names (e.g. `A telemetry of type 'MyApp.Stem' is not a 'MyApp.Log'.`).
- A plain cast, or C#'s own `is` and `as`, works too; `AsType<T>()` only adds the clearer message.

### Reading a stream or pipe asynchronously

A large document does not need to be in memory: `DeserializeAsync` reads a `Stream` or a
`PipeReader` and returns a lazy `IAsyncEnumerable<Telemetry>`, enumerable once, with the same telemetries as
`Deserialize`:

```csharp
await using FileStream file = File.OpenRead("harvest.hpr");

await foreach (Telemetry telemetry in TelemetrySerialization.DeserializeAsync(file, options, cancellationToken))
{
    Log log = (Log)telemetry;
}
```

- Each telemetry is handed out as soon as its end is read, without waiting for more input.
- The bytes can be split anywhere, even inside a name or a value: the telemetries are the same.
- A stream is left open; a `PipeReader` is the caller's to complete.
- The options are checked and locked at the call, as with `Deserialize`. The cancellation token,
  and one passed to `WithCancellation`, cancel a move that waits for bytes.

### Names, constructors and converters

- **Names**: by default a telemetry matches an element named like its class and a member an element named
  like the member. The XML package's `[XmlElementName("Tree")]` on the class, or
  `[XmlElementName("Volume")]` on a property or field, uses the document's name instead: names in the
  document belong to the format, not to this package.
- **Constructor**: deserialization uses the constructor marked `[TelemetryConstructor]`, whatever its
  accessibility. Without a mark it uses the class's only public constructor, which for a class that
  declares none is the one C# provides; with several public constructors and no mark, it cannot
  choose. In this slice the constructor takes no parameters: members are set from the document or
  by policies.
- **Converters**: each member's text is converted to its type once, invariant culture: text,
  booleans, integers, decimals, `DateTime`, `DateTimeOffset`, `Guid` and the other types parsable
  from UTF-8, plus their `Nullable<T>`. A format adds its own defaults (XML reads `1` and `0` as
  booleans). Your own `Converter<T>` added to `options.Converters` wins over both.
- **Empty elements**: `<LogVolume />` sets `null` where the member allows it, otherwise the type's
  default.

### Changing behavior with policies

Policies change what happens while telemetries are built, without touching the format. Two kinds, in
`Forestry.Serialization.Policies`:

- **Enumeration policies** (`EnumerationPolicy`) act per telemetry: when it is constructed (`Construct`),
  when each member is set (`Set`), when it completes (`Complete`), and when the enumeration ends
  (`Clear`). Each method gets the enumeration's deserialization context (see below) and the next step
  as `next`: call it to keep the default. Override only what you need; a policy takes part in exactly
  the methods it overrides.
- **Definition policies** (`DefinitionPolicy`) act once, when the options lock, and decide which
  public properties and fields of a telemetry type are members (`IncludeMember`).

```csharp
public sealed class AuditingPolicy : EnumerationPolicy
{
    public override void Set<T>(ContextTelemetry telemetry, PropertyDefinition<T> property, T value,
        DeserializeContext context, Action<ContextTelemetry, T> next)
    {
        // inspect or change the value, then let the setter run
        next(telemetry, value);
    }
}

DeserializeXmlOptions options = new();
options.Telemetries.Add<Log>();
options.Policies.Add<Log>(new AuditingPolicy());                 // every member of Log
options.Policies.Add<Log>(nameof(Log.LogKey), new AuditingPolicy()); // only LogKey
```

- **Attaching**: `[Policy<TPolicy>]` on a telemetry class or member, or through `options.Policies`: for
  every telemetry type (`Add(policy)`), one type (`Add<TTelemetry>(policy)`) or one member
  (`Add<TTelemetry>(memberName, policy)`, `Set` only). The options win over attributes; the most specific
  target wins, and policies added through the options for the same target run in the order added.
- **Lifetime**: a policy lives as long as its options; keep what one enumeration needs in its
  deserialization context, not in the policy's fields. `Clear(context)` runs once when each
  enumeration ends. Options with enumeration policies serve one enumeration at a time: use one
  options instance per concurrent deserialization.
- Policies are configuration: once the options are used, adding one throws.

### Telemetries that need each other: the deserialization context

Some telemetries carry what other telemetries need: a site whose key every order should carry, a contact whose
name belongs on its site. Each enumeration has a **deserialization context**
(`DeserializeContext`, in `Forestry.Serialization.Reading`) that its enumeration policies share.
Every telemetry is in it from its start to its end (open), so a policy sees the telemetries around the one it is
building. When a telemetry completes, its policies decide two things, each at most once:

- **whether it comes out**: `telemetry.Enumerate(false)` keeps it out of the enumeration;
- **how long it stays**: `telemetry.Keep(Lifetime.UntilEnd)` or `telemetry.Keep(Lifetime.UntilReleased)`, until
  a later policy calls `context.Release(telemetry)`.

What no policy decides gets the default: a telemetry comes out when its type was added to the options, and
leaves the context at its end. Here every order carries its site's key, and no site comes out:

```csharp
public sealed class SiteKeptOut : EnumerationPolicy
{
    public override void Complete(ContextTelemetry telemetry, DeserializeContext context, Action<ContextTelemetry> next)
    {
        telemetry.Enumerate(false);         // no Site telemetries in the enumeration
        telemetry.Keep(Lifetime.UntilEnd);  // telemetries after the site can still find it
        next(telemetry);
    }
}

public sealed class OrderWithSiteKey : EnumerationPolicy
{
    public override void Complete(ContextTelemetry telemetry, DeserializeContext context, Action<ContextTelemetry> next)
    {
        Site site = context.Telemetries<Site>().Single().Telemetry.AsType<Site>();
        telemetry.Set(nameof(Order.SiteKey), site.SiteKey);
        next(telemetry);
    }
}

DeserializeXmlOptions options = new();
options.Telemetries.Add<Site>().Add<Order>();
options.Policies.Add<Site>(new SiteKeptOut()).Add<Order>(new OrderWithSiteKey());
```

- **Finding telemetries**: `context.Telemetries<TTelemetry>()` gives the open and kept telemetries of a type, oldest first, as
  a snapshot (releasing telemetries while you loop over it takes effect for the next query). Telemetries nest by
  the document's structure, so `context.Parent(telemetry)`, `Ancestors(telemetry)`, `Children(telemetry)` and
  `Siblings(telemetry)` find related telemetries still in the context; a parent that already left is not found.
- **Only what has been read so far**: an open telemetry's members that come later in the document are not
  set yet. `telemetry.IsSet(nameof(Site.SiteKey))` tells, whether the document set it (an empty element
  included) or a policy did through `telemetry.Set(...)`; a plain C# assignment to `telemetry.Telemetry` is not
  tracked.
- **Changing telemetries**: `telemetry.Set(name, value)` works on open telemetries and on kept telemetries that did not come
  out. A telemetry that came out no longer changes: setting one of its members throws. When a telemetry both
  comes out and stays, you get a copy, so nothing you do to it reaches the context, and nothing a
  policy does later reaches yours.
- **Limits and lifetime**: the context keeps at most `options.MaxContextTelemetries` telemetries (default 100;
  open telemetries do not count). Going past it throws a `DeserializeContextLimitException` listing the
  kept telemetries per type. When the enumeration ends, each policy's `Clear(context)` runs once, then
  every telemetry leaves the context.

#### Values inside wrappers, attributes and text (XML)

- **Wrappers**: a member is read only from a direct child of its telemetry's element. For a value one
  level down (`<Stem><SingleTreeProcessedStem><DBH>399</DBH>...`), add the wrapper as a telemetry type
  that does not come out, and let its policy copy the value onto `context.Parent(telemetry)`. A wrapper
  that tells values apart (e.g. `<BoomPositioning boomPositioningCategory="Felling">`) works the
  same way, its policy choosing which member of the parent to set.
- **Attributes and text** come from `Forestry.Serialization.Xml`'s own attributes, since where a value
  comes from is the format's:

  ```csharp
  public sealed class Price : Telemetry
  {
      [XmlAttributeName("currency")]
      public string? Currency { get; set; }   // <Price currency="EUR">

      [XmlElementText]
      public decimal Amount { get; set; }     // <Price ...>0.59</Price>
  }
  ```

  A member marked `[XmlAttributeName]` is set only from that attribute of the telemetry's own element,
  never from a child element of the same name. The text member (or the one named with
  `options.ElementText<TTelemetry>(memberName)`, which wins) takes the last text of the telemetry's own element;
  with no text it is set empty. A member has one source: two of `[XmlElementName]`,
  `[XmlAttributeName]` and `[XmlElementText]` on it fail. Attributes no member asks for are ignored.

### Failing fast

- Before any byte is read, at the `Deserialize` call: no telemetry type added, a member that is not a single
  value (a nested structure, or a struct that formats only to chars), a member whose type no converter
  handles (every failing member of a telemetry type in one message), no constructor that can be chosen, a chosen constructor with parameters,
  two telemetry types or two members read from the same element, and a policy that cannot apply (one on
  a member overriding more than `Set`, or registered for a type or member the options do not have).
- Also before any byte is read (XML): two members reading the same attribute, two text members
  in one telemetry type, or a member with more than one source; `MaxContextTelemetries` changed after the options are used.
- While reading, from a policy: a completion action taken twice or on another telemetry than the one
  completing, a member set on a telemetry that already came out, or more kept telemetries than
  `MaxContextTelemetries` allows.
- While reading: a value that cannot be converted throws a `FormatException` naming the member, its
  path in the document (e.g. `Root/Log/LogKey`) and the class. A document cut off inside a telemetry
  throws, and that telemetry is never handed out.

## Status

Spec 001's four user stories are done ([tasks.md](specs/001-walking-skeleton/tasks.md)): the
synchronous and asynchronous entry points, `Telemetry.Is<T>()` / `Telemetry.AsType<T>()` (a type check and a
checked cast naming both classes when it fails), and policies attached by attribute or by options.
Spec 002's three are done too ([tasks.md](specs/002-reading-context/tasks.md)): the deserialization
context, telemetries of several types nested in one pass with their containment relationships, and XML
attributes and element text as members. Spec 003's three are done too
([tasks.md](specs/003-big-data-orientation/tasks.md)): flat telemetries checked when the options lock, names
in the document declared with the XML package's attributes, and one vocabulary, format and flatten.
The policy model is good enough for the POC; its polish
before a release is tracked separately.

Later slices add relationships by keys and references, and telemetries that take their ancestors as
constructor parameters (e.g. a `Log` built with its `Stem`).

## Tests

```bash
dotnet test test/Forestry.Serialization.Tests.csproj
```

The core's tests need no document: definitions, constructor choice and converter resolution.
Flattening is tested through the real XML reader in `Forestry.Serialization.Xml`. Slow performance
tests (trait `Category=Performance`) are left out of a plain `dotnet test` and run as the gate at
the end of every user story: `dotnet test -s Performance.runsettings`.
