using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Forestry.Xml.Reading
{
    /// <summary>
    /// Utf8XmlReader read delegation
    /// </summary>
    public ref partial struct Utf8XmlReader
    {
        #region constructor
        /// <summary>
        /// Reader construction from a byte span where reading is implicitly completed 
        /// and the reader state is created from optional reader options
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="readerOptions"></param>
        public Utf8XmlReader(
            ReadOnlySpan<byte> segment,
            ReaderOptions readerOptions = default
        ): this(segment, isReadingCompleted: true, new ReaderState(readerOptions)) {}

        /// <summary>
        /// Reader construction from a byte span where the reader completed flag and reader 
        /// state are explicit
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="isReadingCompleted"></param>
        /// <param name="readerState"></param>
        public partial Utf8XmlReader(
            ReadOnlySpan<byte> segment,
            bool isReadingCompleted,
            ReaderState readerState
        ); 

        /// <summary>
        /// Reader construction from a byte sequence where reading is implicitly completed 
        /// and the reader state is created from optional reader options
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="readerOptions"></param>
        public Utf8XmlReader(
            ReadOnlySequence<byte> segments,
            ReaderOptions readerOptions = default
        ): this(segments, isReadingCompleted: true, new ReaderState(readerOptions)) {}

        /// <summary>
        /// Reader construction from a byte sequence where the reader completed flag and reader 
        /// state are explicit
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="isReadingCompleted"></param>
        /// <param name="readerState"></param>
        public partial Utf8XmlReader(
            ReadOnlySequence<byte> sequence,
            bool isReadingCompleted,
            ReaderState readerState
        );
        #endregion

        #region segment
        /// <summary>
        /// Current (active) segment when advancing
        /// </summary>
        private ReadOnlySpan<byte> _segment;

        /// <summary>
        /// Position (index) in the current segment
        /// </summary>
        private int _segmentPosition;

        /// <summary>
        /// Only false when explicit in constructing the reader otherwise defaults 
        /// to true meaning that reading is completed i.e. no more byte spans or byte sequences
        /// </summary>
        private readonly bool _isReadingCompleted;

        /// <summary>
        /// Only false when explicit in constructing the reader otherwise defaults 
        /// to true meaning that reading is completed i.e. no more byte spans or byte sequences
        /// </summary>
        public readonly bool IsReadingCompleted => _isReadingCompleted;

        /// <summary>
        /// Current == last segment
        /// </summary>
        private bool _isLastSegment;

        /// <summary>
        /// When reading is completed and the current == last byte span in multiple segments or there is only a single segment
        /// </summary>
        /// <summary>
        /// Bytes left from the committed position to the end of the data, the offset an
        /// Incomplete exception reports (#43)
        /// </summary>
        internal long RemainingLength => _isMultipleSegments
            ? _sequence.Slice(SequencePosition).Length
            : _segment.Length - _segmentPosition;

        /// <summary>
        /// Line number and line position (0-based) of the byte an offset ahead of the
        /// committed position, walking the bytes in between with the same line rules as
        /// reading. Nothing is committed (#43)
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        internal (long, long) GetLineTrackingAt(long offset)
        {
            long lineNumber = _lineNumber;
            long linePosition = _linePosition;

            if (offset == 0)
            {
                return (lineNumber, linePosition);
            }

            if (_isMultipleSegments)
            {
                foreach (ReadOnlyMemory<byte> memory in _sequence.Slice(SequencePosition, offset))
                {
                    (lineNumber, linePosition) = Utf8Reader.Track(lineNumber, linePosition, memory.Span);
                }

                return (lineNumber, linePosition);
            }

            return Utf8Reader.Track(lineNumber, linePosition, _segment.Slice(_segmentPosition, (int)offset));
        }

        private readonly bool IsLastReadableSegment => _isReadingCompleted && (!_isMultipleSegments || _isLastSegment);
        #endregion

        #region sequence
        /// <summary>
        /// When current segment is from a byte sequence i.e. multiple segments
        /// </summary>
        private readonly bool _isMultipleSegments;

        /// <summary>
        /// Segments including the current segment from a byte sequence
        /// </summary>
        private readonly ReadOnlySequence<byte> _sequence;

        /// <summary>
        /// Advancement position when the reader has been constructed from 
        /// a byte sequence i.e. not set when constructed from a byte segment 
        /// </summary>
        private long _advancementPosition;

        /// <summary>
        /// When the reader has been constructed from a byte sequence
        /// </summary>
        private bool _isByteSequence;

        /// <summary>
        /// Position (index) in segments including the current segment from a the byte sequence 
        /// </summary>
        private SequencePosition _currentSequencePosition;

        /// <summary>
        /// Next position in segments including the current segment from a the byte sequence
        /// </summary>
        private SequencePosition _nextSequencePosition;
        #endregion

        #region state
        /// <summary>
        /// Reader state from internal properties about the document position,
        /// token, reader options and element stack
        /// </summary>
        public readonly ReaderState ReaderState => new(
            lineNumber: _lineNumber,
            linePosition: _linePosition,
            documentType: _documentType,
            hasSkippedSpacingInStartTag: _hasSkippedSpacingInStartTag,
            hasSkippedAttributeDelimiter: _hasSkippedAttributeDelimiter,
            byteOrderMark: _byteOrderMark,
            currentTokenType: _currentTokenType,
            previousTokenType: _previousTokenType,
            elementStack: _elementStack,
            readerOptions: _readerOptions
        );

        /// <summary>
        /// Document line number i.e. top to bottom
        /// </summary>
        private long _lineNumber;

        /// <summary>
        /// Document line position i.e. left to right
        /// </summary>
        private long _linePosition;

        /// <summary>
        /// Advancement past a document type non-terminal
        /// </summary>
        private bool _documentType;

        /// <summary>
        /// When spacing has been skipped while inside a start tag
        /// </summary>
        private bool _hasSkippedSpacingInStartTag;

        /// <summary>
        /// When attribute delimiter has been skipped and the curren token == Attribute
        /// </summary>
        private bool _hasSkippedAttributeDelimiter;

        /// <summary>
        /// What the Skip BOM step decided at the start of the document (spec 001 research R7)
        /// </summary>
        private ByteOrderMark _byteOrderMark;

        /// <summary>
        /// Mutable current token type
        /// </summary>
        private TokenType _currentTokenType;

        /// <summary>
        /// Current (active) token type
        /// </summary>
        public readonly TokenType TokenType => _currentTokenType;

        /// <summary>
        /// Previous token type
        /// </summary>
        private TokenType _previousTokenType;

        /// <summary>
        /// Starting position of the current token in the segment
        /// </summary>
        public long TokenPosition { get; private set; }

        /// <summary>
        /// Position in the current segment plus when multiple segment the positions of the previous segments
        /// </summary>
        public readonly long Position {
            get {
                #if DEBUG
                if (!_isMultipleSegments)
                {
                    Debug.Assert(_advancementPosition == 0);
                }
                #endif

                return _advancementPosition + _segmentPosition;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private ElementStack _elementStack;

        /// <summary>
        /// Depth in the element non-terminal
        /// </summary>
        public readonly int Depth
        {
            get
            {
                return _elementStack.Depth;
            }
        }

        /// <summary>
        /// Root element exists
        /// </summary>
        public readonly bool RootElement
        {
            get
            {
                return _elementStack.RootElement;
            }
        }

        /// <summary>
        /// Reader options
        /// </summary>
        private ReaderOptions _readerOptions;
        #endregion

        #region value
        /// <summary>
        /// Value could not fit inside a single byte span <see cref="Value"/> instead inside a 
        /// byte sequence <see cref="ValueSequence"/>
        /// </summary>
        public bool HasValueSequence { get; private set; }

        /// <summary>
        /// Byte sequence value
        /// </summary>
        public ReadOnlySequence<byte> ValueSequence { get; private set; }

        /// <summary>
        /// Byte span value
        /// </summary>
        public ReadOnlySpan<byte> Value { get; private set; }

        /// <summary>
        /// The current Value's raw bytes contain at least one character reference or predefined
        /// entity reference (<c>&amp;amp;</c> <c>&amp;lt;</c> <c>&amp;gt;</c> <c>&amp;quot;</c>
        /// <c>&amp;apos;</c>), not resolved: the caller unescapes before using the value. False
        /// for every other token and for a Value without references. Like <see cref="Value"/>,
        /// it lives for one <see cref="Read"/>. When true, every reference in the Value is
        /// well-formed. The behaviour follows <c>Utf8JsonReader.ValueIsEscaped</c>.
        /// </summary>
        public bool HasEscapedValue { get; private set; }
        #endregion

        #region starting terminals
        /// <summary>
        /// Largest number of characters in allowed starting terminals i.e. the
        /// document type <c>&lt;!DOCTYPE</c>
        /// </summary>
        internal const int StartingTerminalsLength = 9;

        /// <summary>
        /// Fixed number of characters living inline in the reader - no heap
        /// allocation per reader construction
        /// </summary>
        [InlineArray(StartingTerminalsLength)]
        internal struct StartingTerminals
        {
            private byte _character;
        }

        /// <summary>
        /// Scratch pad for starting terminals that is transient
        /// </summary>
        internal StartingTerminals _startingTerminals;

        /// <summary>
        /// Character count in the scratch pad
        /// </summary>
        internal int _startingTerminalCharacterCount;
        #endregion

        #region skipping, peeking, draining
        /// <summary>
        /// Assert state of XML document when reading as completed 
        /// e.g. document is malformed or reader options are violated
        /// </summary>
        /// <returns></returns>
        private bool AssertStateWhenLastReadableSegment()
        {
            Debug.Assert(IsLastReadableSegment);

            if (!RootElement)
            {
                Throwing.ThrowXmlException(ref this, Throwing.Category.Incomplete, Throwing.Markup.RootElement, offset: RemainingLength);
            }

            if (Depth != 0)
            {
                Throwing.ThrowXmlException(ref this, Throwing.Category.Incomplete, Throwing.Markup.Element, offset: RemainingLength);
            }

            // TODO: Reader option policies when comments or other accepted prolog + miscellaneous non-terminals

            return true;
        }

        /// <summary>
        /// Skip a UTF-8 byte order mark at the start of the document, without line tracking,
        /// returning true to continue or false when reading is halted (spec 001 US3, research R7).
        /// Runs only until it has decided, recorded in <see cref="_byteOrderMark"/>
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><c>EF BB BF</c>: skipped (FR-012), then the drained check: a document of only a
        /// BOM is empty</item>
        /// <item><c>FE FF</c>, <c>FF FE</c> (and so <c>FF FE 00 00</c>), <c>00 00 FE FF</c>: not
        /// supported at line 1, position 1 (FR-013)</item>
        /// <item>An incomplete start of one of those at the end of the data halts when reading is
        /// not completed (C1); when it is, <c>EF</c> or <c>EF BB</c> is an incomplete document,
        /// and the others are not a BOM: nothing skipped, so they start no markup as today</item>
        /// <item>Anything else: nothing skipped</item>
        /// </list>
        /// </remarks>
        /// <returns></returns>
        internal bool SkipByteOrderMark()
        {
            if (_byteOrderMark != ByteOrderMark.Unchecked)
            {
                return true;
            }

            Span<byte> peeked = stackalloc byte[4];
            int count = 0;
            while (count < peeked.Length && PeekCharacter(count, out byte character))
            {
                peeked[count++] = character;
            }

            ReadOnlySpan<byte> start = peeked[..count];

            if (start.StartsWith(EBNF.Utf8Bom))
            {
                _byteOrderMark = ByteOrderMark.Utf8;

                if (_isMultipleSegments)
                {
                    AdvanceMultipleSegments(EBNF.Utf8Bom.Length);
                }
                else
                {
                    _segmentPosition += EBNF.Utf8Bom.Length;
                }

                // Leave the reader on a readable byte (rule 12): halt, or throw for a document of only a BOM
                return !(_isMultipleSegments ? IsMultipleSegmentDrained() : IsSingleSegmentDrained());
            }

            if (start.StartsWith(Utf16BigEndianBom) || start.StartsWith(Utf16LittleEndianBom) || start.StartsWith(Utf32BigEndianBom))
            {
                Throwing.ThrowXmlException(ref this, Throwing.Category.NotSupported, Throwing.Markup.ByteOrderMark);
            }

            bool isIncomplete = IsIncompleteStart(start, EBNF.Utf8Bom);
            if (isIncomplete || IsIncompleteStart(start, Utf16BigEndianBom) || IsIncompleteStart(start, Utf16LittleEndianBom) || IsIncompleteStart(start, Utf32BigEndianBom))
            {
                if (!_isReadingCompleted)
                {
                    return false;  // halt, nothing committed
                }

                if (isIncomplete)
                {
                    Throwing.ThrowXmlException(ref this, Throwing.Category.Incomplete, Throwing.Markup.RootElement, offset: RemainingLength);
                }
            }

            _byteOrderMark = ByteOrderMark.None;
            return true;

            static bool IsIncompleteStart(ReadOnlySpan<byte> start, ReadOnlySpan<byte> byteOrderMark) =>
                start.Length < byteOrderMark.Length && byteOrderMark.StartsWith(start);
        }

        private static ReadOnlySpan<byte> Utf16BigEndianBom => [0xFE, 0xFF];

        private static ReadOnlySpan<byte> Utf16LittleEndianBom => [0xFF, 0xFE];

        private static ReadOnlySpan<byte> Utf32BigEndianBom => [0x00, 0x00, 0xFE, 0xFF];

        /// <summary>
        /// Skip delimiter returning true to continue (i.e. delimiter or nothing skipped) or
        /// false when reading is halted
        /// </summary>
        /// <returns></returns>
        internal bool SkipDelimiter()
        {
            if (_currentTokenType == TokenType.Attribute)
            {
                return SkipEqualsDelimiter();
            }

            return true;
        }

        /// <summary>
        /// Skip equals delimiter between the attribute name non-terminal and 
        /// the attribute value non-terminal
        /// </summary>
        /// <returns></returns>
        internal bool SkipEqualsDelimiter()
        {
            Debug.Assert(_currentTokenType == TokenType.Attribute, "The equals delimiter is only valid between the attribute name and attribute value non-terminals.");

            if (_hasSkippedAttributeDelimiter)
            {
                return true;
            }
            
            if (!TryPeekNonSpacing(offset: 0, out byte delimiter, out long beforeBytesConsumed))
            {
                goto Undecided;  // S5
            }

            if (delimiter != EBNF.EqualsSign)
            {
                Throwing.ThrowXmlException(ref this, Throwing.Category.Expected, Throwing.Markup.EqualsSignTerminal, foundByte: delimiter, offset: beforeBytesConsumed);  // S4
            }

            if (!TryPeekNonSpacing(beforeBytesConsumed + 1, out _, out _))
            {
                goto Undecided;  // S7, S8
            }
            
            goto Read;  // S0, S1, S2, S3

            Undecided:
                if (!_isReadingCompleted)
                {
                    return false;
                }

                Throwing.ThrowXmlException(ref this, Throwing.Category.Incomplete, Throwing.Markup.Element, offset: RemainingLength);  // S6, S9
                return false;

            Read:
                if (beforeBytesConsumed != 0) {
                    if (_isMultipleSegments) SkipMultipleSpacing(); else SkipSingleSpacing(); // S1
                }

                _segmentPosition += 1;
                _linePosition += 1;
                _hasSkippedAttributeDelimiter = true;

                if (_isMultipleSegments) SkipMultipleSpacing(); else SkipSingleSpacing(); // S2
         
                return true;
        }

        /// <summary>
        /// Skip spacing returning true to continue (i.e. spacing or nothing skipped) or 
        /// false when reading is halted
        /// </summary>
        internal bool SkipSpacing()
        {
            byte character = _segment[_segmentPosition];
            if (!(character == EBNF.Space || character == EBNF.Tab || character == EBNF.LineFeed || character == EBNF.CarriageReturn)) {
                return true;  // S0: nothing to skip
            }

            if (_elementStack.ContentReady is false || _elementStack.Depth == 0) { // S1: prolog, miscellanous or start tag
                if (_isMultipleSegments)
                {
                    SkipMultipleSpacing();
                } else
                {
                    SkipSingleSpacing();
                }

                if (_elementStack.Depth != 0 && _elementStack.ContentReady is false) {
                    _hasSkippedSpacingInStartTag = true;  // S1.a (only start tag)
                }

                if (_isMultipleSegments ? IsMultipleSegmentDrained() : IsSingleSegmentDrained())
                {
                    return false;
                }
            } else
            {
                // S2: spacing may be character data when content ready and not past the root element.
                // Search for the first non-spacing character without advancing (simulated rollback)
                bool hasNonSpacing;
                byte nonSpacing = default;

                if (_isMultipleSegments)
                {
                    SequenceReader<byte> sequenceReader = new(_sequence.Slice(SequencePosition));
                    sequenceReader.AdvancePastAny(" \t\r\n"u8);

                    hasNonSpacing = sequenceReader.TryPeek(out nonSpacing);
                } else
                {
                    ReadOnlySpan<byte> remaining = _segment[_segmentPosition..];
                    int index = remaining.IndexOfExceptWhiteSpace();

                    hasNonSpacing = index < remaining.Length;
                    if (hasNonSpacing)
                    {
                        nonSpacing = remaining[index];
                    }
                }

                if (hasNonSpacing)
                {
                    if (nonSpacing != EBNF.LessThan)
                    {
                        return true;  // S4: the spacing belongs to character data, skip nothing
                    }

                    // S3: character data that is only spacing is ignored, skip it with line tracking
                    if (_isMultipleSegments)
                    {
                        SkipMultipleSpacing();
                    } else
                    {
                        SkipSingleSpacing();
                    }

                    return true;
                }

                if (!_isReadingCompleted)
                {
                    return false;  // S5: undecided, skip nothing and halt so the caller expands the segment(s)
                }

                // S6: only spacing left and no more segment(s), skip it draining the segment(s) which
                // asserts the reader state i.e. an element not ended
                if (_isMultipleSegments)
                {
                    SkipMultipleSpacing();
                } else
                {
                    SkipSingleSpacing();
                }

                if (_isMultipleSegments ? IsMultipleSegmentDrained() : IsSingleSegmentDrained())
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Peek starting terminal
        /// </summary>
        /// <returns></returns>
        internal bool PeekStartingTerminal()
        {
            _startingTerminalCharacterCount = 0;

            while (true)
            {
                if (_startingTerminalCharacterCount >= StartingTerminalsLength)
                {
                    Debug.Assert(false, "Scratch pad overflow - S1 or S2 should have been asserted.");
                    break;  // guard: scratch pad overflow
                }

                if (PeekCharacter(_startingTerminalCharacterCount, out byte character))
                {
                    if (_startingTerminalCharacterCount == 0 && !EBNF.IsStartingTerminalsFirstCharacter(character)) 
                    {
                        return true;  // S5 no starting terminal begins with the first character
                    }

                    _startingTerminals[_startingTerminalCharacterCount] = character;
                    _startingTerminalCharacterCount += 1; 

                    ReadOnlySpan<byte> scratchPad = _startingTerminals;
                    scratchPad = scratchPad[.._startingTerminalCharacterCount];

                    // S0: a longer allowed starting terminal starts with the scratch pad
                    bool isPrefix = false;
                    foreach (byte[] terminal in EBNF._allowedStartingTerminals)
                    {
                        if (terminal.Length > scratchPad.Length && terminal.AsSpan().StartsWith(scratchPad))
                        {
                            isPrefix = true;
                            break;
                        }
                    }

                    if (!isPrefix && !scratchPad.SequenceEqual(EBNF.DeclarationStartingTerminal))  // Note: == operator only works when pointing to the same memory
                    {
                        return true; // S1.A or S2 ignoring malformed markup
                    }

                    // S1.b (<?xml) continues to append the next character
                } else
                {
                    return _isReadingCompleted; // S3, S4
                }
            }

            return true;
        }

        /// <summary>
        /// Peek the character at offset from the segment position, continuing 
        /// into following segments of a byte sequence without advancing.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="character"></param>
        /// <returns></returns>
        /// <remarks>
        /// readonly makes the compiler reject any assignment to the reader's 
        /// fields, enforcing #17's Peek only requirement.
        /// </remarks>
        private readonly bool PeekCharacter(int offset, out byte character)
        {
            character = default;
            int index = _segmentPosition + offset;

            if (index < (uint)_segment.Length)
            {
                character = _segment[index];
                return true;
            }

            if (!_isMultipleSegments)
            {
                return false;
            }

            index -= _segment.Length;
            SequencePosition position = _nextSequencePosition;

            while (_sequence.TryGet(ref position, out ReadOnlyMemory<byte> memory, advance: true))
            {
                if (index < memory.Length)
                {
                    character = memory.Span[index];
                    return true;
                }

                index -= memory.Length; // empty segments subtract 0
            }

            return false;
        }

        /// <summary>
        /// Peek the first non-spacing byte at or after the offset without advancing
        /// </summary>
        private bool TryPeekNonSpacing(
            long offset, 
            out byte character, 
            out long index
        ) {
            character = default;

            if (_isMultipleSegments)
            {
                SequenceReader<byte> sequenceReader = new(_sequence.Slice(SequencePosition));
                sequenceReader.Advance(offset);
                sequenceReader.AdvancePastAny(" \t\r\n"u8);

                index = sequenceReader.Consumed;
                return !sequenceReader.End && sequenceReader.TryPeek(out character);
            }

            ReadOnlySpan<byte> remaining = _segment[(_segmentPosition + (int)offset)..];
            int nonSpacing = remaining.IndexOfExceptWhiteSpace();

            index = offset + nonSpacing;
            if (nonSpacing < remaining.Length)
            {
                character = remaining[nonSpacing];
                return true;
            }

            return false;
        }
        #endregion

        #region read delegation
        /// <summary>
        /// Read the value of the next token
        /// </summary>
        /// <returns></returns>
        public bool Read()
        {
            bool advancement = false;

            // Unreliable value
            Value = default;
            ValueSequence = default;
            HasEscapedValue = false;
            
            // Break advancement fast when segment is drained
            if (_isMultipleSegments ? IsMultipleSegmentDrained() : IsSingleSegmentDrained())
            {
                goto Halted;
            }

            // Skip a byte order mark at the start of the document (spec 001 US3, research R7)
            if (!SkipByteOrderMark())
            {
                goto Halted;
            }

            // Skip delimiters (potentially bounded by spacing)
            if (!SkipDelimiter())
            {
                goto Halted;
            }

            // Skip spacing
            if (!SkipSpacing())
            {
                goto Halted;
            }

            // Content ready
            ContentReady();

            // Empty element ending terminal
            bool? hasEmptyElementEndingTerminal = TryReadEmptyElementEndingTerminal();
            if (hasEmptyElementEndingTerminal is not null)
            {
                advancement = hasEmptyElementEndingTerminal.Value;
                goto Completed;
            }

            // Content ready may skip '>' (S3 or S5) draining the segment
            if (_isMultipleSegments ? IsMultipleSegmentDrained() : IsSingleSegmentDrained())
            {
                goto Completed;
            }

            // Skip spacing only character data after the start tag's '>'
            if (!SkipSpacingOnlyCharacterData())
            {
                goto Halted;
            }

            // Read value
            return ReadValue();

            Halted:
                return false;

            Completed:
                return advancement;
        }

        /// <summary>
        /// Skip spacing only character data in the read cycle where content ready
        /// skipped the start tag's '>', returning true to continue (i.e. spacing or
        /// nothing skipped) or false when reading is halted
        /// </summary>
        /// <remarks>
        /// Later read cycles in content skip spacing only character data in
        /// <see cref="SkipSpacing"/> before content ready
        /// </remarks>
        /// <returns></returns>
        internal bool SkipSpacingOnlyCharacterData()
        {
            bool isAfterStartTag = _currentTokenType == TokenType.Element
                || (_currentTokenType == TokenType.Value && _previousTokenType == TokenType.Attribute);

            if (!(_elementStack.ContentReady && _elementStack.Depth != 0 && isAfterStartTag))
            {
                return true;  // not right after a start tag
            }

            if (!TryPeekNonSpacing(offset: 0, out byte character, out long index))
            {
                if (!_isReadingCompleted)
                {
                    return false;  // undecided, nothing skipped
                }

                Throwing.ThrowXmlException(ref this, Throwing.Category.Incomplete, Throwing.Markup.CharacterData, offset: RemainingLength);
            }

            if (index != 0 && character == EBNF.LessThan)
            {
                // Never SkipSpacing() which would set the spacing flag
                if (_isMultipleSegments) SkipMultipleSpacing(); else SkipSingleSpacing();
            }

            return true;  // spacing only skipped, or spacing starting text left alone
        }

        /// <summary>
        /// When first character == '>' then determine if the element non-terminal
        /// has a content non-terminal after the start tag
        /// </summary>
        /// <returns></returns>
        internal void ContentReady()
        {
            byte character = _segment[_segmentPosition];

            if (character == EBNF.StartTagEndingTerminal)
            {
                if (_elementStack.Depth == 0 && _currentTokenType == TokenType.None)
                {
                    goto IgnoreMalformed;  // S0
                }

                if (_elementStack.Depth == 0 && (_elementStack.RootElement is false || _elementStack.RootElement is true))
                {
                    goto IgnoreMalformed;  // S1 || S2
                }

                if (_elementStack.Depth != 0 && _elementStack.ContentReady is false && _currentTokenType == TokenType.Element)
                {
                    goto Skip; // S3
                }

                if (_elementStack.Depth != 0 && _elementStack.ContentReady is false && _currentTokenType == TokenType.Attribute)
                {
                    Throwing.ThrowXmlException(ref this, Throwing.Category.Expected, Throwing.Markup.AttributeValue); // S4
                }

                if (_elementStack.Depth != 0 && _elementStack.ContentReady is false && _currentTokenType == TokenType.Value && _previousTokenType == TokenType.Attribute)
                {
                    goto Skip; // S5
                }

                if (_elementStack.Depth != 0 && _elementStack.ContentReady is false && _currentTokenType == TokenType.Value && (_previousTokenType == TokenType.Value || _previousTokenType == TokenType.Element))
                {
                    goto IgnoreWellformed; // S6
                }

                if (_elementStack.Depth != 0 && _elementStack.ContentReady is true)
                {
                    goto IgnoreWellformed; // S7
                }

                Debug.Assert(false, "No S0-S7 state matched - reader state is unreliable.");
            }

            // break fast when not the '>' character
            return;

            Skip:
                _segmentPosition += 1;
                _linePosition += 1;  // the '>' is a column too (#30's one-line fix, spec 001 US1)
                _elementStack.NegateContentReady();

                _hasSkippedSpacingInStartTag = false;
                return;

            IgnoreMalformed:
                return;

            IgnoreWellformed:
                return;
        }

        /// <summary>
        /// Try reading the ending terminal of an empty element when the current character is 
        /// not '/' return null else then assert:
        /// - advancement past the '/>' characters then return false
        /// - '/' is the last assessible character and reading is not completed then return true
        /// - '>' not immediately after the '/' character then throw malformed
        /// - '/' is the last assessible character and reading is completed then throw malformed
        /// </summary>
        /// <returns></returns>
        internal bool? TryReadEmptyElementEndingTerminal()
        {
            if (_elementStack.ContentReady || _elementStack.Depth == 0) // S0
            {
                return null;
            }

            if (_segment[_segmentPosition] != EBNF.Slash) // S1
            {
                return null;
            }

            if (!PeekCharacter(1, out byte character))
            {
                if (!_isReadingCompleted) // S2
                {
                    return false;
                }

                Throwing.ThrowXmlException(ref this, Throwing.Category.Incomplete, Throwing.Markup.EndingTerminal, bytes: EBNF.EmptyElementTagEndingTerminal, offset: RemainingLength);  // S3
            }

            if (character != EBNF.StartTagEndingTerminal) 
            {
                Throwing.ThrowXmlException(ref this, Throwing.Category.Expected, Throwing.Markup.EmptyElementEndingTerminal, foundByte: character, offset: 1);  // S4
            }

            if (_currentTokenType == TokenType.Attribute)
            {
                Throwing.ThrowXmlException(ref this, Throwing.Category.Expected, Throwing.Markup.AttributeValue);  // S5.a
            }

            _previousTokenType = _currentTokenType;
            _currentTokenType = TokenType.ElementEnd;
            _elementStack.Pop();

            if (_isMultipleSegments)
            {
                AdvanceMultipleSegments(2);
            }
            else
            {
                _segmentPosition += 2;
            }
            
            _linePosition += 2;
            _hasSkippedSpacingInStartTag = false; // S5.b

            return true; 
        }

        /// <summary>
        /// Read values by peeking for a starting terminal and if one exists then 
        /// get a candidate non-terminal to delegate reading
        /// </summary>
        /// <returns></returns>
        internal bool ReadValue()
        {
            if (!PeekStartingTerminal()) return false;

            switch (new CandidateNonTerminal(_startingTerminals[.._startingTerminalCharacterCount], in _elementStack, _currentTokenType).NonTerminal)
            {
                case NonTerminal.Declaration:
                    return ReadDeclaration();
                case NonTerminal.DocumentType:
                    return ReadDocumentType();
                case NonTerminal.Comment:
                    return ReadComment();
                case NonTerminal.ProcessingInstruction:
                    return ReadProcessingInstruction();
                case NonTerminal.StartTag:
                    return ReadStartTag();
                case NonTerminal.EndTag:
                    return ReadEndTag();
                case NonTerminal.Attribute:
                    return ReadAttribute();
                case NonTerminal.AttributeValue:
                    return ReadAttributeValue();
                case NonTerminal.CharacterData:
                    return ReadContentCharacterData();
                case NonTerminal.None:
                    return ReadNone();
                default:
                    string message = "Candidate non-terminal likely added without read value delegation";

                    Debug.Assert(false, message);
                    throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Read opaque value from declaration non-terminal
        /// </summary>
        /// <returns></returns>
        internal bool ReadDeclaration()
        {
            if (_lineNumber != 0 || _linePosition != 0) {
                Throwing.ThrowXmlException(ref this, Throwing.Category.Order, Throwing.Markup.Declaration);
            }

            // The encoding name must be UTF-8; after a skipped UTF-8 BOM another name contradicts it (FR-014)
            DeclarationEncodingConstraint encoding = new() { HasUtf8ByteOrderMark = _byteOrderMark == ByteOrderMark.Utf8 };

            return _isMultipleSegments ?
                ReadMultipleOpaqueValue(EBNF.DeclarationStartingTerminal, EBNF.DeclarationEndingTerminal, TokenType.Declaration, encoding) :
                ReadSingleOpaqueValue(EBNF.DeclarationStartingTerminal, EBNF.DeclarationEndingTerminal, TokenType.Declaration, encoding);
        }

        /// <summary>
        /// Read opaque value from document type non-terminal
        /// </summary>
        /// <returns></returns>
        internal bool ReadDocumentType()
        {
            if (_documentType)
            {
                Throwing.ThrowXmlException(ref this, Throwing.Category.Order, Throwing.Markup.DocumentType);
            }
            
            bool advancement =  _isMultipleSegments ?
                ReadMultipleOpaqueValue(EBNF.DocumentTypeStartingTerminal, EBNF.DocumentTypeEndingTerminal, TokenType.DocumentType, new DocumentTypeConstraint()) :
                ReadSingleOpaqueValue(EBNF.DocumentTypeStartingTerminal, EBNF.DocumentTypeEndingTerminal, TokenType.DocumentType, new DocumentTypeConstraint());

            if (advancement)
            {
                _documentType = true;
            }

            return advancement;
        }

        /// <summary>
        /// Read opaque value from comment non-terminal
        /// </summary>
        /// <returns></returns>
        internal bool ReadComment() => _isMultipleSegments ?
                ReadMultipleOpaqueValue(EBNF.CommentStartingTerminal, EBNF.CommentEndingTerminal, TokenType.Comment, new NoConstraint()) :
                ReadSingleOpaqueValue(EBNF.CommentStartingTerminal, EBNF.CommentEndingTerminal, TokenType.Comment, new NoConstraint());

        /// <summary>
        /// Read opaque value from processing instruction non-terminal
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// The target (S5 + S6) is checked before the value is committed (#43)
        /// </remarks>
        internal bool ReadProcessingInstruction() => _isMultipleSegments ?
                ReadMultipleOpaqueValue(EBNF.ProcessingInstructionStartingTerminal, EBNF.ProcessingInstructionEndingTerminal, TokenType.ProcessInstruction, new ProcessingInstructionTargetConstraint { MaxLength = _readerOptions.MaxNameLength }) :
                ReadSingleOpaqueValue(EBNF.ProcessingInstructionStartingTerminal, EBNF.ProcessingInstructionEndingTerminal, TokenType.ProcessInstruction, new ProcessingInstructionTargetConstraint { MaxLength = _readerOptions.MaxNameLength });

        /// <summary>
        /// Read name has a simulated rollback when false and a committed advancement 
        /// when true
        /// </summary>
        /// <returns></returns>
        internal bool ReadName()
        {
            if (!EvaluateName(startingTerminalLength: 0, out int nameLength)) // S7
            {
                return false; // simulate rollback without advancement
            }

            AdvanceValue(ref this, startingTerminalLength: 0, valueLength: nameLength, consumedLength: nameLength, lineTracking: true); // S4, S8
            return true;
        }

        /// <summary>
        /// Evaluate a name after its starting terminal without committing anything, bounded by
        /// <c>MaxNameLength</c> (spec 001 US2): true when the name is decided, false to halt;
        /// throws when malformed, not supported or too long
        /// </summary>
        /// <param name="startingTerminalLength"></param>
        /// <param name="nameLength"></param>
        /// <returns></returns>
        private bool EvaluateName(int startingTerminalLength, out int nameLength)
        {
            NameMarkup markup = startingTerminalLength > 0
                ? new() { StartingTerminalLength = startingTerminalLength, MaxLength = _readerOptions.MaxNameLength }
                : new() { MaxLength = _readerOptions.MaxNameLength };
            MarkupState value = _isMultipleSegments ?
                EvaluateMultipleMarkup(ref markup, out nameLength, out MarkupReason reason) :
                markup.Evaluate(_segment[(_segmentPosition + startingTerminalLength)..], out nameLength, out reason);

            switch (value)
            {
                case MarkupState.WellFormed:
                    return true;  // S4
                case MarkupState.Malformed:
                    if (reason == MarkupReason.UnsupportedCharacter) // S5
                    {
                        Throwing.ThrowUnsupportedNameCharacter(ref this, offset: startingTerminalLength + nameLength);
                    }
                    if (reason == MarkupReason.TooLong)  // spec 001 US2
                    {
                        Throwing.ThrowXmlException(ref this, Throwing.Category.Limit, Throwing.Markup.Name, offset: startingTerminalLength + nameLength);
                    }

                    Throwing.ThrowXmlException(ref this, Throwing.Category.Name, Throwing.Markup.Name, offset: startingTerminalLength + nameLength);  // S6
                    break;
                case MarkupState.Unknown:
                    if (!_isReadingCompleted) // S7
                    {
                        return false;
                    }
                    if (nameLength > 0) // S8
                    {
                        return true;
                    }

                    Throwing.ThrowXmlException(ref this, Throwing.Category.Name, Throwing.Markup.Name, offset: RemainingLength); // S9
                    break;
                default:
                    Debug.Assert(false, "The name non-terminal evaluation returned an unexpected value.");
                    break;
            }

            return false;
        }

        /// <summary>
        /// Read the start tag's starting terminal &lt; and name, shared by
        /// element and empty element non-terminals; simulated rollback when false.
        /// </summary>
        /// <returns></returns>
        internal bool ReadStartTag()
        {
            Debug.Assert(_segment[_segmentPosition] == EBNF.LessThan, "The first character of a start tag is the starting terminal '<'.");

            const int startingTerminalLength = 1;

            // MaxDepth is enforced at the start tag, before anything is committed (S1, spec 001 US2)
            if (_elementStack.Depth >= _readerOptions.MaxDepth)
            {
                Throwing.ThrowXmlException(ref this, Throwing.Category.Limit, Throwing.Markup.Element, offset: startingTerminalLength);
            }

            if (!EvaluateName(startingTerminalLength, out int nameLength)) // S0
            {
                return false;
            }

            // S1: propagate exceptions

            // The whole name must fit the element stack, checked before committing (R4)
            if (!_elementStack.CanPush(nameLength))
            {
                Throwing.ThrowXmlException(ref this, Throwing.Category.NotSupported, Throwing.Markup.ElementStack, offset: startingTerminalLength);
            }

            AdvanceValue(ref this, startingTerminalLength: startingTerminalLength, valueLength: nameLength, consumedLength: startingTerminalLength + nameLength, lineTracking: true);
            _linePosition += startingTerminalLength;

            bool pushed = HasValueSequence  // S3 straddling, S2 in one span; copied straight into the stack
                ? _elementStack.TryPush(ValueSequence)
                : _elementStack.TryPush(Value);
            Debug.Assert(pushed, "The name was checked to fit before committing.");

            _previousTokenType = _currentTokenType;
            _currentTokenType = TokenType.Element;

            return true;  // S2-S5
        }

        /// <summary>
        /// Read the end tag's starting terminal &lt;/, name, optional spacing and ending terminal
        /// &gt;. Everything is evaluated before anything is committed (simulated rollback when
        /// false): the name, the ending terminal after only spacing, and the name against the top
        /// of the element stack. Then the name is popped and the reader advanced past &gt;.
        /// </summary>
        /// <returns></returns>
        internal bool ReadEndTag()
        {
            const int startingTerminalLength = 2;

            // Evaluate the name non-terminal after the starting terminal, never beyond the start-tag
            // name's length: a longer end tag is a mismatch (FR-011, spec 001 US2)
            NameMarkup markup = new() { StartingTerminalLength = startingTerminalLength, MaxLength = _elementStack.TopLength };
            MarkupState value = _isMultipleSegments ?
                EvaluateMultipleMarkup(ref markup, out int nameLength, out MarkupReason reason) :
                markup.Evaluate(_segment[(_segmentPosition + startingTerminalLength)..], out nameLength, out reason);

            switch (value)
            {
                case MarkupState.WellFormed:
                    break;
                case MarkupState.Malformed:  // S6-S8, S16 malformed whether or not reading is completed
                    if (reason == MarkupReason.UnsupportedCharacter)
                    {
                        Throwing.ThrowUnsupportedNameCharacter(ref this, offset: startingTerminalLength + nameLength);
                    }
                    if (reason == MarkupReason.TooLong)  // S15: longer than the start-tag name
                    {
                        Throwing.ThrowXmlException(ref this, Throwing.Category.Name, Throwing.Markup.EndTag, offset: startingTerminalLength);
                    }
                    Throwing.ThrowXmlException(ref this, Throwing.Category.Name, Throwing.Markup.Name, offset: startingTerminalLength + nameLength);
                    break;
                case MarkupState.Unknown:
                    if (!_isReadingCompleted) // S9
                    {
                        return false;
                    }
                    Throwing.ThrowXmlException(ref this, Throwing.Category.Incomplete, Throwing.Markup.EndTag, offset: RemainingLength);  // S10
                    break;
            }

            // Evaluate only optional spacing then the ending terminal, index is the offset of the first non-spacing byte
            if (!TryPeekNonSpacing(startingTerminalLength + nameLength, out byte character, out long index))
            {
                if (!_isReadingCompleted) // S13
                {
                    return false;
                }
                Throwing.ThrowXmlException(ref this, Throwing.Category.Incomplete, Throwing.Markup.EndTag, offset: RemainingLength);  // S14
            }

            if (character != EBNF.GreaterThan) // S11, S12, S16, S17
            {
                Throwing.ThrowXmlException(ref this, Throwing.Category.Expected, Throwing.Markup.EndingTerminal, foundByte: character, offset: index);
            }

            // Evaluate the name against the top of the element stack in full, a sequence compared
            // without copying (spec 001 US2, R4)
            bool matches = _isMultipleSegments
                ? _elementStack.TryPeek(_sequence.Slice(SequencePosition).Slice(startingTerminalLength, nameLength), out _)
                : _elementStack.TryPeek(_segment.Slice(_segmentPosition + startingTerminalLength, nameLength), out _);

            if (!matches) // S15
            {
                Throwing.ThrowXmlException(ref this, Throwing.Category.Name, Throwing.Markup.EndTag, offset: startingTerminalLength);
            }

            // Commit: pop, value, line tracking (spacing may hold line feeds), positions past the ending terminal, tokens
            _elementStack.Pop();

            AdvanceValue(ref this, startingTerminalLength: startingTerminalLength, valueLength: nameLength, consumedLength: (int)index + 1, lineTracking: false);

            _previousTokenType = _currentTokenType;
            _currentTokenType = TokenType.ElementEnd;

            return true;
        }

        /// <summary>
        /// The default delegate when the scratch pad starts no candidate non-terminal
        /// allowed in the context, always malformed
        /// </summary>
        /// <returns></returns>
        internal bool ReadNone()
        {
            // TODO: a specific malformed explanation per context and scratch pad (new task)
            Throwing.ThrowUnexpectedMarkup(ref this, _segment[_segmentPosition]);
            return false;
        }

        /// <summary>
        /// Read character data in content up to the next markup (less than), references
        /// included raw (spec 001 US1)
        /// </summary>
        /// <returns></returns>
        internal bool ReadContentCharacterData()
        {
            CharacterDataMarkup markup = new() { IsDocumentType = _documentType, MaxNameLength = _readerOptions.MaxNameLength };
            MarkupState value = _isMultipleSegments ?
                EvaluateMultipleMarkup(ref markup, out int length, out MarkupReason reason) :
                markup.Evaluate(_segment[_segmentPosition..], out length, out reason);

            switch (value)
            {
                case MarkupState.WellFormed:
                    Debug.Assert(length > 0, "Candidate S8 never starts character data at a less than.");

                    AdvanceValue(ref this, startingTerminalLength: 0, valueLength: length, consumedLength: length, lineTracking: false);  // S0, S1, S2, S6, S7
                    HasEscapedValue = markup.HasReferences;
                    goto Completed;
                case MarkupState.Malformed:
                    ThrowWhenReferenceMalformed(reason, offset: length, referenceStart: markup.ReferenceStart);
                    PeekCharacter(length, out byte found);
                    Throwing.ThrowXmlException(ref this, Throwing.Category.Expected, Throwing.Markup.CharacterData, foundByte: found, offset: length);  // S3
                    break;
                case MarkupState.Unknown:
                    if (!_isReadingCompleted)
                    {
                        return false;  // S4: simulate rollback without advancement
                    }

                    Throwing.ThrowXmlException(ref this, Throwing.Category.Incomplete, Throwing.Markup.CharacterData, offset: RemainingLength);  // S5
                    break;
                default:
                    Debug.Assert(false, "The character data evaluation returned an unexpected value.");
                    break;
            }

            return false;

            Completed:
                _previousTokenType = _currentTokenType;
                _currentTokenType = TokenType.Value;

                return true;
        }

        /// <summary>
        /// Read attribute name non-terminal starting with a spacing non-terminal 
        /// before the name non-terminal
        /// </summary>
        /// <returns></returns>
        internal bool ReadAttribute()
        {
            // Guard (#41): the equals non-terminal was skipped (#45) but no literal follows, e.g. <a b=1/>.
            // Before S0 so the right exception is thrown; reads no bytes, so it never halts.
            if (_currentTokenType == TokenType.Attribute && _hasSkippedAttributeDelimiter)
            {
                Throwing.ThrowXmlException(ref this, Throwing.Category.Expected, Throwing.Markup.AttributeValue);
            }

            if (!_hasSkippedSpacingInStartTag)
            {
                Throwing.ThrowXmlException(ref this, Throwing.Category.Expected, Throwing.Markup.Spacing); // S0
            }

            if (!ReadName()) // S1 + S2
            {
                return false;
            }

            // S3 + S4 + S5
            _hasSkippedSpacingInStartTag = false;
            _previousTokenType = _currentTokenType;
            _currentTokenType = TokenType.Attribute;

            return true;
        }

        /// <summary>
        /// Read attribute value non-terminal which is a literal
        /// </summary>
        /// <returns></returns>
        internal bool ReadAttributeValue()
        {
            if (_currentTokenType != TokenType.Attribute)
            {
                Throwing.ThrowXmlException(ref this, Throwing.Category.Expected, Throwing.Markup.AttributeValue);
            }

            byte quote = _segment[_segmentPosition];
            Debug.Assert(quote == EBNF.DoubleQuote || quote == EBNF.SingleQuote, "Candidate S7 guarantees the starting quote.");

            LiteralMarkup markup = new() { Quote = quote, IsDocumentType = _documentType, MaxNameLength = _readerOptions.MaxNameLength };
            MarkupState value = _isMultipleSegments ?
                EvaluateMultipleMarkup<LiteralMarkup>(ref markup, out int literalLength, out MarkupReason reason) :
                markup.Evaluate(_segment[(_segmentPosition + 1)..], out literalLength, out reason);

            // The offending byte or the ending quote is after the starting quote
            int offset = 1 + literalLength;

            switch (value)
            {
                case MarkupState.WellFormed:
                    AdvanceValue(ref this, startingTerminalLength: 1, valueLength: literalLength, consumedLength: literalLength + 2, lineTracking: false);  // S0-S5, S7, S9
                    HasEscapedValue = markup.HasReferences;
                    goto Completed;
                case MarkupState.Malformed:
                    ThrowWhenReferenceMalformed(reason, offset: offset, referenceStart: 1 + markup.ReferenceStart);  // S10
                    PeekCharacter(offset, out byte found);
                    Throwing.ThrowXmlException(ref this, Throwing.Category.Expected, Throwing.Markup.AttributeValue, foundByte: found, offset: offset);  // S6, S8
                    break;
                case MarkupState.Unknown:
                    if (!_isReadingCompleted)
                    {
                        return false;  // S11: simulate rollback without advancement
                    }

                    Throwing.ThrowXmlException(ref this, Throwing.Category.Incomplete, Throwing.Markup.AttributeValue, offset: RemainingLength);  // S12
                    break;
                default:
                    Debug.Assert(false, "The literal evaluation returned an unexpected value.");
                    break;
            }

            return false;

            Completed:
                _hasSkippedAttributeDelimiter = false;

                _previousTokenType = _currentTokenType;
                _currentTokenType = TokenType.Value;

                return true;            
        }

        /// <summary>
        /// Throw for a malformed or not supported reference (spec 001 US1, research.md R2) when
        /// the markup's reason says so; returns for any other reason
        /// </summary>
        /// <param name="reason"></param>
        /// <param name="offset">bytes from the committed position to the reference's offending byte</param>
        /// <param name="referenceStart">bytes from the committed position to the reference's <c>&amp;</c></param>
        private void ThrowWhenReferenceMalformed(MarkupReason reason, long offset, long referenceStart)
        {
            switch (reason)
            {
                case MarkupReason.MalformedReference:
                    PeekCharacter((int)offset, out byte found);
                    Throwing.ThrowXmlException(ref this, Throwing.Category.Expected, Throwing.Markup.Reference, foundByte: found, offset: offset);
                    break;
                case MarkupReason.UndeclaredEntity:
                    Throwing.ThrowXmlException(ref this, Throwing.Category.Name, Throwing.Markup.Reference, offset: offset);
                    break;
                case MarkupReason.NotSupportedEntity:
                    Throwing.ThrowXmlException(ref this, Throwing.Category.NotSupported, Throwing.Markup.EntityReference, offset: referenceStart);
                    break;
                case MarkupReason.TooLong:
                    Throwing.ThrowXmlException(ref this, Throwing.Category.Limit, Throwing.Markup.EntityReference, offset: offset);
                    break;
            }
        }

        /// <summary>
        /// Read value
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="startingTerminalLength"></param>
        /// <param name="valueLength"></param>
        /// <param name="consumedLength"></param>
        /// <param name="lineTracking"></param>
        static void AdvanceValue(
            ref Utf8XmlReader reader, 
            int startingTerminalLength,
            int valueLength,
            int consumedLength,
            bool lineTracking
        )
        {
            if (reader._isMultipleSegments)
            {
                ReadOnlySequence<byte> value = reader._sequence.Slice(reader.SequencePosition).Slice(startingTerminalLength, valueLength);

                // A value ending exactly at a segment's end isn't a single segment (its end
                // position is the start of the next segment) though its bytes are all first
                if (value.IsSingleSegment || value.FirstSpan.Length == valueLength)
                {
                    reader.Value = value.FirstSpan;
                    reader.ValueSequence = ReadOnlySequence<byte>.Empty;
                    reader.HasValueSequence = false;
                }
                else
                {
                    reader.Value = [];
                    reader.ValueSequence = value;
                    reader.HasValueSequence = true;
                }
            } else
            {
                int start = reader._segmentPosition + startingTerminalLength;

                reader.Value = reader._segment[start..(start + valueLength)];
                reader.ValueSequence = ReadOnlySequence<byte>.Empty;
                reader.HasValueSequence = false;
            }

            // Line tracking before advancing: either the value has no line feeds (names) so the line
            // position adds the value length, or the consumed bytes are walked (literals)
            if (lineTracking)
            {
                reader._linePosition += valueLength;
            } else
            {
                (reader._lineNumber, reader._linePosition) = reader.GetLineTrackingAt(consumedLength);
            }

            if (reader._isMultipleSegments)
            {
                reader.AdvanceMultipleSegments(consumedLength);
            } else
            {
                reader._segmentPosition += consumedLength;
            }
        }
        #endregion
    }
}