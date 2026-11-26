namespace Forestry.Serialization.Writing
{
    /// <summary>
    /// A write-only stream over an inner <see cref="Stream"/> that knows about one collection (spec 004 research
    /// R51): how many telemetries (<see cref="Sequence"/>) and bytes (<see cref="BytesWritten"/>) went in, whether
    /// it is full, and how it completed. Pass it to <c>TelemetrySerialization.Serialize</c> in place of a plain
    /// stream; monitoring formatters derive from it
    /// </summary>
    /// <remarks>
    /// Completing (<see cref="Complete"/>, <see cref="CompleteAsync"/>, or disposing) flushes every byte to the
    /// inner stream, disposes it unless <see cref="MonitoringCollectionOptions.LeaveOpen"/>, then calls
    /// <see cref="OnCompleted"/>: without an exception the collection is ready to hand on; with one it is
    /// abandoned. Disposing completes without an exception.
    /// </remarks>
    public class MonitoringCollectionStream : Stream, ICollectionWriter, ICollectionWriterSequence
    {
        private readonly Stream _inner;

        private bool _completed;

        /// <summary>
        /// A collection over <paramref name="inner"/>
        /// </summary>
        /// <param name="inner">Where the bytes go</param>
        /// <param name="options">The collection's settings; <see cref="MonitoringCollectionOptions.Default"/> when null</param>
        /// <exception cref="ArgumentException"><paramref name="inner"/> cannot be written to</exception>
        public MonitoringCollectionStream(Stream inner, MonitoringCollectionOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(inner);

            if (!inner.CanWrite)
            {
                throw new ArgumentException(Formatting.StreamCannotWrite, nameof(inner));
            }

            _inner = inner;
            Options = options ?? MonitoringCollectionOptions.Default;
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

        /// <inheritdoc/>
        public override bool CanRead => false;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <summary>
        /// True until the collection is completed
        /// </summary>
        public override bool CanWrite => !_completed;

        /// <inheritdoc/>
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// Write to the inner stream and count the bytes
        /// </summary>
        /// <param name="buffer"></param>
        /// <exception cref="InvalidOperationException">The collection is completed</exception>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ThrowWhenCompleted();
            _inner.Write(buffer);
            BytesWritten += buffer.Length;
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        /// <summary>
        /// Write to the inner stream asynchronously and count the bytes
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">The collection is completed</exception>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ThrowWhenCompleted();
            await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            BytesWritten += buffer.Length;
        }

        /// <inheritdoc/>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        /// <inheritdoc/>
        public override void Flush() => _inner.Flush();

        /// <inheritdoc/>
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <summary>
        /// End the collection: every byte reaches the inner stream, which is disposed unless left open, then
        /// <see cref="OnCompleted"/> is told ready (no exception) or abandoned. Once only
        /// </summary>
        /// <param name="exception">Why the collection is abandoned; null when it is ready</param>
        public void Complete(Exception? exception = null)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _inner.Flush();

            if (!Options.LeaveOpen)
            {
                _inner.Dispose();
            }

            OnCompleted(exception);
        }

        /// <summary>
        /// As <see cref="Complete"/>, flushing and disposing asynchronously
        /// </summary>
        /// <param name="exception">Why the collection is abandoned; null when it is ready</param>
        /// <returns></returns>
        public async ValueTask CompleteAsync(Exception? exception = null)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            await _inner.FlushAsync().ConfigureAwait(false);

            if (!Options.LeaveOpen)
            {
                await _inner.DisposeAsync().ConfigureAwait(false);
            }

            OnCompleted(exception);
        }

        /// <summary>
        /// The collection ended, after every byte reached the inner stream: ready to hand on when
        /// <paramref name="exception"/> is null, abandoned otherwise. Does nothing by default
        /// </summary>
        /// <param name="exception"></param>
        protected virtual void OnCompleted(Exception? exception)
        {
        }

        /// <summary>
        /// Disposing completes the collection without an exception, if it is not completed yet
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Complete();
            }

            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            await CompleteAsync().ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
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
