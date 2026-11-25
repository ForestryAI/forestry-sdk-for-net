# Quickstart: validating writing telemetries

**Plan**: [plan.md](plan.md) | API: [contracts/public-api.md](contracts/public-api.md)

## Prerequisites

- .NET SDK from `global.json`, run from the repository root.
- Branch `serialization-004-writing`.

## Build and test

```powershell
dotnet test sdk/serialization/Forestry.Serialization/Forestry.Serialization.slnx
dotnet test sdk/serialization/Forestry.Serialization.Xml/Forestry.Serialization.Xml.slnx
dotnet test sdk/stanford/Forestry.StanForD/Forestry.StanForD.slnx
```

Expected: all pass, including every test that existed before this spec (SC-002).

## Scenarios

| # | Scenario | Where | Expected |
|---|---|---|---|
| 1 | Each built-in converter writes then reads a value | `ConverterWriteTests` | Same value back (SC-001) |
| 2 | Two telemetries serialized into a `MemoryStream` with the string test format; rewind; `new StreamReader(memory, leaveOpen: true).ReadToEnd()` | `SerializeTests` | Two lines, in order; stream still open |
| 3 | Same telemetries: sync `Stream`, async `Stream`, `PipeWriter` over a `Pipe` | `SerializeTests` | Identical bytes (SC-003); target not disposed or completed (SC-004) |
| 4 | Telemetry type not added; member that cannot be written | `SerializeTests` | Fails before any byte; message names type and member (SC-007) |
| 5 | Three telemetries into a `MonitoringCollectionStream` over a `MemoryStream` | `CollectionWriterTests` | `Sequence` 3; `BytesWritten` = length (SC-006) |
| 6 | `TargetSize` or `MaxTelemetries` passed | `CollectionWriterTests` | `IsFull` only after a whole telemetry |
| 7 | `Complete()` and `Complete(exception)`; `LeaveOpen` true and false | `CollectionWriterTests` | Hook told ready / abandoned; inner disposed only without `LeaveOpen` |
| 8 | Same as 5 through `MonitoringCollectionPipeWriter` | `CollectionWriterTests` | Same counts as the stream |
| 9 | `foreach` over `Deserialize` of a generated document, `Serialize` each into one collection stream over `Stream.Null`, N and 10×N telemetries | `Forestry.Serialization.Xml.Tests` `SerializeMemoryTests` | Heap growth flat; `Sequence` = telemetries read (SC-005) |

## Documentation check

The package README's new "Serializing" section serializes into a `MemoryStream` and reads the
string back; following it alone works (SC-008).
