using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Forestry.Deserialize.Xml.Reading
{
    /// <summary>
    /// Public constructors, properties and methods
    /// </summary>
    public ref partial struct Utf8XmlReader
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
        /// Packed names of every currently-open element, innermost last - see
        /// <see cref="ReaderState._elementNameStack"/> for why there's no separate single-slot
        /// storage for a leaf any more.
        /// </summary>
        private ElementNameStack _elementNameStack;

        /// <summary>
        /// Scratch storage <see cref="ElementNameStack.Pop(Span{byte})"/> unpacks a popped
        /// name's raw bytes into, so an unconditional pop (e.g. an empty element closing itself)
        /// can set <see cref="Value"/> - a plain array, allocated once here at construction, not
        /// an `[InlineArray]`: converting an inline-array *field* to a span requires a real
        /// conversion that returns a reference into `this`, and a struct can't assign that
        /// result into a property (<see cref="Value"/>) at all, however directly it's written -
        /// a genuine C# restriction, not something written around it avoids. An array reference
        /// converts to a span without that step, matching how <see cref="_segment"/> itself
        /// already assigns into <see cref="Value"/> successfully.
        /// </summary>
        private readonly byte[] _poppedNameBuffer = new byte[ElementNameStack.PackedNameLength * 8];

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

        #region constructors
        /// <summary>
        /// Reading segment from a byte span
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="readerOptions"></param>
        public Utf8XmlReader(
            ReadOnlySpan<byte> segment,
            ReaderOptions readerOptions = default
        ): this(segment, isFinalSegment: true, new ReaderState(readerOptions)) {}

        /// <summary>
        /// Reading segment from a byte span using a reader state (where options follow along)
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="isFinalSegment"></param>
        /// <param name="readerState"></param>
        public partial Utf8XmlReader(
            ReadOnlySpan<byte> segment,
            bool isFinalSegment,
            ReaderState readerState
        ); 

        /// <summary>
        /// Reading segments from a byte sequence starting with the first segment
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="readerOptions"></param>
        public Utf8XmlReader(
            ReadOnlySequence<byte> segments,
            ReaderOptions readerOptions = default
        ): this(segments, isFinalSegment: true, new ReaderState(readerOptions)) {}

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
        public partial Utf8XmlReader(
            ReadOnlySequence<byte> segments,
            bool isFinalSegment,
            ReaderState readerState
        );
        #endregion

        #region public properties

        /// <summary>
        /// Position in the buffer.  Advancement sets the position after the value of 
        /// the current token including any drift e.g. from spacing.  Rollback resets 
        /// the position to the value of the previous token including any drift.
        /// </summary>
        public readonly long Position => default;  // TODO: total bytes read together with bytes after Read calls

        /// <summary>
        /// Depth in the element stack either affected by advancement or rollback.
        /// </summary>
        public readonly int Depth
        {
            get
            {
                int _depth = _elementNameStack.Depth;
                if (TokenType is TokenType.Element or TokenType.Attribute)  // TODO: token type == value of an attribute
                {
                    Debug.Assert(_depth >= 1);
                    _depth--;
                }

                return _depth;
            }
        }

        /// <summary>
        /// Reader state used to resume reading with a new reader
        /// </summary>
        public readonly ReaderState ReaderState => new(
            lineNumber: _lineNumber,
            linePosition: _linePosition,
            documentNonTerminal: _documentNonTerminal,
            currentTokenType: _currentTokenType,
            previousTokenType: _previousTokenType,
            elementNameStack: _elementNameStack,
            readerOptions: _readerOptions
        );

        /// <summary>
        /// Current token type.  Rollback reverts to the previous token type.
        /// </summary>
        public readonly TokenType TokenType => _currentTokenType;

        /// <summary>
        /// Position at the start of the current token in the buffer.
        /// </remarks>
        public long TokenPosition { get; private set; }

        /// <summary>
        /// Value could not fit inside a single byte span <see cref="Value"/> instead inside a 
        /// byte sequence <see cref="ValueSequence"/>
        /// </summary>
        public bool HasValueSequence { get; private set; }

        /// <summary>
        /// Value with sequencing including all EBNF terminals
        /// </summary>
        public ReadOnlySequence<byte> ValueSequence { get; private set; }

        /// <summary>
        /// Value without sequencing including all EBNF terminals
        /// </summary>
        public ReadOnlySpan<byte> Value { get; private set; }
        #endregion


        #region segment
        /// <summary>
        /// Is final segment
        /// </summary>
        public readonly bool IsFinalSegment => _isFinalSegment;

        /// <summary>
        /// The segment's underlying byte data is only accessible when the segment position is
        /// less than the segment length and the segment is not closed.
        ///
        /// Multiple segments access the next non-empty segment before throwing
        /// if the segment is not closed.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsSegmentFetchable()
        {
            if (_segmentPosition >= (uint)_segment.Length)
            {
                if (_isMultipleSegments && FetchNextSegment())
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
                ThrowableElementNotClosed();

                if (_documentNonTerminal == EBNF.Document.None || _documentNonTerminal == EBNF.Document.Prolog)
                {
                    throw new InvalidOperationException();  // TODO: formatting
                }
            }
        }

        /// <summary>
        /// Throws when the segment has closed while an element is still open -
        /// <see cref="_elementNameStack"/> has one or more names still pushed.
        /// </summary>
        private readonly void ThrowableElementNotClosed()
        {
            if (_elementNameStack.Depth > 0)
            {
                throw new InvalidOperationException(); // TODO: formatting
            }
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
        /// Advancement to the next token i.e. EBNF non-terminal and the value including all terminals
        /// </summary>
        /// <remarks>value properties are defaulted making them unreliable after rollback</remarks>
        /// <returns></returns>
        public bool Read()
        {
            bool readable = false;
            Value = default;

            if (!IsSegmentFetchable())
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
        /// Skips all prolog, miscellaneous, End Element and Value tokens blindly except for:
        ///  - Element tokens where all child tokens i.e. elements and attributes are ignored
        ///  - Attribute tokens ignore the next Value token
        /// </summary>
        public void Skip()
        {
            // TODO: Reads based on the current token
        }

        /// <summary>
        /// Read document non-terminals in order:
        ///   document ::= prolog element miscellaneous
        /// </summary>
        /// <returns></returns>
        internal bool ReadDocument()
        {
            TokenPosition = _segmentPosition;
            EBNF.Document previousNonTerminal;
            int previousSegmentPosition;

            bool readable;
            do
            {
                previousNonTerminal = _documentNonTerminal;
                previousSegmentPosition = _segmentPosition;

                readable = _documentNonTerminal switch
                {
                    EBNF.Document.None or EBNF.Document.Prolog => ReadPrologNonTerminal(),
                    EBNF.Document.Element => ReadElementNonTerminal(),
                    EBNF.Document.Miscellaneous => ReadMiscellaneousNonTerminal(),
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
        /// Read in order non-terminals defining the prolog non-terminal:
        ///   prolog ::= declaration? miscellaneous* (document-type miscellaneous*)?
        /// 
        /// Peek if next non-terminal is an element when reading is false setting 
        /// the document non-terminal too element
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <returns></returns>
        internal bool ReadPrologNonTerminal()
        {
            bool readable;

            readable = ReadDeclaration() || ReadMiscellaneousNonTerminal() || (_currentTokenType != TokenType.DocumentType && ReadOpaqueValue("<!DOCTYPE"u8, ">"u8, TokenType.DocumentType));

            if (readable && _documentNonTerminal == EBNF.Document.None)
            {
                _documentNonTerminal = EBNF.Document.Prolog;
            }

            if (!readable && PeekElementStartingTag())
            {
                _documentNonTerminal = EBNF.Document.Element;
            }

            return readable;
        }

        /// <summary>
        /// XML declarations must start at the beginning of an XML document. This method asserts only
        /// against the declaration's starting and ending terminals ("&lt;?xml " and "?&gt;") - the
        /// real <c>XMLDecl</c> production's non-terminals in between (<c>VersionInfo</c>,
        /// <c>EncodingDecl</c>, <c>SDDecl</c>) move the segment position along as part of finding the
        /// ending terminal, but are skipped rather than separately read into their own token or value.
        /// A successful match sets the token type to <see cref="TokenType.Declaration"/> and the value
        /// to that whole opaque span; a failed match leaves both untouched.
        /// </summary>
        /// <returns></returns>
        internal bool ReadDeclaration()
        {
            bool readable = false;

            if (_documentNonTerminal == EBNF.Document.None)
            {
                readable = ReadOpaqueValue(EBNF.StartDeclarationTerminal, EBNF.StopDeclarationTerminal, TokenType.Declaration);
            }

            return readable;
        }

        /// <summary>
        /// Read element non-terminal:
        ///   element ::= Empty Element | Start Content End
        /// 
        /// where Start Content End is an element with content that can also
        /// be empty
        /// <returns></returns>
        internal bool ReadElementNonTerminal()
        {
            SkipSpacing();

            return ReadEmptyElementNonTerminal() || ReadContentElementNonTerminal();
        }

        internal bool ReadEmptyElementNonTerminal()
        {
            return false;
        }

        internal bool ReadContentElementNonTerminal()
        {
            return false;
        }

        internal bool ReadStartNonTerminal()
        {
            if (Utf8Reader.TryMatch(_segment[_segmentPosition..], EBNF.StopTerminal, out int lastMatchReadBytes))
            {
                if (Utf8Reader.TryMatch(_segment[_segmentPosition..], EBNF.StartTerminal, out int firstMatchReadBytes)) {
                    ReadMiscellaneousNonTerminal();
                    // TODO: Complex content
                } else
                {
                    // TODO: Simple content
                }
            }

            return false;
        }

        /// <summary>
        /// Read end non-terminal:
        ///   end ::= '</' Name Spacing? '>'
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        internal bool ReadEndingNonTerminal()
        {
            if (Utf8Reader.TryMatch(_segment[_segmentPosition..], EBNF.EndTerminal, out int endTerminalBytes))
            {
                _segmentPosition += endTerminalBytes; // advance past '</'

                if (!ReadName())
                {
                    // document malformed when '</' without expected Name non-terminal
                    throw new InvalidOperationException(); // TODO: formatting
                }

                if (!_elementNameStack.TryPop(Value))
                {
                    // document malformed when read name non-terminal does not equal the name of the current element
                    throw new InvalidOperationException(); // TODO: formatting
                }

                SkipSpacing();

                if (Utf8Reader.TryMatch(_segment[_segmentPosition..], EBNF.StopTerminal, out int stopTerminalBytes))
                {
                    _segmentPosition += stopTerminalBytes; // advance past '>'
                }
                else
                {
                    // document malformed when '</' Name without expected '>' stop terminal
                    throw new InvalidOperationException(); // TODO: formatting
                }

                _previousTokenType = _currentTokenType;
                _currentTokenType = TokenType.ElementEnd;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Given an already-read Element (start tag) token, read whatever's legal right after
        /// its Name: an empty element's own closing '/&gt;', an attribute name, or '&gt;'
        /// starting its content.
        /// </summary>
        /// <returns></returns>
        internal bool ReadElement()
        {
            SkipSpacing();

            if (Utf8Reader.TryMatch(_segment[_segmentPosition..], EBNF.EmptyTerminal, out int emptyMatchReadBytes))
            {
                _segmentPosition += emptyMatchReadBytes;

                // No name to read here - the caller is closing exactly the element it just
                // opened, so ElementNameStack.Pop() (not TryPop) is the right tool: there's
                // nothing to compare against, only something to unconditionally close.
                int poppedNameLength = _elementNameStack.Pop(_poppedNameBuffer);
                Value = ((ReadOnlySpan<byte>)_poppedNameBuffer)[..poppedNameLength];

                _previousTokenType = _currentTokenType;
                _currentTokenType = TokenType.ElementEnd;

                return true;
            } else if (Utf8Reader.TryMatch(_segment[_segmentPosition..], EBNF.StopTerminal, out int lastMatchReadBytes))
            {
                if (Utf8Reader.TryMatch(_segment[_segmentPosition..], EBNF.StartTerminal, out int firstMatchReadBytes)) {
                    ReadMiscellaneousNonTerminal();
                    // TODO: Complex content
                } else
                {
                    // TODO: Simple content
                }
            }

            // TODO: Peek spacing with following name character then skip spacing, read name to value and set token == Attribute

            // TODO: Peek '>' then read content either complex or simple
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        internal bool ReadAttribute()
        {
            // TODO: Peek spacing then '=', skip spacing + '=' + trailing spacing then read opaque string between '"' to value

            return false;
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
        internal bool ReadMiscellaneousNonTerminal()
        {
            SkipSpacing();

            return ReadOpaqueValue("<!--"u8, "-->"u8, TokenType.Comment) ||
                   ReadOpaqueValue("<?"u8, "?>"u8, TokenType.ProcessInstruction);
        }

        /// <summary>
        /// Skip every contiguous run of whitespace starting at the current segment position -
        /// the XML <c>S</c> production ("one or more" space characters) is always a whole run,
        /// never a single character in isolation, so the single-segment scan step
        /// (<see cref="SkipSpace"/>) exists only in service of this method, never called on its
        /// own. It's a private instance method rather than a true local function only because
        /// C# doesn't allow a local function inside a `ref struct`'s method to touch the
        /// struct's own instance fields (`this` can't be implicitly captured the way a class
        /// allows) - the intent is the same either way. Drains across as many
        /// <see cref="SkipSpace"/> calls as it takes to exhaust the run (e.g. one that continues
        /// past the end of the current segment). Never produces a token - it only advances the
        /// segment position (and line number/position) past whatever whitespace is available.
        /// Returns whether any whitespace was skipped at all across the whole drain - not just
        /// whatever the final, naturally-failing call in the loop happened to return.
        /// </summary>
        /// <returns></returns>
        internal bool SkipSpacing()
        {
            bool skippedAny = false;

            while (SkipSpace())
            {
                skippedAny = true;
            }

            return skippedAny;
        }

        /// <summary>
        /// Skip a single contiguous run of whitespace bounded by the current segment - a run
        /// continuing into the next fetched segment needs another call from
        /// <see cref="SkipSpacing"/>'s own loop. Returns false, not an error, when there's no
        /// whitespace to consume right now. Exists only to serve <see cref="SkipSpacing"/> - see
        /// its summary for why this isn't a local function instead.
        /// </summary>
        /// <returns></returns>
        private bool SkipSpace()
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
        /// Read a starting element tag's Name - the caller has already matched and consumed the
        /// '<' itself (<see cref="ReadElementNonTerminal"/>), so <see cref="_segmentPosition"/> already sits
        /// on the Name's first byte. Delegates the actual character scan to <see cref="ReadName"/>,
        /// which is deliberately unaware of elements at all - the exact same scan will serve
        /// attribute names later (#25), the only difference being which <see cref="TokenType"/>
        /// the caller stamps once it succeeds. Pushes the Name onto <see cref="_elementNameStack"/>
        /// unconditionally - every element, leaf or not, is tracked there now (see
        /// <see cref="ReaderState._elementNameStack"/> for why the earlier single-slot fast path
        /// was dropped), so an ending tag later has exactly one place to check against.
        /// </summary>
        /// <returns></returns>
        internal bool ReadElementName()
        {
            if (!ReadName())
            {
                return false;
            }

            _elementNameStack.Push(Value);

            _previousTokenType = _currentTokenType;
            _currentTokenType = TokenType.Element;

            return true;
        }

        /// <summary>
        /// Read a Name (<c>NameStartChar (NameChar)*</c>) starting exactly at
        /// <see cref="_segmentPosition"/> - callers are responsible for having already
        /// positioned there (past whatever delimiter applies to them: '<' for an element,
        /// nothing at all for an attribute), since an attribute name has no leading delimiter
        /// of its own to skip. Sets <see cref="Value"/> and advances
        /// <see cref="_segmentPosition"/> past the Name on success; returns <see langword="false"/>
        /// when there's no valid Name at all (nothing there, or the first byte isn't a
        /// NameStartChar) without touching either - single/multiple segment aware, matching
        /// <see cref="ReadOpaqueValue"/>'s own dispatch shape.
        /// </summary>
        /// <returns></returns>
        private bool ReadName() => _isMultipleSegments
            ? ReadMultipleSegmentName()
            : ReadSingleSegmentName();

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
        #endregion

        #region peek
        /// <summary>
        /// Peek whether an element's starting tag begins here - a '<' character then a valid
        /// name-starting character - without advancing the reader at all.
        /// </summary>
        /// <returns></returns>
        private bool PeekElementStartingTag()
        {
            if (_segmentPosition >= _segment.Length || _segment[_segmentPosition] != EBNF.StartTerminal[0])
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