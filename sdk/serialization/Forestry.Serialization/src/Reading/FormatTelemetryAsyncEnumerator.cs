using System.Buffers;
using System.IO.Pipelines;
using System.Reflection;

namespace Forestry.Serialization.Reading
{
    /// <summary>
    /// Asynchronous telemetry enumerator over a <see cref="PipeReader"/>. The core writes the moves; a format
    /// derives from it and overrides <see cref="Read"/>, driving its own reader and the core's telemetry builder
    /// (research R2). One telemetry per move
    /// </summary>
    public abstract class FormatTelemetryAsyncEnumerator : IAsyncEnumerator<Telemetry>
    {
        /// <summary>
        /// Telemetry enumerator over the bytes of a pipe
        /// </summary>
        /// <param name="pipe">UTF-8 bytes of the document</param>
        /// <param name="options"></param>
        /// <param name="cancellationToken">Cancels a move waiting for bytes</param>
        protected FormatTelemetryAsyncEnumerator(PipeReader pipe, DeserializeOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pipe);
            ArgumentNullException.ThrowIfNull(options);

            _buffering = new PipeReaderBuffering(pipe, options.SkipsByteOrderMark);
            _cancellationToken = cancellationToken;
            Builder = new TelemetryBuilder(options);
        }

        private PipeReaderBuffering _buffering;

        private readonly CancellationToken _cancellationToken;

        private bool _isDone;

        /// <summary>
        /// The core's telemetry builder for this deserialization, for a format's shared reading class
        /// </summary>
        protected TelemetryBuilder Builder { get; }

        /// <summary>
        /// Current telemetry
        /// </summary>
        public Telemetry Current { get; private set; } = null!;

        /// <summary>
        /// Read from the unconsumed bytes until the core completed a telemetry, or nothing is left
        /// </summary>
        /// <param name="bytes">Unconsumed bytes</param>
        /// <param name="isFinalBlock">No more bytes follow</param>
        /// <param name="telemetry">The telemetry completed during the call</param>
        /// <param name="consumed">Bytes consumed, reported even when 0</param>
        /// <returns></returns>
        protected abstract ReadStatus Read(ReadOnlySequence<byte> bytes, bool isFinalBlock, out Telemetry? telemetry, out long consumed);

        /// <summary>
        /// A telemetry of type <typeparamref name="T"/> starts
        /// </summary>
        /// <typeparam name="T"></typeparam>
        protected void Start<T>() where T : Telemetry => Builder.Start<T>();

        /// <summary>
        /// A property of the top open telemetry has this UTF-8 value
        /// </summary>
        /// <param name="property"></param>
        /// <param name="utf8"></param>
        /// <param name="information"></param>
        protected void Set(PropertyInfo property, ReadOnlySpan<byte> utf8, in ReadingInformation information) =>
            Builder.Set(property, utf8, in information);

        /// <summary>
        /// A field of the top open telemetry has this UTF-8 value
        /// </summary>
        /// <param name="field"></param>
        /// <param name="utf8"></param>
        /// <param name="information"></param>
        protected void Set(FieldInfo field, ReadOnlySpan<byte> utf8, in ReadingInformation information) =>
            Builder.Set(field, utf8, in information);

        /// <summary>
        /// The top open telemetry is complete
        /// </summary>
        /// <returns>The telemetry to hand out, or null</returns>
        protected Telemetry? Complete() => Builder.Complete();

        /// <summary>
        /// Move to the next telemetry: refill, read, advance by the bytes consumed (even 0), refill again
        /// while the format needs more bytes
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> MoveNextAsync()
        {
            while (!_isDone)
            {
                _buffering = await _buffering.ReadAsync(_cancellationToken).ConfigureAwait(false);

                if (_buffering.IsStarting)
                {
                    _buffering.Advance(0, isExamined: true);  // a byte order mark not complete yet
                    continue;
                }

                ReadStatus state;
                Telemetry? telemetry;

                try
                {
                    state = Read(_buffering.Bytes, _buffering.IsCompleted, out telemetry, out long consumed);
                    _buffering.Advance(consumed, isExamined: state == ReadStatus.Partial);
                }
                catch
                {
                    _buffering.Release();
                    throw;
                }

                switch (state)
                {
                    case ReadStatus.Complete when telemetry is not null:
                        Current = telemetry;
                        return true;
                    case ReadStatus.Complete:
                        continue;  // a telemetry only a constructor needs: not handed out
                    case ReadStatus.None:
                        _isDone = true;
                        Builder.Clear();  // the enumeration ended: its policies are cleared now
                        break;
                    default:
                        if (_buffering.IsCompleted)
                        {
                            Throwing.WhenPartialAtFinalBlock();
                        }
                        break;  // refill
                }
            }

            Current = null!;
            return false;
        }

        /// <summary>
        /// Release the state and the pipe's buffer; telemetries still open are never handed out
        /// </summary>
        /// <returns></returns>
        public virtual ValueTask DisposeAsync()
        {
            _isDone = true;
            _buffering.Release();
            Current = null!;
            Builder.Clear();
            GC.SuppressFinalize(this);

            return ValueTask.CompletedTask;
        }
    }
}
