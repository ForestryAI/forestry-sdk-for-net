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
                    SequencePosition nextSequencePosition = _nextSequencePosition;
                    while (sequence.TryGet(ref _nextSequencePosition, out ReadOnlyMemory<byte> memory, advance: true))
                    {                        
                        _currentSequencePosition = nextSequencePosition;
                        if (memory.Length != 0)
                        {
                            _segment = memory.Span;
                            break;
                        }

                        nextSequencePosition = _nextSequencePosition;
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
    }
}