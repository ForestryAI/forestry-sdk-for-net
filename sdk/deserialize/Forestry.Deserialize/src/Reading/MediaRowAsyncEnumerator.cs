using System.Buffers;
using System.IO.Pipelines;
using System.Reflection;

namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// Asynchronous row enumerator over a <see cref="PipeReader"/>. The core writes the moves; a media
    /// derives from it and overrides <see cref="Read"/>, driving its own reader and the core's row builder
    /// (research R2). One row per move
    /// </summary>
    public abstract class MediaRowAsyncEnumerator : IAsyncEnumerator<Row>
    {
        /// <summary>
        /// Row enumerator over the bytes of a pipe
        /// </summary>
        /// <param name="pipe">UTF-8 bytes of the document</param>
        /// <param name="options"></param>
        /// <param name="cancellationToken">Cancels a move waiting for bytes</param>
        protected MediaRowAsyncEnumerator(PipeReader pipe, DeserializeOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pipe);
            ArgumentNullException.ThrowIfNull(options);

            _buffering = new PipeReaderBuffering(pipe, options.SkipsByteOrderMark);
            _cancellationToken = cancellationToken;
            Builder = new RowBuilder(options);
        }

        private PipeReaderBuffering _buffering;

        private readonly CancellationToken _cancellationToken;

        private bool _isDone;

        /// <summary>
        /// The core's row builder for this deserialization, for a media's shared reading class
        /// </summary>
        protected RowBuilder Builder { get; }

        /// <summary>
        /// Current row
        /// </summary>
        public Row Current { get; private set; } = null!;

        /// <summary>
        /// Read from the unconsumed bytes until the core completed a row, or nothing is left
        /// </summary>
        /// <param name="bytes">Unconsumed bytes</param>
        /// <param name="isFinalBlock">No more bytes follow</param>
        /// <param name="row">The row completed during the call</param>
        /// <param name="consumed">Bytes consumed, reported even when 0</param>
        /// <returns></returns>
        protected abstract ReadStatus Read(ReadOnlySequence<byte> bytes, bool isFinalBlock, out Row? row, out long consumed);

        /// <summary>
        /// A row of type <typeparamref name="T"/> starts
        /// </summary>
        /// <typeparam name="T"></typeparam>
        protected void Start<T>() where T : Row => Builder.Start<T>();

        /// <summary>
        /// A property of the top open row has this UTF-8 value
        /// </summary>
        /// <param name="property"></param>
        /// <param name="utf8"></param>
        /// <param name="context"></param>
        protected void Set(PropertyInfo property, ReadOnlySpan<byte> utf8, in FieldContext context) =>
            Builder.Set(property, utf8, in context);

        /// <summary>
        /// A field of the top open row has this UTF-8 value
        /// </summary>
        /// <param name="field"></param>
        /// <param name="utf8"></param>
        /// <param name="context"></param>
        protected void Set(FieldInfo field, ReadOnlySpan<byte> utf8, in FieldContext context) =>
            Builder.Set(field, utf8, in context);

        /// <summary>
        /// The top open row is complete
        /// </summary>
        /// <returns>The row to hand out, or null</returns>
        protected Row? Complete() => Builder.Complete();

        /// <summary>
        /// Move to the next row: refill, read, advance by the bytes consumed (even 0), refill again
        /// while the media needs more bytes
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
                Row? row;

                try
                {
                    state = Read(_buffering.Bytes, _buffering.IsCompleted, out row, out long consumed);
                    _buffering.Advance(consumed, isExamined: state == ReadStatus.Partial);
                }
                catch
                {
                    _buffering.Release();
                    throw;
                }

                switch (state)
                {
                    case ReadStatus.Complete when row is not null:
                        Current = row;
                        return true;
                    case ReadStatus.Complete:
                        continue;  // a row only a constructor needs: not handed out
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
        /// Release the state and the pipe's buffer; rows still open are never handed out
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
