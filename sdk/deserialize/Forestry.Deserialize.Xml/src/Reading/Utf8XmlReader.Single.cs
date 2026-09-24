using System.Buffers;
using System.Runtime.CompilerServices;

namespace Forestry.Deserialize.Xml.Reading
{
    /// <summary>
    /// Single segment reading
    /// </summary>
    public ref partial struct Utf8XmlReader
    {
        /// <summary>
        /// Reader construction from a byte span
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="isReadingCompleted"></param>
        /// <param name="readerState"></param>
        public partial Utf8XmlReader(
            ReadOnlySpan<byte> segment,
            bool isReadingCompleted,
            ReaderState readerState
        )
        {
            // segment
            _segment = segment;
            _segmentPosition = 0;
            TokenPosition = 0;
            _isLastSegment = isReadingCompleted;
            _isReadingCompleted = isReadingCompleted;

            // sequence
            _sequence = default;
            _isByteSequence = false;
            _advancementPosition = 0;
            _isMultipleSegments = false;
            _currentSequencePosition = default;
            _nextSequencePosition = default;

            // state
            _linePosition = readerState._linePosition;
            _lineNumber = readerState._lineNumber;
            _currentTokenType = readerState._currentTokenType;
            _previousTokenType = readerState._previousTokenType;
            _elementStack = readerState._elementStack;
            _readerOptions = readerState._readerOptions;

            if (_readerOptions.MaxDepth <= 0)
            {
                _readerOptions.MaxDepth = ReaderOptions.DefaultMaxDepth;
            }

            // value
            Value = ReadOnlySpan<byte>.Empty;
            ValueSequence = ReadOnlySequence<byte>.Empty;
            HasValueSequence = false;
        }

        /// <summary>
        /// When the segment position exceeds the length of the current 
        /// segment then the segment is drained halting advancement
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsSingleSegmentDrained()
        {
            if (_segmentPosition >= (uint)_segment.Length)
            {
                if (IsLastReadableSegment)
                {
                    AssertStateWhenLastReadableSegment();
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Skip any miscellaneous spacing
        /// </summary>
        private void SkipSingleSpacing()
        {
            ReadOnlySpan<byte> segment = _segment;
            ReadOnlySpan<byte> remaining = segment.Slice(_segmentPosition);

            int indexOfExceptWhiteSpace = remaining.IndexOfExceptWhiteSpace();
            if (indexOfExceptWhiteSpace > 0)
            {
                (int lineFeedCount, int lastLineFeedIndex) = Utf8Reader.LineFeedCount(remaining[..indexOfExceptWhiteSpace]);

                _lineNumber += lineFeedCount;
                if (lastLineFeedIndex >= 0)
                {
                    _linePosition = indexOfExceptWhiteSpace - lastLineFeedIndex - 1;
                }
                else
                {
                    _linePosition += indexOfExceptWhiteSpace;
                }

                _segmentPosition += indexOfExceptWhiteSpace;
            }

            return;            
        }
    }
}