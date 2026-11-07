using System.Buffers;
using System.IO.Pipelines;
using System.Reflection;

namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// Asynchronous row enumerator over a <see cref="PipeReader"/>. The core writes the moves; a media
    /// derives from it and overrides <see cref="Read"/>, driving its own reader and the core's pipelines
    /// (research R2). One row per move
    /// </summary>
    public abstract class RowAsyncEnumerator : IAsyncEnumerator<Row>
    {
        /// <summary>
        /// Row enumerator over the bytes of a pipe
        /// </summary>
        /// <param name="pipe">UTF-8 bytes of the document</param>
        /// <param name="options"></param>
        /// <param name="cancellationToken">Cancels a move waiting for bytes</param>
        protected RowAsyncEnumerator(PipeReader pipe, DeserializeOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pipe);
            ArgumentNullException.ThrowIfNull(options);

            _buffering = new PipeReaderBuffering(pipe, options.SkipsByteOrderMark);
            _cancellationToken = cancellationToken;
            Pipelines = new RowPipelines(options);
        }

        private PipeReaderBuffering _buffering;

        private readonly CancellationToken _cancellationToken;

        private bool _isDone;

        /// <summary>
        /// The core's pipelines for this deserialization, for a media's shared reading class
        /// </summary>
        protected RowPipelines Pipelines { get; }

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
        protected abstract RowState Read(ReadOnlySequence<byte> bytes, bool isFinalBlock, out Row? row, out long consumed);

        /// <summary>
        /// A row of type <typeparamref name="T"/> starts
        /// </summary>
        /// <typeparam name="T"></typeparam>
        protected void Pipeline<T>() where T : Row => Pipelines.Pipeline<T>();

        /// <summary>
        /// A property of the top open row has this UTF-8 value
        /// </summary>
        /// <param name="property"></param>
        /// <param name="utf8"></param>
        /// <param name="context"></param>
        protected void Pipeline(PropertyInfo property, ReadOnlySpan<byte> utf8, in FieldContext context) =>
            Pipelines.Pipeline(property, utf8, in context);

        /// <summary>
        /// A field of the top open row has this UTF-8 value
        /// </summary>
        /// <param name="field"></param>
        /// <param name="utf8"></param>
        /// <param name="context"></param>
        protected void Pipeline(FieldInfo field, ReadOnlySpan<byte> utf8, in FieldContext context) =>
            Pipelines.Pipeline(field, utf8, in context);

        /// <summary>
        /// The top open row is complete
        /// </summary>
        /// <returns>The row to hand out, or null</returns>
        protected Row? Pipeline() => Pipelines.Pipeline();

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

                RowState state;
                Row? row;

                try
                {
                    state = Read(_buffering.Bytes, _buffering.IsCompleted, out row, out long consumed);
                    _buffering.Advance(consumed, isExamined: state == RowState.Partial);
                }
                catch
                {
                    _buffering.Release();
                    throw;
                }

                switch (state)
                {
                    case RowState.Complete when row is not null:
                        Current = row;
                        return true;
                    case RowState.Complete:
                        continue;  // a row only a constructor needs: not handed out
                    case RowState.None:
                        _isDone = true;
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
            Pipelines.Clear();
            GC.SuppressFinalize(this);

            return ValueTask.CompletedTask;
        }
    }
}
