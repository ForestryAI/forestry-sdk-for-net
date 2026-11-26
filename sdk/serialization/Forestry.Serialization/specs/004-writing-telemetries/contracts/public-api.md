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
        public virtual void Write(T value, TelemetryWriter writer);         // new (R45); delegates to the writer; default throws NotSupportedException
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
        protected abstract TelemetryWriter CreateWriter();   // the format's writer, one per call; protected: formats need no access to internals
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

namespace Forestry.Serialization.Definitions
{
    public abstract class PropertyDefinition
    {
        public ReadOnlyMemory<byte> Utf8Name { get; }   // new (R46): the name in UTF-8, computed once, for writers
        public int Index { get; }                       // now public (R46): position in TypeDefinition.Properties, for columnar writers
    }
}

namespace Forestry.Serialization.Writing
{
    public abstract class TelemetryWriter : IBufferWriter<byte>    // R49: one class a format or developer overrides
    {
        protected TelemetryWriter();
        protected IBufferWriter<byte> Output { get; }               // the current call's output, set by the serializer
        protected virtual void OnReset();                           // clear state kept between telemetries of a call

        // IBufferWriter<byte>, forwarding to Output, for converters writing raw bytes
        public Span<byte> GetSpan(int sizeHint = 0);
        public Memory<byte> GetMemory(int sizeHint = 0);
        public void Advance(int count);

        // Framing, called by the serializer: start; per member WriteName then the converter's Write, or WriteNull; end
        public virtual void WriteStartTelemetry(TypeDefinition definition);   // default: nothing
        public virtual void WriteEndTelemetry();                             // default: nothing
        public virtual void WriteName(PropertyDefinition property);          // default: nothing; separators are the writer's state
        public virtual void WriteNull(PropertyDefinition property);          // default: WriteName, no value

        // Values by type, called by converters (R49): named for what the value is, not how a format represents it
        public virtual void WriteValue(ReadOnlySpan<char> value);            // text; default: UTF-8, unescaped
        public virtual void WriteValue(bool value);                          // default: true / false
        public virtual void WriteValue(int value);
        public virtual void WriteValue(long value);
        public virtual void WriteValue(double value);
        public virtual void WriteValue(decimal value);
        public virtual void WriteValue(Guid value);                          // default: D
        public virtual void WriteValue(DateTime value);                      // default: round-trip O
        public virtual void WriteValue(DateTimeOffset value);                // default: round-trip O
        public virtual void WriteValue<T>(T value) where T : IUtf8SpanFormattable;   // every other value; overriding it does not change the typed overloads
        protected void WriteFormatted<T>(T value, ReadOnlySpan<char> format) where T : IUtf8SpanFormattable;  // shared by the defaults
        public virtual void WriteRaw(ReadOnlySpan<byte> utf8);               // default: bytes as they are
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
