using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;

namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// Buffered bytes of a <see cref="PipeReader"/> for the async row enumerator (research R2): refill,
    /// then advance by the bytes consumed, even 0
    /// </summary>
    /// <remarks>
    /// A struct copied across awaits, as System.Text.Json's buffering is: <see cref="ReadAsync"/> returns
    /// the refilled copy.
    /// </remarks>
    /// <param name="pipe"></param>
    /// <param name="skipsByteOrderMark">Skip a leading UTF-8 byte order mark (research R18)</param>
    internal struct PipeReaderBuffering(PipeReader pipe, bool skipsByteOrderMark)
    {
        private readonly PipeReader _pipe = pipe;

        private ReadOnlySequence<byte> _sequence = ReadOnlySequence<byte>.Empty;

        private bool _isCompleted;

        private bool _isStarting = skipsByteOrderMark;

        private bool _isHolding;

        /// <summary>
        /// No more bytes follow the buffered ones
        /// </summary>
        public readonly bool IsCompleted => _isCompleted;

        /// <summary>
        /// The buffered bytes may still start a byte order mark to skip: advance by 0 and refill before
        /// reading
        /// </summary>
        public readonly bool IsStarting => _isStarting;

        /// <summary>
        /// Buffered bytes not yet consumed
        /// </summary>
        public readonly ReadOnlySequence<byte> Bytes => _sequence;

        /// <summary>
        /// Refill: the unconsumed bytes, plus new ones when the last advance examined everything
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>The refilled copy</returns>
        public readonly async ValueTask<PipeReaderBuffering> ReadAsync(CancellationToken cancellationToken)
        {
            Debug.Assert(!_isHolding, "Advance before reading again");
            PipeReaderBuffering buffering = this;  // async copy of a struct

            ReadResult result = await _pipe.ReadAsync(cancellationToken).ConfigureAwait(false);

            if (result.IsCanceled)
            {
                Throwing.WhenPipeReaderCanceled();
            }

            buffering._sequence = result.Buffer;
            buffering._isCompleted = result.IsCompleted;
            buffering._isHolding = true;
            buffering.ReadStarting();

            return buffering;
        }

        /// <summary>
        /// Advance by the bytes consumed, even 0
        /// </summary>
        /// <param name="consumed">Bytes consumed from <see cref="Bytes"/></param>
        /// <param name="isExamined">
        /// Every byte was examined: the next refill waits for new bytes (after <c>Partial</c>). Otherwise
        /// the unconsumed bytes come back at once (after a row)
        /// </param>
        public void Advance(long consumed, bool isExamined)
        {
            Debug.Assert(_isHolding, "Read before advancing");
            SequencePosition position = _sequence.GetPosition(consumed);

            if (isExamined)
            {
                _pipe.AdvanceTo(position, _sequence.End);
            }
            else
            {
                _pipe.AdvanceTo(position);
            }

            _sequence = ReadOnlySequence<byte>.Empty;
            _isHolding = false;
        }

        /// <summary>
        /// Release the pipe's buffer, consuming nothing more
        /// </summary>
        public void Release()
        {
            if (_isHolding)
            {
                _pipe.AdvanceTo(_sequence.Start);
                _sequence = ReadOnlySequence<byte>.Empty;
                _isHolding = false;
            }
        }

        /// <summary>
        /// Skips the UTF-8 byte order mark at the very start, as the memory entry point does, when the
        /// options ask for it
        /// </summary>
        private void ReadStarting()
        {
            if (!_isStarting)
            {
                return;
            }

            ReadOnlySpan<byte> bom = Constants.Utf8Bom;
            Span<byte> start = stackalloc byte[3];
            int length = (int)Math.Min(_sequence.Length, bom.Length);
            _sequence.Slice(0, length).CopyTo(start);

            if (length < bom.Length && !_isCompleted && bom.StartsWith(start[..length]))
            {
                return;  // a byte order mark not complete yet: IsStarting, decided at the next refill
            }

            _isStarting = false;

            if (start[..length].SequenceEqual(bom))
            {
                _sequence = _sequence.Slice(bom.Length);
            }
        }
    }
}
