# Contract: Forestry.StanForD public API

Shapes follow [data-model.md](../data-model.md). Built in US2 (2026-11-13) unless marked
*proposed*.

## Rows

```csharp
namespace Forestry.StanForD;

public sealed class Stem : Forestry.Deserialize.Row { /* members per data-model.md */ }
public sealed class Log  : Forestry.Deserialize.Row { /* members per data-model.md */ }

// Read for the stems and logs, never come out
public sealed class Machine : Forestry.Deserialize.Row { }
public sealed class ObjectDefinition : Forestry.Deserialize.Row { }
public sealed class SpeciesGroupDefinition : Forestry.Deserialize.Row { }
public sealed class OperatorDefinition : Forestry.Deserialize.Row { }
public sealed class ProductDefinition : Forestry.Deserialize.Row { }

public static class Identification
{
    public static string Format(Guid machineKey, int key);              // "{machine key N}-{key}"
    public static string Format(Guid machineKey, int key, int subkey);  // "{machine key N}-{key}-{subkey}"
}
```

The common definitions are public because policies and Forestry.Deserialize's options need them;
only Stem and Log come out of a Harvesting document.

## One call to configure the options

```csharp
namespace Forestry.StanForD;

public static class HarvestingOptionsExtensions
{
    // Adds Stem and Log, the machine and the common definitions, the StanForD enum converters
    // and the policies to the options.
    public static DeserializeXmlOptions AddHarvesting(this DeserializeXmlOptions options);
}
```

*Proposed for US4*: an overload taking `Action<HarvestingOptions>` with
`HarvestingOptions.Unmatched` (`UnmatchedOccurrence.Fail` by default, or `Ignore`; FR-012).

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
| A stem, log or definition with no machine key read before it | `InvalidDataException`, naming the row and `MachineKey` |
| A log not inside a stem with a stem key | `InvalidDataException`, naming the log |
| A reference key with no definition earlier in the document | `InvalidDataException`, naming the row, the key and its value |
| A common definition with no key | `InvalidDataException`, naming the definition and its key |
| A StanForD text naming no enum member | `FormatException` (from the converter) |
| *Proposed (US4)*: an occurrence matching no condition (`Fail`) | `InvalidDataException`, naming element and attributes |
| *Proposed (US4)*: two occurrences for one conditional member in a row | `InvalidDataException`, naming member and element |

Forestry.Deserialize's own failures (conversion, cut-off document, context limit) are unchanged.

## Policies (internal)

Built: `MachinePolicy`, `CommonDefinitionPolicy<TDefinition>`, `StemIdentificationPolicy` and
`LogIdentificationPolicy`, all acting when a row completes (`Complete`), in `src/Policies/`.
*Proposed*: partitioning (US3) and conditional mapping (US4) policies on the same pattern.
