using System.Buffers;

namespace Forestry.Serialization.Writing
{
    /// <summary>
    /// Settings of one collection: whether completing it leaves its inner target open, its limits, and how a
    /// collection <c>PipeWriter</c> created over a stream buffers (spec 004 research R53). Immutable once
    /// created, shared by <see cref="MonitoringCollectionStream"/> and <see cref="MonitoringCollectionPipeWriter"/>
    /// </summary>
    /// <remarks>
    /// Monitoring collects telemetries into big data ready collections, then later analyses them; a collection
    /// is what is collected (spec 004 clarifications).
    /// </remarks>
    public sealed class MonitoringCollectionOptions
    {
        /// <summary>
        /// No limits, the inner target disposed or completed with the collection, default buffering
        /// </summary>
        public static MonitoringCollectionOptions Default { get; } = new();

        /// <summary>
        /// Whether completing the collection writer leaves its inner <see cref="Stream"/> undisposed, or its
        /// inner <c>PipeWriter</c> uncompleted. Default false
        /// </summary>
        public bool LeaveOpen { get; init; }

        /// <summary>
        /// Bytes after which the collection is full, checked between telemetries; null for no limit
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Not positive</exception>
        public long? TargetSize
        {
            get => _targetSize;
            init
            {
                if (value is { } size)
                {
                    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size, nameof(TargetSize));
                }

                _targetSize = value;
            }
        }

        private readonly long? _targetSize;

        /// <summary>
        /// Telemetries after which the collection is full; null for no limit
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Not positive</exception>
        public long? MaxTelemetries
        {
            get => _maxTelemetries;
            init
            {
                if (value is { } count)
                {
                    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count, nameof(MaxTelemetries));
                }

                _maxTelemetries = value;
            }
        }

        private readonly long? _maxTelemetries;

        /// <summary>
        /// Whether <see cref="TargetSize"/> means compressed bytes. Default false: the bytes the collection
        /// writer sees. Kept for formats that compress (research R53)
        /// </summary>
        public bool SizeIsCompressed { get; init; }

        /// <summary>
        /// Minimum buffer size of the <c>PipeWriter</c> that <see cref="MonitoringCollectionPipeWriter.Create"/>
        /// creates over a stream. Default 4096
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Not positive</exception>
        public int MinimumBufferSize
        {
            get => _minimumBufferSize;
            init
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(MinimumBufferSize));
                _minimumBufferSize = value;
            }
        }

        private readonly int _minimumBufferSize = 4096;

        /// <summary>
        /// Memory pool of the <c>PipeWriter</c> that <see cref="MonitoringCollectionPipeWriter.Create"/> creates
        /// over a stream; null for the shared pool
        /// </summary>
        public MemoryPool<byte>? Pool { get; init; }

        /// <summary>
        /// Full once a limit is reached
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="bytesWritten"></param>
        /// <returns></returns>
        internal bool IsFull(long sequence, long bytesWritten) =>
            (TargetSize is { } size && bytesWritten >= size) || (MaxTelemetries is { } max && sequence >= max);
    }
}
