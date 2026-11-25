# Data Model: Writing telemetries

**Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

## Options

| Type | Kind | Holds | Lifecycle |
|---|---|---|---|
| `TelemetrySerializerOptions` | abstract base (R44) | `Telemetries`, `Converters`, `FormatConverters`, type and property definitions | Mutable until `MakeReadOnly` (first use); then definitions are built and frozen |
| `DeserializeOptions` | abstract, derives from the base | `Policies`, `MaxContextTelemetries`, enumerator factories, BOM handling | Unchanged |
| `SerializeOptions` | abstract, derives from the base | `CreateObjectConverter(TypeDefinition)` (abstract, the format's), `DefaultCollectionOptions` | Locks on first serialize; a member whose converter cannot write fails here (S1) |
| `MonitoringCollectionOptions` | sealed, `init` only (R53) | `LeaveOpen`, `TargetSize`, `MaxTelemetries`, `SizeIsCompressed`, `MinimumBufferSize`, `Pool`, `Default` | Immutable from creation |

Relationships: one options instance → its own frozen definitions (never shared between
instances). `SerializeOptions.DefaultCollectionOptions` → one `MonitoringCollectionOptions`.

## Definitions

| Type | Change | Validation |
|---|---|---|
| `TypeDefinition` | Built from `TelemetrySerializerOptions` | Flat rule (C7, R32) |
| `PropertyDefinition<T>` | Adds a compiled getter `Func<Telemetry, T>` and internal `WriteValue` (R46) | Resolved converter must override `Write` when built for `SerializeOptions` (R45) |

## Converters

| Member | Signature | Rule |
|---|---|---|
| `Read` | `T Read(ReadOnlySpan<byte> utf8)` | Renamed from `Convert`; behavior unchanged |
| `Write` | `virtual void Write(T value, IBufferWriter<byte> writer)` | Never given null; asks for more room instead of failing (FR-002) |

## Writing

| Type | Kind | Fields / members | Notes |
|---|---|---|---|
| `ObjectConverter` | abstract (R49) | `WriteTelemetry`, overridable `WriteStart`, `WriteMember`, `WriteNull`, `WriteEnd` | One per type definition, created by the format |
| `PooledBufferWriter` | internal | rented segments, `WrittenSpan`, `Reset` | Lives for one call (R48) |
| `ICollectionWriter` | interface (R51) | `Options`, `Sequence`, `BytesWritten`, `IsFull` | Recognized by `TelemetrySerializer` |
| `MonitoringCollectionStream` | `Stream`, public, not sealed | inner `Stream`, options, counters, completion state | Counts in `Write`/`WriteAsync` |
| `MonitoringCollectionPipeWriter` | `PipeWriter`, public, not sealed | inner `PipeWriter`, options, counters, completion state | Counts in `Advance`; static `Create(Stream, options)` |

### Collection writer states

```text
Open ──(telemetry written: Sequence+1, BytesWritten+n)──▶ Open
Open ──(IsFull after a telemetry)──▶ Open (full; the collector decides)
Open ──Complete(null)──▶ Completed (ready)      : bytes reach inner, inner disposed/completed unless LeaveOpen, OnCompleted(null)
Open ──Complete(ex)────▶ Completed (abandoned)  : same, OnCompleted(ex)
Completed ──write──▶ InvalidOperationException
```

`IsFull` = (`TargetSize` set and `BytesWritten` ≥ `TargetSize`) or (`MaxTelemetries` set and
`Sequence` ≥ `MaxTelemetries`), evaluated after a telemetry (R52).
