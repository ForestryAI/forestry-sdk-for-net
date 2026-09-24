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
        /// Reader construction from a byte sequence
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
            _isByteSequence = true;
            if (sequence.IsSingleSegment)
            {
                _advancementPosition = 0;
                _isMultipleSegments = false;
                _currentSequencePosition = sequence.Start;
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
        /// Sequence position offset by the current segment position only when the 
        /// reader is constructed from a byte sequence otherwise the value is always 
        /// the default
        /// </summary>
        public SequencePosition SequencePosition
        {
            get
            {
                if (_isByteSequence)
                {
                    Debug.Assert(_currentSequencePosition.GetObject() is not null);
                    return _sequence.GetPosition(_segmentPosition, _currentSequencePosition);
                }

                return default;
            }
        }

        /// <summary>
        /// When the segment position exceeds the length of the current 
        /// or the next non-empty segment then the segment is drained 
        /// halting advancement 
        /// </summary>
        /// <remarks>
        /// A drainage assertion may advance the internal current sequence position 
        /// and segment position at the start of the next non-empty segment
        /// </remarks>
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
        /// Try skip any empty segments when the sequence allows advancement 
        /// to the start of the next non-empty sequence
        /// </summary>
        /// <remarks>
        /// A drainage assertion may advance the internal current sequence position 
        /// and segment position at the start of the next non-empty segment
        /// </remarks>
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
            _advancementPosition += _segmentPosition;
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