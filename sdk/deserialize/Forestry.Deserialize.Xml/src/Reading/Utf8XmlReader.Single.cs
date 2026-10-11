using System.Buffers;
using System.Diagnostics;
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
            _documentType = readerState._documentType;
            _hasSkippedSpacingInStartTag = readerState._hasSkippedSpacingInStartTag;
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
                AdvanceLineTracking(remaining[..indexOfExceptWhiteSpace]);
                _segmentPosition += indexOfExceptWhiteSpace;
            }

            return;            
        }

        /// <summary>
        /// Read opaque value from first character in the starting terminal 
        /// to the last character in the ending terminal.
        /// </summary>
        /// <remarks>
        /// S2 - set previous and current token
        /// S3 - rollback
        /// S4 - throw malformed
        /// </remarks>
        /// <returns></returns>
        private bool ReadSingleOpaqueValue(
            ReadOnlySpan<byte> startingTerminal, 
            ReadOnlySpan<byte> endingTerminal,
            TokenType tokenType
        ) {
            bool advancement = IgnoreSingleOpaqueValue(startingTerminal, endingTerminal, true, tokenType == TokenType.ProcessInstruction);

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
        /// <param name="isProcessingInstruction">check the target (S5 + S6) before committing</param>
        /// <returns></returns>
        private bool IgnoreSingleOpaqueValue(
            ReadOnlySpan<byte> startingTerminal, 
            ReadOnlySpan<byte> endingTerminal,
            bool setValue = false,
            bool isProcessingInstruction = false
        ) {
            // Local sliced segment variable starting from the current position elimating the need to rollback
            ReadOnlySpan<byte> segment = _segment[_segmentPosition..];
            int endingTerminalIndex = segment.Length < startingTerminal.Length
                ? -1
                : segment[startingTerminal.Length..].IndexOf(endingTerminal);

            if (endingTerminalIndex < 0)
            {
                if (_isReadingCompleted)
                {
                    Throwing.ThrowXmlException(ref this, Throwing.Category.Incomplete, Throwing.Markup.EndingTerminal, bytes: endingTerminal);  // S4
                }

                return false;  // S3: Break fast without advancement (i.e. simulating rollback)
            }

            // S2: set position, line number + position, and value
            ReadOnlySpan<byte> value = segment[..(startingTerminal.Length + endingTerminalIndex + endingTerminal.Length)];

            if (isProcessingInstruction)
            {
                ProcessingInstructionTarget target = default;
                MarkupState evaluation = target.Evaluate(value[startingTerminal.Length..]);

                Debug.Assert(evaluation != MarkupState.Unknown, "The value ends with the ending terminal ?> so the scan always ends.");
                if (evaluation != MarkupState.WellFormed)
                {
                    Throwing.ThrowXmlException(ref this, Throwing.Category.Name, Throwing.Markup.ProcessingInstructionTarget, offset: startingTerminal.Length + target.OffendingIndex);  // S5 + S6
                }
            }

            AdvanceLineTracking(value);
            _segmentPosition += value.Length;

            if (setValue)
            {
                Value = value;
                ValueSequence = ReadOnlySequence<byte>.Empty;
                HasValueSequence = false;
            }

            return true;
        }

        /// <summary>
        /// Advance the line number and line position over characters the reader advances
        /// past: every line feed increments the line number and restarts the line position
        /// </summary>
        /// <param name="characters"></param>
        private void AdvanceLineTracking(ReadOnlySpan<byte> characters)
        {
            (_lineNumber, _linePosition) = Utf8Reader.Track(_lineNumber, _linePosition, characters);
        }
    }
}