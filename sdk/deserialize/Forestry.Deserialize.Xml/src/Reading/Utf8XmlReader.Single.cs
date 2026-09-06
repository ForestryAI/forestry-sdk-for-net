using System.Buffers;

namespace Forestry.Deserialize.Xml.Reading
{
    /// <summary>
    /// Single segment reading
    /// </summary>
    public ref partial struct Utf8XmlReader
    {
        public partial Utf8XmlReader(
            ReadOnlySpan<byte> segment,
            bool isFinalSegment,
            ReaderState readerState
        ) {
            // segment
            _segment = segment;
            _segmentPosition = 0;

            _isExternalFinalSegment = isFinalSegment;
            _isFinalSegment = isFinalSegment;

            _documentPosition = 0;

            // state
            _lineNumber = readerState._lineNumber;
            _linePosition = readerState._linePosition;

            _documentNonTerminal = readerState._documentNonTerminal;

            _currentTokenType = readerState._currentTokenType;
            _previousTokenType = readerState._previousTokenType;
            _readerOptions = readerState.ReaderOptions;

            if (_readerOptions.MaxDepth == 0)
            {
                _readerOptions.MaxDepth = ReaderOptions.DefaultMaxDepth;
            }

            _elementNameStack = readerState._elementNameStack;

            // sequence (not used when byte span)
            HasValueSequence = false;
            Value = [];
            ValueSequence = ReadOnlySequence<byte>.Empty;

            _isSequence = false;
            _sequence = default;

            _currentSequencePosition = default;
            _nextSequencePosition = default;

            _isMultipleSegments = false;
        }

        /// <summary>
        /// Read opaque value from a single segment
        /// </summary>
        /// <param name="startingTerminal"></param>
        /// <param name="endingTerminal"></param>
        /// <param name="tokenType"></param>
        /// <returns></returns>
        internal bool ReadSingleSegmentOpaqueValue(
            ReadOnlySpan<byte> startingTerminal,
            ReadOnlySpan<byte> endingTerminal,
            TokenType tokenType
        ) {
            if (
                Utf8Reader.TryMatch(_segment[_segmentPosition..], startingTerminal, out int matchReadBytes) &&
                Utf8Reader.TrySkip(_segment[(_segmentPosition + matchReadBytes)..], endingTerminal, out int skipReadBytes, out int lineNumbersRead, out int linePosition)
            )
            {
                if (lineNumbersRead > 0)
                {
                    _lineNumber += lineNumbersRead;
                    _linePosition = linePosition;
                }
                else
                {
                    _linePosition += matchReadBytes + linePosition;
                }

                _previousTokenType = _currentTokenType;
                _currentTokenType = tokenType;

                Value = _segment.Slice(_segmentPosition, matchReadBytes + skipReadBytes);
                _segmentPosition = _segmentPosition + matchReadBytes + skipReadBytes;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Read a Name (<c>NameStartChar (NameChar)*</c>) from a single segment, starting
        /// exactly at <see cref="_segmentPosition"/> - stops at the first byte that isn't a
        /// valid NameChar (whitespace, '>', '/', or anything else - deliberately not just
        /// whitespace, since "&lt;Foo&gt;"/"&lt;Foo/&gt;" have no space before their delimiter
        /// and still need a name read).
        /// </summary>
        /// <returns></returns>
        internal bool ReadSingleSegmentName()
        {
            if (_segmentPosition >= _segment.Length || !EBNF.IsNameStartingCharacter(_segment[_segmentPosition]))
            {
                return false;
            }

            int nameLength = 1;
            while (_segmentPosition + nameLength < _segment.Length && EBNF.IsNameCharacter(_segment[_segmentPosition + nameLength]))
            {
                nameLength++;
            }

            Value = _segment.Slice(_segmentPosition, nameLength);
            _segmentPosition += nameLength;

            return true;
        }
    }
}