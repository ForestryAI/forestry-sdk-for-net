using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Forestry.Deserialize.Xml.Reading
{
    /// <summary>
    /// Multiple segment reading
    /// </summary>
    public ref partial struct Utf8XmlReader
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="isReadingCompleted"></param>
        /// <param name="readerState"></param>
        public partial Utf8XmlReader(
            ReadOnlySequence<byte> sequence,
            bool isReadingCompleted,
            ReaderState readerState
        )
        {
            // segment
            _segment = sequence.First.Span;
            _segmentPosition = 0;
            TokenPosition = 0;
            _isLastSegment = isReadingCompleted;
            _isReadingCompleted = isReadingCompleted;

            // sequence
            _sequence = sequence;
            if (sequence.IsSingleSegment)
            {
                _sequencePosition = 0;
                _isMultipleSegments = false;
                _currentSequencePosition = default;
                _nextSequencePosition = default;
            } else
            {
                _isMultipleSegments = true;
                _currentSequencePosition = sequence.Start;
                _nextSequencePosition = _currentSequencePosition;

                bool isEmptyFirstSegment = _segment.Length == 0;
                if (isEmptyFirstSegment)
                {
                    SequencePosition position = _nextSequencePosition;
                    while (sequence.TryGet(ref _nextSequencePosition, out ReadOnlyMemory<byte> memory, advance: true))
                    {                        
                        _currentSequencePosition = position;
                        if (memory.Length != 0)
                        {
                            _segment = memory.Span;
                            break;
                        }

                        position = _nextSequencePosition;
                    }
                }

                _isLastSegment = !sequence.TryGet(ref _nextSequencePosition, out _, advance: !isEmptyFirstSegment) && isReadingCompleted; 
            }

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
        /// oor the next non-empty segment then the segment is drained
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsMultipleSegmentDrained()
        {
            if (_segmentPosition >= (uint)_segment.Length)
            {
                if (IsLastReadableSegment)
                {
                    if (!AssertStateWhenLastReadableSegment())
                    {
                        return true;
                    }
                }

                if (!TrySkipEmptySegments())
                {
                    if (IsLastReadableSegment)
                    {
                        AssertStateWhenLastReadableSegment();
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Try skip any empty segments when the sequence can be advanced and 
        /// setting the current segment to the next non-empty
        /// </summary>
        /// <returns></returns>
        private bool TrySkipEmptySegments()
        {
            ReadOnlyMemory<byte> memory;

            while (true)
            {
                Debug.Assert(_currentSequencePosition.GetObject() is not null);

                SequencePosition position = _currentSequencePosition;
                _currentSequencePosition = _nextSequencePosition;

                if (!_sequence.TryGet(ref _nextSequencePosition, out memory, advance: true))
                {
                    _currentSequencePosition = position;
                    _isLastSegment = true;

                    return false;
                }

                if (memory.Length != 0)
                {
                    break;
                }

                _currentSequencePosition = position;  // advancement is empty revert position back
            }

            if (_isReadingCompleted)
            {
                _isLastSegment = !_sequence.TryGet(ref _nextSequencePosition, out _, advance: false);
            }

            _segment = memory.Span;
            _sequencePosition += _segmentPosition;
            _segmentPosition = 0;

            return true;
        }

        /// <summary>
        /// Skip any miscellaneous spacing
        /// </summary>
        private void SkipMultipleSpacing()
        {
            while (true)
            {
                SkipSingleSpacing();

                if (_segmentPosition < _segment.Length)
                {
                    break;
                }

                if (!TrySkipEmptySegments())
                {
                    break;
                }
            }
        }
    }
}