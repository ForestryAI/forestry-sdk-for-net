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
            _documentType = readerState._documentType;
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

        /// <summary>
        /// Read opaque value from first character in the starting terminal 
        /// to the last character in the ending terminal
        /// </summary>
        /// <remarks>
        /// S2 - set previous and current token
        /// S3 - rollback
        /// S4 - throw malformed
        /// </remarks>
        /// <returns></returns>
        private bool ReadMultipleOpaqueValue(
            ReadOnlySpan<byte> startingTerminal, 
            ReadOnlySpan<byte> endingTerminal,
            TokenType tokenType
        ) {
            bool advancement = IgnoreMultipleOpaqueValue(startingTerminal, endingTerminal, true);

            if (advancement)
            {
                _previousTokenType = _currentTokenType;
                _currentTokenType = tokenType;
            }

            return advancement;
        }

        /// <summary>
        /// Ignore opaque value advances from first character in the starting terminal 
        /// to the last character in the ending terminal and defaults to not setting 
        /// the value
        /// </summary>
        /// <remarks>
        /// S3 - rollback
        /// S4 - throw malformed
        /// </remarks>
        /// <param name="startingTerminal"></param>
        /// <param name="endingTerminal"></param>
        /// <param name="setValue"></param>
        /// <returns></returns>
        private bool IgnoreMultipleOpaqueValue(
            ReadOnlySpan<byte> startingTerminal, 
            ReadOnlySpan<byte> endingTerminal,
            bool setValue = false
        ) {
            // Local sequence-reader variable starting at the current sequence position elimating the need to rollback
            ReadOnlySequence<byte> sequence = _sequence.Slice(SequencePosition);
            SequenceReader<byte> sequenceReader = new(sequence);

            bool hasOpaqueValue = sequenceReader.Remaining >= startingTerminal.Length;
            if (hasOpaqueValue)
            {
                sequenceReader.Advance(startingTerminal.Length);
                hasOpaqueValue = sequenceReader.TryReadTo(out ReadOnlySequence<byte> _, endingTerminal, advancePastDelimiter: true);
            }

            if (!hasOpaqueValue)
            {
                if (_isReadingCompleted)
                {
                    Throwing.ThrowXmlException(ref this, Throwing.ExceptionType.WhenEndingTerminalMissing, bytes: endingTerminal);  // S4
                }

                return false;  // S3: Break fast without advancement (i.e. simulating rollback)
            }

            // S2: set position(s), line number + position, and value
            ReadOnlySequence<byte> value = sequence.Slice(0, sequenceReader.Consumed);

            foreach (ReadOnlyMemory<byte> memory in value)
            {
                AdvanceLineTracking(memory.Span);
            }

            AdvanceMultipleSegments(value.Length);

            if (setValue)
            {
                if (value.IsSingleSegment)
                {
                    Value = value.FirstSpan;
                    ValueSequence = ReadOnlySequence<byte>.Empty;
                    HasValueSequence = false;
                } else
                {
                    Value = [];
                    ValueSequence = value;
                    HasValueSequence = true;
                }
            }

            return true;
        }

        /// <summary>
        /// Advance the segment position by a count of bytes, moving into following
        /// non-empty segments the same way a drainage assertion does
        /// </summary>
        /// <param name="count"></param>
        private void AdvanceMultipleSegments(long count)
        {
            while (true)
            {
                int available = _segment.Length - _segmentPosition;
                if (count <= available)
                {
                    _segmentPosition += (int)count;
                    return;
                }

                count -= available;
                _segmentPosition = _segment.Length;

                bool skipped = TrySkipEmptySegments();
                Debug.Assert(skipped, "Advancing past the end of the byte sequence - the count was found by searching it.");
                if (!skipped)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// PI target conditions (S5 + S6) on a value byte sequence, scanning segment by
        /// segment since the target may straddle segments
        /// </summary>
        /// <returns></returns>
        private bool IsMultipleProcessingInstructionMalformed()
        {
            ProcessingInstructionTarget target = default;

            foreach (ReadOnlyMemory<byte> memory in ValueSequence.Slice(EBNF.ProcessingInstructionStartingTerminal.Length))
            {
                MarkupState evaluation = target.Evaluate(memory.Span);
                if (evaluation != MarkupState.Unknown)
                {
                    return evaluation != MarkupState.WellFormed;
                }
            }

            Debug.Assert(false, "The value ends with the ending terminal ?> so the scan always ends.");
            return true;
        }

        /// <summary>
        /// Evaluates a name non-terminal markup state across multiple segments, continuing to
        /// the next segment while the markup state is unknown
        /// </summary>
        /// <remarks>
        /// The first character is the first byte of the name, not the first segment: no name
        /// characters counted yet, since the sequence can start with empty segments
        /// </remarks>
        /// <param name="nameLength"></param>
        /// <param name="unsupportedCharacter"></param>
        /// <returns></returns>
        private MarkupState EvaluateMultipleNameNonTerminalMarkup(
            out int nameLength,
            out bool unsupportedCharacter
        )
        {
            nameLength = 0;
            unsupportedCharacter = false;

            foreach (ReadOnlyMemory<byte> memory in _sequence.Slice(SequencePosition))
            {
                MarkupState markupState = EvaluateNameNonTerminalMarkup(memory.Span, isFirstCharacter: nameLength == 0, out int segmentNameLength, out unsupportedCharacter);
                nameLength += segmentNameLength;

                if (markupState != MarkupState.Unknown)
                {
                    return markupState;
                }
            }

            return MarkupState.Unknown;
        }
    }
}