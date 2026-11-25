# Contract: Public API added or changed

**Plan**: [../plan.md](../plan.md) | Decisions: [../research.md](../research.md)

Signatures are the contract; XML documentation follows D0. Breaking changes are accepted (4.0.0).

## Changed

```csharp
namespace Forestry.Serialization.Converters
{
    public abstract class Converter<T> : Converter
    {
        public abstract T Read(ReadOnlySpan<byte> utf8);                   // was Convert (R56)
        public virtual void Write(T value, IBufferWriter<byte> writer);     // new (R45); default throws NotSupportedException
    }
}

namespace Forestry.Serialization
{
    public abstract partial class DeserializeOptions : TelemetrySerializerOptions   // was a root class (R44)
    {
        // Telemetries, Converters, FormatConverters, IsReadOnly, MakeReadOnly move to the base.
        // Policies, MaxContextTelemetries, enumerator factories stay here.
    }
}
```

## Added

```csharp
namespace Forestry.Serialization
{
    public abstract class TelemetrySerializerOptions
    {
        public TelemetryTypes Telemetries { get; }
        public IList<Converter> Converters { get; }
        protected virtual IEnumerable<Converter> FormatConverters { get; }
        public bool IsReadOnly { get; }
        public void MakeReadOnly();
    }

    public abstract class SerializeOptions : TelemetrySerializerOptions
    {
        public MonitoringCollectionOptions DefaultCollectionOptions { get; set; }   // settable until locked
        protected internal abstract ObjectConverter CreateObjectConverter(TypeDefinition definition);
    }

    public static partial class TelemetrySerializer
    {
        public static void Serialize(Telemetry telemetry, Stream utf8, SerializeOptions options);
        public static void Serialize(IEnumerable<Telemetry> telemetries, Stream utf8, SerializeOptions options);

        public static ValueTask SerializeAsync(Telemetry telemetry, Stream utf8, SerializeOptions options, CancellationToken cancellationToken = default);
        public static Task SerializeAsync(IAsyncEnumerable<Telemetry> telemetries, Stream utf8, SerializeOptions options, CancellationToken cancellationToken = default);

        public static ValueTask SerializeAsync(Telemetry telemetry, PipeWriter utf8, SerializeOptions options, CancellationToken cancellationToken = default);
        public static Task SerializeAsync(IAsyncEnumerable<Telemetry> telemetries, PipeWriter utf8, SerializeOptions options, CancellationToken cancellationToken = default);
    }
}

namespace Forestry.Serialization.Writing
{
    public abstract class ObjectConverter
    {
        protected ObjectConverter(TypeDefinition definition);
        public TypeDefinition Definition { get; }
        protected virtual void WriteStart(IBufferWriter<byte> writer);
        protected virtual void WriteMember(PropertyDefinition property, int index, IBufferWriter<byte> writer, Action<IBufferWriter<byte>> writeValue);
        protected virtual void WriteNull(PropertyDefinition property, int index, IBufferWriter<byte> writer);
        protected virtual void WriteEnd(IBufferWriter<byte> writer);
    }

    public interface ICollectionWriter
    {
        MonitoringCollectionOptions Options { get; }
        long Sequence { get; }
        long BytesWritten { get; }
        bool IsFull { get; }
    }

    public sealed class MonitoringCollectionOptions
    {
        public static MonitoringCollectionOptions Default { get; }
        public bool LeaveOpen { get; init; }
        public long? TargetSize { get; init; }
        public long? MaxTelemetries { get; init; }
        public bool SizeIsCompressed { get; init; }
        public int MinimumBufferSize { get; init; }      // default 4096
        public MemoryPool<byte>? Pool { get; init; }
    }

    public class MonitoringCollectionStream : Stream, ICollectionWriter
    {
        public MonitoringCollectionStream(Stream inner, MonitoringCollectionOptions? options = null);
        public void Complete(Exception? exception = null);
        public ValueTask CompleteAsync(Exception? exception = null);
        protected virtual void OnCompleted(Exception? exception);
        // Write/WriteAsync/Flush/FlushAsync overridable; CanRead/CanSeek false; CanWrite true until completed.
    }

    public class MonitoringCollectionPipeWriter : PipeWriter, ICollectionWriter
    {
        public MonitoringCollectionPipeWriter(PipeWriter inner, MonitoringCollectionOptions? options = null);
        public static MonitoringCollectionPipeWriter Create(Stream utf8, MonitoringCollectionOptions? options = null);
        protected virtual void OnCompleted(Exception? exception);
        // GetSpan, GetMemory, FlushAsync, CancelPendingFlush, UnflushedBytes forward; Advance counts;
        // Complete/CompleteAsync complete the inner writer unless LeaveOpen, then OnCompleted.
    }
}
```

## Behavior guarantees

- Serializing never disposes a caller's `Stream` nor completes a caller's `PipeWriter` (FR-014).
- `Sequence` rises by one after each whole telemetry, only for an `ICollectionWriter` target
  (FR-019).
- A telemetry type not added to the options fails before any byte of it is written (FR-017).
- Options lock on first serialize; a member whose converter does not override `Write` fails then
  (FR-008).
- `ValueTask` results follow the usual rules: await once, never concurrently.
