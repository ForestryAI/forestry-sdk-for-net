using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Forestry.Deserialize.Xml.Reading
{
    internal ref partial struct Utf8XmlReader
    {
        #region segment
        /// <summary>
        /// Internal segment deriving from either a byte span or byte sequence
        /// </summary>
        private ReadOnlySpan<byte> _segment;

        /// <summary>
        /// Internal segment position
        /// </summary>
        private int _segmentPosition;

        /// <summary>
        /// Final segment from an external flag
        /// </summary>
        private bool _isExternalFinalSegment;

        /// <summary>
        /// Only true when the internal segment derives from a byte sequence that has multiple segments
        /// </summary>
        private bool _isMultipleSegments;

        /// <summary>
        /// Final segment internal flag
        /// </summary>
        private bool _isFinalSegment;

                /// <summary>
        /// Reading is completed when the external final segment is flagged and there are 
        /// no multiple segments or the internal final segment is flagged
        /// </summary>
        private readonly bool IsReadingCompleted => _isExternalFinalSegment && (!_isMultipleSegments || _isFinalSegment);
        #endregion

        #region sequence
        /// <summary>
        /// When the segment derives from a byte sequence
        /// </summary>
        private readonly bool _isSequence;

        /// <summary>
        /// Sequence backing the internal buffer
        /// </summary>
        private readonly ReadOnlySequence<byte> _sequence;

        /// <summary>
        /// Current sequence position
        /// </summary>
        private SequencePosition _currentSequencePosition;

        /// <summary>
        /// Next sequence position
        /// </summary>
        private SequencePosition _nextSequencePosition;

        public readonly SequencePosition SequencePosition
        {
            get
            {
                if (_isSequence)
                {
                    Debug.Assert(_currentSequencePosition.GetObject() is not null);
                    return _sequence.GetPosition(_segmentPosition, _currentSequencePosition);
                }
                return default;
            }
        }

        /// <summary>
        /// External access to the internal sequence
        /// </summary>
        internal readonly ReadOnlySequence<byte> Sequence => _sequence;
        #endregion

        #region state
        /// <summary>
        /// Line number i.e. top to bottom
        /// </summary>
        private long _lineNumber;

        /// <summary>
        /// Line position i.e. left to right
        /// </summary>
        private long _linePosition;

        /// <summary>
        /// Document non-terminal
        /// </summary>
        private EBNF.Document _documentNonTerminal;

        /// <summary>
        /// Current XML Token
        /// </summary>
        private TokenType _currentTokenType;

        /// <summary>
        /// Previous XML Token
        /// </summary>
        private TokenType _previousTokenType;

        /// <summary>
        /// Element name when the element contains a value
        /// </summary>
        private ulong[] _elementName;

        /// <summary>
        /// Element names when the element contains a child element
        /// </summary>
        private ElementNameStack _elementNameStack;

        /// <summary>
        /// Reader options
        /// </summary>
        private ReaderOptions _readerOptions;
        #endregion

        #region document
        /// <summary>
        /// Position in the document
        /// </summary>
        private int _documentPosition; 
        #endregion

        /// <summary>
        /// Reading segment from a byte span
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="readerOptions"></param>
        public Utf8XmlReader(
            ReadOnlySpan<byte> segment,
            ReaderOptions readerOptions = default
        ): this(segment, isFinalSegment: true, new ReaderState(readerOptions))
        {
            
        }

        /// <summary>
        /// Reading segment from a byte span using a reader state (where options follow along)
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="isFinalSegment"></param>
        /// <param name="readerState"></param>
        public Utf8XmlReader(
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

            _elementName = readerState._elementName;
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
        /// Reading segments from a byte sequence starting with the first segment
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="readerOptions"></param>
        public Utf8XmlReader(
            ReadOnlySequence<byte> segments,
            ReaderOptions readerOptions = default
        ): this(segments, isFinalSegment: true, new ReaderState(readerOptions))
        {
            
        }

        /// <summary>
        /// Reading segments from a byte sequence starting with the first segment using a 
        /// reader state (where options follow along)
        /// 
        /// The <paramref name="isFinalSegment"/> is always respected when the sequence 
        /// only has a single segment.
        /// 
        /// The <paramref name="isFinalSegment"/> is only respected when using the last 
        /// segment in the sequence otherwise ignored.  All starting segments that are 
        /// empty are ignored when multiple segments.
        /// </summary>
        /// <param name="segments"></param>
        public Utf8XmlReader(
            ReadOnlySequence<byte> segments,
            bool isFinalSegment,
            ReaderState readerState
        ): this(segments.FirstSpan, isFinalSegment, readerState)
        {
            _isSequence = true;
            _sequence = segments;

            _currentSequencePosition = segments.Start;

            // remaining fields depend on if the sequence has a single segment or multiple ignoring empty segments
            if (segments.IsSingleSegment)
            {
                _isMultipleSegments = false;
                _nextSequencePosition = default;
            } else
            {
                _nextSequencePosition = _currentSequencePosition;

                bool emptyFirstSegment = _segment.Length == 0;
                if (emptyFirstSegment)
                {
                    SequencePosition referenceSequencePosition = _nextSequencePosition;
                    while (segments.TryGet(ref _nextSequencePosition, out ReadOnlyMemory<byte> memory, advance: true))
                    {
                        _currentSequencePosition = referenceSequencePosition;
                        if (memory.Length != 0)
                        {
                            _segment = memory.Span;
                            break;
                        }
                        referenceSequencePosition = _nextSequencePosition;
                    }
                }

                _isFinalSegment = !segments.TryGet(ref _nextSequencePosition, out _, advance: !emptyFirstSegment) && isFinalSegment; 

                Debug.Assert(!_nextSequencePosition.Equals(_currentSequencePosition));
                _isMultipleSegments = true;
            }
        }

        #region token
        /// <summary>
        /// Current token type
        /// </summary>
        public readonly TokenType TokenType => _currentTokenType;

        /// <summary>
        /// Current token index excluding the value
        /// </summary>
        public long TokenIndex { get; private set; }

        /// <summary>
        /// Value could not fit inside a single byte span <see cref="Value"/> instead inside a 
        /// byte sequence <see cref="ValueSequence"/>
        /// </summary>
        public bool HasValueSequence { get; private set; }

        /// <summary>
        /// Value with sequencing
        /// </summary>
        public ReadOnlySequence<byte> ValueSequence { get; private set; }

        /// <summary>
        /// Value without sequencing
        /// </summary>
        public ReadOnlySpan<byte> Value { get; private set; }

        /// <summary>
        /// Reader state
        /// </summary>
        public readonly ReaderState ReaderState => new(
            lineNumber: _lineNumber,
            linePosition: _linePosition,
            documentNonTerminal: _documentNonTerminal,
            currentTokenType: _currentTokenType,
            previousTokenType: _previousTokenType,
            elementName: _elementName,
            elementNameStack: _elementNameStack,
            readerOptions: _readerOptions
        );
        #endregion


        #region segment
        /// <summary>
        /// A readable segment is only possible whe the segment position is less than 
        /// the segment length and the segment is not closed.
        /// 
        /// Multiple segment read until the next non-empty segment before throwing 
        /// if the segment is not closed.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsSegmentReadable()
        {
            if (_segmentPosition >= (uint)_segment.Length)
            {
                if (_isMultipleSegments && ReadNextSegment())
                {
                    return true;
                }

                ThrowableSegmentClosed();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Throws when the segment is not closed and is the final segment:
        /// 
        /// - element block is not closed
        /// - document non-terminal is None or Prolog i.e. no markup has been read
        /// </summary>
        /// <returns></returns>
        private readonly void ThrowableSegmentClosed()
        {
            if (_isFinalSegment)
            {
                // TODO: element name stack length != 0 || element name is not empty

                if (_documentNonTerminal == EBNF.Document.None || _documentNonTerminal == EBNF.Document.Prolog)
                {
                    throw new InvalidOperationException();  // TODO: formatting
                }
            }
        }

        /// <summary>
        /// Reads only non-empty segments into the internal segment and reseting 
        /// the segment position
        /// </summary>
        /// <returns></returns>
        private bool ReadNextSegment()
        {
            ReadOnlyMemory<byte> memory;

            while (true)
            {
                Debug.Assert(!_isMultipleSegments || _currentSequencePosition.GetObject() is not null);

                SequencePosition referenceSequencePosition = _currentSequencePosition;
                _currentSequencePosition = _nextSequencePosition;

                if (!_sequence.TryGet(ref _nextSequencePosition, out memory, advance: true))
                {
                    _currentSequencePosition = referenceSequencePosition;
                    _isFinalSegment = true;

                    return false;
                }

                if (memory.Length != 0)
                {
                    break;
                }

                _currentSequencePosition = referenceSequencePosition;
                Debug.Assert(!_isMultipleSegments || _currentSequencePosition.GetObject() is not null);
            }

            if (_isExternalFinalSegment)
            {
                _isFinalSegment = !_sequence.TryGet(ref _nextSequencePosition, out _, advance: false);
            }

            _segment = memory.Span;

            _documentPosition += _segmentPosition;
            _segmentPosition = 0;

            return true;
        }

        /// <summary>
        /// Rollback is only applicable to element entities that effect 
        /// internal state whereas the prolog and miscellaneous have entities
        /// with opaque values only peeking to match the starting terminal 
        /// and skip until the ending terminal.
        /// </summary>
        /// <returns></returns>
        private bool Rollback()
        {
            // TODO: Local copy of state

            

            return false;
        }
        #endregion

        #region read 
        /// <summary>
        /// Read token returning false when unable and throwing 
        /// on any invalid operations
        /// </summary>
        /// <returns></returns>
        public bool Read()
        {
            bool readable = false;
            Value = default;

            if (!IsSegmentReadable())
            {
                goto ReadingCompleted;
            }

            readable = ReadDocument();
            goto ReadingCompleted;

            ReadingCompleted:
                if (!readable)
                {
                    if (_isExternalFinalSegment && _currentTokenType is TokenType.None)
                    {
                        throw new InvalidOperationException(); // TODO: Formatting
                    }
                }

                return readable;
        }

        /// <summary>
        /// Read document non-terminals in order:
        ///   document ::= prolog element miscellaneous
        /// </summary>
        /// <returns></returns>
        internal bool ReadDocument()
        {
            TokenIndex = _segmentPosition;
            EBNF.Document previousNonTerminal;
            int previousSegmentPosition;

            bool readable;
            do
            {
                previousNonTerminal = _documentNonTerminal;
                previousSegmentPosition = _segmentPosition;

                readable = _documentNonTerminal switch
                {
                    EBNF.Document.None or EBNF.Document.Prolog => ReadProlog(),
                    EBNF.Document.Element => ReadMarkup(),
                    EBNF.Document.Miscellaneous => ReadMiscellaneous(),
                    _ => false
                };
                // Spacing advances _segmentPosition without producing a token or changing
                // _documentNonTerminal, so the phase-only check below isn't enough on its own -
                // without also watching position, leading whitespace before real content would
                // make this return false for one whole Read() call even though the very next
                // bytes are perfectly readable, and on the final segment that's a spurious throw.
            }
            while (!readable && (_documentNonTerminal != previousNonTerminal || _segmentPosition != previousSegmentPosition));

            return readable;
        }

        /// <summary>
        /// Read prolog non-terminals in order:
        ///   prolog ::= declaration? miscellaneous* (document-type miscellaneous*)?
        /// </summary>
        /// <remarks>
        /// no assert against multiple document-type non-terminals.
        ///
        /// The declaration's starting terminal is matched as "&lt;?xml " (6 bytes, including the
        /// trailing space) rather than the bare 5-byte "&lt;?xml" - a real, legal processing
        /// instruction target only has to *start with* "xml" (e.g. "xml-stylesheet"; only the
        /// exact target "xml", case-insensitive, is reserved), so a bare prefix match would
        /// misread such a PI as a declaration. This is an approximation, not a full implementation
        /// of the EBNF's actual boundary: XML's <c>S</c> production also allows tab/CR/LF, not
        /// just a literal space, so "&lt;?xml" followed by a tab would wrongly fail to match even
        /// though it is technically legal XML. Accepted for the POC since real StanForD data only
        /// ever uses a plain space there.
        /// </remarks>
        /// <returns></returns>
        internal bool ReadProlog()
        {
            bool readable = false;

            if (_documentNonTerminal == EBNF.Document.None)
            {
                _documentNonTerminal = EBNF.Document.Prolog;
                readable = ReadOpaqueValue("<?xml "u8, "?>"u8,  TokenType.Declaration);

                if (readable) {
                    goto ReadCompleted;
                }
            }

            if (IsElementStartingTag())
            {
                _documentNonTerminal = EBNF.Document.Element;
                goto ReadCompleted;
            }
            
            // 
            readable = ReadMiscellaneous() || (_currentTokenType != TokenType.DocumentType && ReadOpaqueValue("<!DOCTYPE"u8, ">"u8, TokenType.DocumentType));

            ReadCompleted:
                return readable;
        }

        /// <summary>
        /// Read markup acts on non-terimal elements, end elements,
        /// attributes and values both associated to attributes and
        /// simple content in elements.
        /// </summary>
        /// <returns></returns>
        internal bool ReadMarkup()
        {
            bool readable = false;
            do
            {
                if (Utf8Reader.TryMatch(_segment.Slice(_segmentPosition, 1), EBNF.StartingElementTerminal, out int _))
                {
                    // TODO: Read name using new method expect position at '<' and ending in any space avoiding a forever loop
                    // TODO: When readable break fast else ? throw
                }

                // TODO: exhaust spacing || '>' (maybe flag expecting content)

                // TODO: when attribute token with value == name

                // TODO: when attribute value token with value between quotes

                // TODO: when empty element

                // TODO: peek complex content pushing name then recursive else simple content reading opaque value

                // TODO: when ending element peek check content type to match name
            } while (!readable && IsElementStartingTag());

            return readable;
        }

        /// <summary>
        /// Read miscellaneous non-terminals in order:
        ///   miscellaneous ::= comment | processing-instruction | spacing
        /// Spacing is not a token - it only advances the segment position - so it is drained
        /// first (there can be more than one run of it once a comment/PI's own trailing spacing
        /// and the next miscellaneous item's leading spacing are both considered) before comment
        /// and processing instruction, which are opaque values, are tried.
        /// </summary>
        /// <returns></returns>
        internal bool ReadMiscellaneous()
        {
            while (ReadSpacing())
            {
            }

            return ReadOpaqueValue("<!--"u8, "-->"u8, TokenType.Comment) ||
                   ReadOpaqueValue("<?"u8, "?>"u8, TokenType.ProcessInstruction);
        }

        /// <summary>
        /// Read (i.e. skip) a contiguous run of whitespace at the current segment position.
        /// Spacing is not a token - it never sets <see cref="TokenType"/>/<see cref="Value"/> -
        /// it only advances the segment position (and line number/position) past whatever
        /// whitespace is immediately available right now. Returns false, not an error, when
        /// there's no whitespace to consume at the current position.
        /// </summary>
        /// <returns></returns>
        internal bool ReadSpacing()
        {
            int whiteSpaceLength = _segment[_segmentPosition..].IndexOfExceptWhiteSpace();
            if (whiteSpaceLength == 0)
            {
                return false;
            }

            ReadOnlySpan<byte> whiteSpace = _segment.Slice(_segmentPosition, whiteSpaceLength);
            (int lineNumbersRead, int lastLineFeedIndex) = Utf8Reader.LineFeeds(whiteSpace);

            if (lineNumbersRead > 0)
            {
                _lineNumber += lineNumbersRead;
                _linePosition = whiteSpaceLength - lastLineFeedIndex - 1;
            }
            else
            {
                _linePosition += whiteSpaceLength;
            }

            _segmentPosition += whiteSpaceLength;

            return true;
        }

        /// <summary>
        /// Read opaque value including and between the starting and ending
        /// terminals then if able set the current token type to <paramref name="tokenType"/>
        /// </summary>
        /// <param name="startingTerminal"></param>
        /// <param name="endingTerminal"></param>
        /// <param name="tokenType"></param>
        /// <returns></returns>
        private bool ReadOpaqueValue(
            ReadOnlySpan<byte> startingTerminal, 
            ReadOnlySpan<byte> endingTerminal, 
            TokenType tokenType
        ) => _isMultipleSegments
            ? ReadMultipleSegmentOpaqueValue(startingTerminal, endingTerminal, tokenType)
            : ReadSingleSegmentOpaqueValue(startingTerminal, endingTerminal, tokenType);

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
        /// Read opaque value from a multiple segments
        /// </summary>
        /// <param name="startingTerminal"></param>
        /// <param name="endingTermianl"></param>
        /// <returns></returns>
        internal bool ReadMultipleSegmentOpaqueValue(
            ReadOnlySpan<byte> startingTerminal,
            ReadOnlySpan<byte> endingTermianl,
            TokenType tokenType
        )
        {
            return false;
        }
        #endregion

        #region peek
        /// <summary>
        /// Element starting tags have a '<' character then valid name characters
        /// </summary>
        /// <returns></returns>
        private bool IsElementStartingTag()
        {
            if (_segmentPosition >= _segment.Length || _segment[_segmentPosition] != (byte)'<')
            {
                return false;
            }

            if (_segmentPosition + 1 < _segment.Length)
            {
                return EBNF.IsNameStartingCharacter(_segment[_segmentPosition + 1]);
            }

            if (!_isMultipleSegments)
            {
                return false; // exhausted after '<' when no sequencing
            }

            SequencePosition peekPosition = _nextSequencePosition;   
            while (_sequence.TryGet(ref peekPosition, out ReadOnlyMemory<byte> nextMemory, advance: true))
            {
                if (nextMemory.Length > 0) // else empty
                {
                    return EBNF.IsNameStartingCharacter(nextMemory.Span[0]);
                }
            }

            return false; // exhausted after '<' when sequencing
        }
        #endregion
    }
}