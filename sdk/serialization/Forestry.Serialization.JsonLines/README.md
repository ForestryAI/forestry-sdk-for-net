# Forestry Serialization JsonLines

The [JSON Lines](https://jsonlines.org/) format for
[Forestry.Serialization](../Forestry.Serialization/README.md): `SerializeJsonLinesOptions` writes each
telemetry as one JSON object on its own line, through the core's `TelemetrySerialization.Serialize`. It
is the first format that **writes** telemetries, and the one the POC uses for Azure Data Explorer
external tables.

```csharp
public sealed class Log : Telemetry
{
    public int LogKey { get; set; }
    public decimal Volume { get; set; }
    public CuttingReason Reason { get; set; }
    public DateOnly? Delivered { get; set; }
}

SerializeJsonLinesOptions options = new();
options.Telemetries.Add<Log>();

using FileStream stream = File.Create("logs.jsonl");

foreach (Log log in logs)
{
    TelemetrySerialization.Serialize(log, stream, options);
}
```

```json
{"LogKey":1,"Volume":0.214,"Reason":"Automatic","Delivered":null}
{"LogKey":2,"Volume":0.187,"Reason":"Top break","Delivered":"2026-11-27"}
```

## Why JSON Lines

JSON Lines is where Forestry's writing side starts, and the format to copy when you write your own:

- **People can read it.** Every telemetry is one line of plain JSON with its members' names: open the
  file in any editor, `grep` it, diff two runs, or paste a line into a ticket. When a device document
  turns into telemetries you did not expect, a JSON Lines file shows you what came out.
- **It takes very little code.** The whole package is about 120 lines of code, plus documentation:
  the telemetry writer (about 75), the options, one attribute and one enum converter. Everything else,
  walking the telemetry's members, converters for every flat member type, null handling, streaming to a
  `Stream` or a `PipeWriter`, counting telemetries into collections, comes from
  [Forestry.Serialization](../Forestry.Serialization/README.md). System.Text.Json's `Utf8JsonWriter`
  does the commas, escaping and validation.
- **It is easy to test.** Serialize into a `MemoryStream`, split on `\n`, and parse each line with
  `JsonDocument`: no schema, no reader of your own, no binary to decode. This package's tests and
  StanForD's round trip (HPR XML → telemetries → JSON Lines → parsed and compared) work that way.
- **Big data can read it.** Azure Data Explorer external tables and most data lakes take JSON Lines as
  it is; partitions come from the folder paths a client writes the files into, not from the format.

## Writing your own format

A format that writes is a `SerializeOptions` and a `TelemetryWriter`. This package is the reference:
read [`JsonLinesTelemetryWriter.cs`](src/JsonLinesTelemetryWriter.cs) and
[`SerializeJsonLinesOptions.cs`](src/SerializeJsonLinesOptions.cs) first.

1. **Options**: derive from `SerializeOptions` and return a new writer from `CreateWriter()`, one per
   serializer call. Register your own converters in `FormatConverters` where a value needs a different
   form (here, enums that may be numbers).
2. **Framing**: override `WriteStartTelemetry`, `WriteName`, `WriteNull` and `WriteEndTelemetry`
   (here `{`, `"name":`, `"name":null` and `}` with a new line).
3. **Values**: override the `WriteValue` overloads your format represents differently, by what the value
   is (`string`, `bool`, numbers, `Guid`, dates, times, durations), and `WriteEnumName`. The defaults
   write plain invariant UTF-8 text through `Output`, so a simple text format overrides little.
4. **Refuse what you cannot write**: throw when a value has no form in your format (here NaN), and, if
   your writer buffers, override `GetSpan`, `GetMemory` and `Advance` to throw so converters use the
   value methods.
5. **Test** it as this package does: serialize hand-built telemetries with every member kind into a
   `MemoryStream`, parse the output, and compare; then the same bytes through a `PipeWriter`.

## What it writes

| Member | JSON |
|---|---|
| `string`, `Guid` | string, escaped as JSON requires; non-ASCII text stays plain UTF-8 |
| `bool` | `true` / `false` |
| `int`, `long`, `double`, `decimal` | number, every digit of a `decimal` |
| `DateTime`, `DateTimeOffset` | ISO 8601 string, round-trip |
| `DateOnly`, `TimeOnly`, `TimeSpan` | string: `2026-11-27`, `13:20:00.0000000`, `1.02:03:04.5000000` |
| enum | its name (or `[EnumMemberName]`) as a string; a number with `[JsonLinesEnumForm(EnumForm.Number)]` on the member |
| no value | `null` |

Members are written by their C# names, in definition order. A `double` that is NaN or infinite has no
JSON form and throws `NotSupportedException` naming the member. A converter registered in the options
wins over the format's (e.g. StanForD's enums always write their StanForD texts).

## Dependencies

- **.NET 10** (`net10.0`), the only target framework.
- **[Forestry.Serialization](../Forestry.Serialization/README.md)**: the format-agnostic core.
- **System.Text.Json**, part of .NET. **No third-party packages.**

## Status

Spec 005 (#138): writing to a `Stream` or a `PipeWriter`, one telemetry type per output. Not yet:
reading JSON Lines back, options (naming, leaving nulls out, number and date formats, gzip), and
collecting settings (#141).

## Tests

```bash
dotnet test test/Forestry.Serialization.JsonLines.Tests.csproj
```
