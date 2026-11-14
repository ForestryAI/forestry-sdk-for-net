# Contract: Forestry.StanForD public API

Shapes follow [data-model.md](../data-model.md). Built in US2 (2026-11-13) unless marked
*proposed*.

## Rows

```csharp
namespace Forestry.StanForD;

public sealed class Stem : Forestry.Deserialize.Row { /* members per data-model.md */ }
public sealed class Log  : Forestry.Deserialize.Row { /* members per data-model.md */ }

// Read for the stems and logs, never come out
public sealed class HarvestedProductionHeader : Forestry.Deserialize.Row { }
public sealed class Machine : Forestry.Deserialize.Row { }
public sealed class ObjectDefinition : Forestry.Deserialize.Row { }
public sealed class SpeciesGroupDefinition : Forestry.Deserialize.Row { }
public sealed class OperatorDefinition : Forestry.Deserialize.Row { }
public sealed class ProductDefinition : Forestry.Deserialize.Row { }
public sealed class LogVolume : Forestry.Deserialize.Row { }       // categories (attributes) and value (text)
public sealed class LogMeasurement : Forestry.Deserialize.Row { }  // category (attribute) and LogLength
public sealed class LogDiameter : Forestry.Deserialize.Row { }     // category (attribute) and value (text)
public sealed class SubObject, ContactInformation, ClassifiedProductDefinition,
    ProductDestination, LoggingContractor : Forestry.Deserialize.Row { }  // US7, copied onto definitions
public sealed class StemCoordinates, Latitude, Longitude, BoomPositioning,
    SingleTreeProcessedStem : Forestry.Deserialize.Row { }                // US8, copied onto the stem

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
    public static DeserializeXmlOptions AddHarvesting(
        this DeserializeXmlOptions options,
        Action<HarvestingOptions>? configure = null);
}

public sealed class HarvestingOptions
{
    // FR-012: fail by default
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
| A stem, log or definition with no machine key read before it | `InvalidDataException`, naming the row and `MachineKey` |
| A log not inside a stem with a stem key | `InvalidDataException`, naming the log |
| A reference key with no definition earlier in the document | `InvalidDataException`, naming the row, the key and its value |
| A common definition with no key | `InvalidDataException`, naming the definition and its key |
| A stem or log with no header `CreationDate` read before it | `InvalidDataException`, naming the row and `CreationDate` |
| A stem with no `ObjectKey` (no contract) | `InvalidDataException`, naming the row and `ObjectKey` |
| A StanForD text naming no enum member | `FormatException` (from the converter) |
| A volume, diameter or measured length matching no condition (`Fail`) | `InvalidDataException`, naming the element, its categories and the log |
| A second volume or diameter for a member already set | `InvalidDataException`, naming the element, its categories, the log and the member |

Forestry.Deserialize's own failures (conversion, cut-off document, context limit) are unchanged.

## Policies (internal)

Built: `MachinePolicy`, `CommonDefinitionPolicy<TDefinition>`, `StemIdentificationPolicy` and
`LogIdentificationPolicy`; `HeaderPolicy`, `StemPartitionPolicy` and `LogPartitionPolicy` (US3), all
acting when a row completes (`Complete`), in `src/Policies/`; `LogVolumePolicy`,
`LogMeasurementPolicy` and `LogDiameterPolicy` (US4), each setting the open log's member its
categories match. The definition part policies (US7), `StemDimensionPolicy` and
`LogDimensionPolicy` (US7), and the stem metric policies (US8) follow the same pattern; an
unknown boom positioning or coordinate category follows `HarvestingOptions.Unmatched`.
