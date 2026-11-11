# Contract: Forestry.StanForD public API (proposed)

Names are proposals (C7); shapes follow [data-model.md](../data-model.md) and
[research.md](../research.md).

## Rows

```csharp
namespace Forestry.StanForD.Harvesting;

public sealed class Stem : Forestry.Deserialize.Row { /* members per data-model.md */ }
public sealed class Log  : Forestry.Deserialize.Row { /* members per data-model.md */ }
```

Context rows (`Machine`, `HarvestedProductionHeader`, the definitions) are `internal`.

## One call to configure the options

```csharp
namespace Forestry.StanForD.Harvesting;

public static class HarvestingOptionsExtensions
{
    // Adds Stem, Log, the context rows and the three policies to the options.
    public static DeserializeXmlOptions AddHarvesting(
        this DeserializeXmlOptions options,
        Action<HarvestingOptions>? configure = null);
}

public sealed class HarvestingOptions
{
    // FR-012: Fail by default.
    public UnmatchedOccurrence Unmatched { get; set; } = UnmatchedOccurrence.Fail;
}

public enum UnmatchedOccurrence { Fail, Ignore }
```

## Use

```csharp
DeserializeXmlOptions options = new DeserializeXmlOptions().AddHarvesting();

await foreach (Row row in Deserialization.DeserializeAsync(stream, options, cancellationToken))
{
    if (row.Is<Log>()) { Log log = row.AsType<Log>(); /* write it out */ }
}
```

## Failures

| When | Exception |
|---|---|
| A Stem starts with no machine key read | `InvalidDataException`, naming the stem element |
| A reference key not defined earlier in the document | `InvalidDataException`, naming the stem and key |
| An occurrence matching no condition (`Fail`) | `InvalidDataException`, naming element and attributes |
| Two occurrences for one conditional member in a row | `InvalidDataException`, naming member and element |

The exception type is a proposal; Forestry.Deserialize's own failures (conversion, cut-off
document) are unchanged.

## Policies (internal)

Identification and timestamp policies (member-level `Set` on key members), conditional mapping
policies (on context rows such as `LogVolume` and `LogDiameter`) and a schema version constraint
policy (on the root context row): enumeration policies on Forestry.Deserialize, internal to the
package; `AddHarvesting` adds the context rows and attaches the policies.
