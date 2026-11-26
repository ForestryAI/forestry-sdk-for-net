using System.IO.Pipelines;

namespace Forestry.Serialization.Writing
{
    /// <summary>
    /// A <see cref="PipeWriter"/> decorating an inner one that knows about one collection (spec 004 research
    /// R51): how many telemetries (<see cref="Sequence"/>) and bytes (<see cref="BytesWritten"/>) went in, whether
    /// it is full, and how it completed. Converters write straight into the inner writer's buffers (no copy);
    /// bytes are counted in <see cref="Advance"/>. Pass it to <c>TelemetrySerialization.SerializeAsync</c> in
    /// place of a plain pipe; monitoring formatters derive from it
    /// </summary>
    /// <remarks>
    /// Completing (<see cref="Complete"/>, <see cref="CompleteAsync"/>) gets every byte to the inner writer,
    /// completes it unless <see cref="MonitoringCollectionOptions.LeaveOpen"/> (one made by <see cref="Create"/>
    /// is always completed, and leaves its stream open as the options say), then calls
    /// <see cref="OnCompleted"/>: without an exception the collection is ready to hand on; with one it is
    /// abandoned.
    /// </remarks>
    public class MonitoringCollectionPipeWriter : PipeWriter, ICollectionWriter, ICollectionWriterSequence
    {
        private readonly PipeWriter _inner;

        /// <summary>
        /// The inner writer was made here (<see cref="Create"/>), so it is always completed
        /// </summary>
        private readonly bool _ownsInner;

        private bool _completed;

        /// <summary>
        /// A collection decorating <paramref name="inner"/>
        /// </summary>
        /// <param name="inner">Where the bytes go</param>
        /// <param name="options">The collection's settings; <see cref="MonitoringCollectionOptions.Default"/> when null</param>
        public MonitoringCollectionPipeWriter(PipeWriter inner, MonitoringCollectionOptions? options = null)
            : this(inner, options, ownsInner: false)
        {
        }

        private MonitoringCollectionPipeWriter(PipeWriter inner, MonitoringCollectionOptions? options, bool ownsInner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            _inner = inner;
            _ownsInner = ownsInner;
            Options = options ?? MonitoringCollectionOptions.Default;
        }

        /// <summary>
        /// A collection writing to <paramref name="utf8"/> through the base class library's <see cref="PipeWriter"/>
        /// (which cannot be subclassed), buffered by <see cref="MonitoringCollectionOptions.MinimumBufferSize"/>
        /// and <see cref="MonitoringCollectionOptions.Pool"/>; the stream is disposed on completion unless
        /// <see cref="MonitoringCollectionOptions.LeaveOpen"/>
        /// </summary>
        /// <param name="utf8"></param>
        /// <param name="options">The collection's settings; <see cref="MonitoringCollectionOptions.Default"/> when null</param>
        /// <returns></returns>
        public static MonitoringCollectionPipeWriter Create(Stream utf8, MonitoringCollectionOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(utf8);
            MonitoringCollectionOptions settings = options ?? MonitoringCollectionOptions.Default;
            PipeWriter inner = PipeWriter.Create(utf8, new StreamPipeWriterOptions(settings.Pool, settings.MinimumBufferSize, settings.LeaveOpen));

            return new MonitoringCollectionPipeWriter(inner, settings, ownsInner: true);
        }

        /// <inheritdoc/>
        public MonitoringCollectionOptions Options { get; }

        /// <inheritdoc/>
        public long Sequence { get; private set; }

        /// <inheritdoc/>
        public long BytesWritten { get; private set; }

        /// <inheritdoc/>
        public bool IsFull => Options.IsFull(Sequence, BytesWritten);

        void ICollectionWriterSequence.OnTelemetryWritten() => Sequence++;

        /// <summary>
        /// Count the bytes and advance the inner writer
        /// </summary>
        /// <param name="bytes"></param>
        /// <exception cref="InvalidOperationException">The collection is completed</exception>
        public override void Advance(int bytes)
        {
            ThrowWhenCompleted();
            _inner.Advance(bytes);
            BytesWritten += bytes;
        }

        /// <inheritdoc/>
        public override Memory<byte> GetMemory(int sizeHint = 0)
        {
            ThrowWhenCompleted();
            return _inner.GetMemory(sizeHint);
        }

        /// <inheritdoc/>
        public override Span<byte> GetSpan(int sizeHint = 0)
        {
            ThrowWhenCompleted();
            return _inner.GetSpan(sizeHint);
        }

        /// <inheritdoc/>
        public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default) => _inner.FlushAsync(cancellationToken);

        /// <inheritdoc/>
        public override void CancelPendingFlush() => _inner.CancelPendingFlush();

        /// <inheritdoc/>
        public override bool CanGetUnflushedBytes => _inner.CanGetUnflushedBytes;

        /// <inheritdoc/>
        public override long UnflushedBytes => _inner.UnflushedBytes;

        /// <summary>
        /// End the collection: the inner writer is completed (which flushes) unless left open, then
        /// <see cref="OnCompleted"/> is told ready (no exception) or abandoned. Once only. Left open, bytes not yet
        /// flushed stay with the inner writer: prefer <see cref="CompleteAsync"/>, which flushes them
        /// </summary>
        /// <param name="exception">Why the collection is abandoned; null when it is ready</param>
        public override void Complete(Exception? exception = null)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;

            if (_ownsInner || !Options.LeaveOpen)
            {
                _inner.Complete(exception);
            }

            OnCompleted(exception);
        }

        /// <summary>
        /// As <see cref="Complete"/>; left open, the inner writer is flushed so every byte reaches it
        /// </summary>
        /// <param name="exception">Why the collection is abandoned; null when it is ready</param>
        /// <returns></returns>
        public override async ValueTask CompleteAsync(Exception? exception = null)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;

            if (_ownsInner || !Options.LeaveOpen)
            {
                await _inner.CompleteAsync(exception).ConfigureAwait(false);
            }
            else
            {
                await _inner.FlushAsync().ConfigureAwait(false);
            }

            OnCompleted(exception);
        }

        /// <summary>
        /// The collection ended, after every byte reached the inner writer: ready to hand on when
        /// <paramref name="exception"/> is null, abandoned otherwise. Does nothing by default
        /// </summary>
        /// <param name="exception"></param>
        protected virtual void OnCompleted(Exception? exception)
        {
        }

        private void ThrowWhenCompleted()
        {
            if (_completed)
            {
                throw new InvalidOperationException(Formatting.CollectionCompleted);
            }
        }
    }
}
