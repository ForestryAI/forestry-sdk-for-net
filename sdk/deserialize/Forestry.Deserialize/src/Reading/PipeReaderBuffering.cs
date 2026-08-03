using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;

namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// Only async reading using <see cref="PipeReader"/>
    /// </summary>
    internal struct PipeReaderBuffering(PipeReader stream) : IBuffering<PipeReaderBuffering, PipeReader>
    {
        private readonly PipeReader _stream = stream;

        private ReadOnlySequence<byte> _sequence = ReadOnlySequence<byte>.Empty;

        private bool _isCompleted;

        private bool _isStarting = true;

        private int _partialReadBytes;

        /// <summary>
        /// Is pipe reader coompleted
        /// </summary>
        public readonly bool IsCompleted => _isCompleted;

        /// <summary>
        /// Buffered bytes
        /// </summary>
        public readonly ReadOnlySequence<byte> Bytes => _sequence;

        /// <summary>
        /// Asynchronously read the next buffered bytes from the stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="maximum">Maximum bytes with internal buffers</param>
        /// <returns></returns>
        public async readonly ValueTask<PipeReaderBuffering> ReadAsync(
            PipeReader stream,
            CancellationToken cancellationToken,
            bool maximum = true
        )
        {
            Debug.Assert(_sequence.Equals(ReadOnlySequence<byte>.Empty), "Call ReadAsync only when buffer is empty");
            PipeReaderBuffering buffering =  this; // async copy with structs

            int minimumReadSize = _partialReadBytes > 0 ? _partialReadBytes : 0;
            ReadResult result = await _stream.ReadAtLeastAsync(minimumReadSize, cancellationToken).ConfigureAwait(false);

            buffering._sequence = result.Buffer;
            buffering._isCompleted = result.IsCompleted;
            buffering.ReadStarting();

            if (result.IsCanceled)
            {
                Throwing.WhenPipeReaderCanceled();
            }

            return buffering;
        }

        /// <summary>
        /// Synchronously read the next buffered bytes from the stream
        /// </summary>
        /// <param name="stream"></param>
        public void Read(PipeReader stream) => throw new NotImplementedException();

        /// <summary>
        /// Advance buffering by the number of bytes used
        /// </summary>
        /// <param name="bytesUsed"></param>
        public void Advance(long bytesUsed)
        {
            _partialReadBytes = 0;
            if (bytesUsed == 0)
            {
                long remaining = _sequence.Length;
                _partialReadBytes = (int)Math.Min(int.MaxValue, remaining * 2);
            }

            _stream.AdvanceTo(_sequence.Slice(bytesUsed).Start, _sequence.End);
            _sequence = ReadOnlySequence<byte>.Empty;
        }

        public void Dispose()
        {
            if (_sequence.Equals(ReadOnlySequence<byte>.Empty))
            {
                return;
            }

            _stream.AdvanceTo(_sequence.Start);
            _sequence = ReadOnlySequence<byte>.Empty;
        }

        /// <summary>
        /// Skips the UTF-8 BOM when present
        /// </summary>
        private void ReadStarting()
        {
            if (_isStarting)
            {
                _isStarting = false;

                if (_sequence.Length > 0)
                {
                    if (_sequence.First.Length >= Constants.Utf8Bom.Length)
                    {
                        if (_sequence.First.Span.StartsWith(Constants.Utf8Bom))
                        {
                            _sequence = _sequence.Slice((byte)Constants.Utf8Bom.Length);
                        }
                    }
                    else
                    {
                        // BOM spans multiple segments
                        SequencePosition pos = _sequence.Start;
                        int matched = 0;
                        while (matched < Constants.Utf8Bom.Length && _sequence.TryGet(ref pos, out ReadOnlyMemory<byte> mem, advance: true))
                        {
                            ReadOnlySpan<byte> span = mem.Span;
                            for (int i = 0; i < span.Length && matched < Constants.Utf8Bom.Length; i++, matched++)
                            {
                                if (span[i] != Constants.Utf8Bom[matched])
                                {
                                    matched = 0;
                                    break;
                                }
                            }
                        }

                        if (matched == Constants.Utf8Bom.Length)
                        {
                            _sequence = _sequence.Slice(Constants.Utf8Bom.Length);
                        }
                    }
                }
            }
        }
    }
}