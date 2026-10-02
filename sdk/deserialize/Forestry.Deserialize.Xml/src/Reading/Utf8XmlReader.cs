using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Forestry.Deserialize.Xml.Reading
{
    /// <summary>
    /// Public constructors, properties and methods
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
        /// </remarks>
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
            
            // Break advancement fast when segment is drained
            if (_isMultipleSegments ? IsMultipleSegmentDrained() : IsSingleSegmentDrained())
            {
                goto Completed;
            }

            // Skip spacing
            if (SkipSpacing())
            {
                goto Completed;
            }

            // Content ready
            ContentReady();

            // Content ready may skip '>' (S3 or S5) draining the segment
            if (_isMultipleSegments ? IsMultipleSegmentDrained() : IsSingleSegmentDrained())
            {
                goto Completed;
            }

            // Read value
            return ReadValue();

            Completed:
                return advancement;
        }

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
                Throwing.ThrowXmlException(ref this, Throwing.ExceptionType.WhenDocumentHasNoRootElement);
            }

            if (Depth != 0)
            {
                Throwing.ThrowXmlException(ref this, Throwing.ExceptionType.WhenDocumentHasElementNotEnded);
            }

            // TODO: Reader option policies when comments or other accepted prolog + miscellaneous non-terminals

            return true;
        }

        /// <summary>
        /// Skip spacing
        /// </summary>
        /// <returns>true when skipping drained the segment(s), halting the read</returns>
        internal bool SkipSpacing()
        {
            // TODO: fire depending on the current token also with line feeds, carriage returns and tabs

            byte character = _segment[_segmentPosition];
            if (character == EBNF.Space) 
            {
                if (_isMultipleSegments)
                {
                    SkipMultipleSpacing();
                } else
                {
                    SkipSingleSpacing();
                }

                if (_isMultipleSegments ? IsMultipleSegmentDrained() : IsSingleSegmentDrained())
                {
                    return true;
                }
            }

            return false;
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
                    goto IgnoreMalformed; // S4
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
                _elementStack.NegateContentReady();
                return;

            IgnoreMalformed:
                return;

            IgnoreWellformed:
                return;
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

                    if (!isPrefix)
                    {
                        return true; // S1 or S2 ignoring malformed markup
                    }
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
        /// Read values
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
                    throw new NotImplementedException();
                case NonTerminal.EndTag:
                    throw new NotImplementedException();
                case NonTerminal.Attribute:
                    throw new NotImplementedException();
                case NonTerminal.AttributeValue:
                    throw new NotImplementedException();
                case NonTerminal.CharacterData:
                    throw new NotImplementedException();
                case NonTerminal.None:
                    throw new NotImplementedException();
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
                Throwing.ThrowXmlException(ref this, Throwing.ExceptionType.WhenDeclarationNotFirst);
            }

            return _isMultipleSegments ?
                ReadMultipleOpaqueValue(EBNF.DeclarationStartingTerminal, EBNF.DeclarationEndingTerminal, TokenType.Declaration) : 
                ReadSingleOpaqueValue(EBNF.DeclarationStartingTerminal, EBNF.DeclarationEndingTerminal, TokenType.Declaration);
        }

        /// <summary>
        /// Read opaque value from document type non-terminal
        /// </summary>
        /// <returns></returns>
        internal bool ReadDocumentType()
        {
            if (_documentType)
            {
                Throwing.ThrowXmlException(ref this, Throwing.ExceptionType.WhenDocumentTypeRepeated);
            }
            
            bool advancement =  _isMultipleSegments ?
                ReadMultipleOpaqueValue(EBNF.DocumentTypeStartingTerminal, EBNF.DocumentTypeEndingTerminal, TokenType.DocumentType) : 
                ReadSingleOpaqueValue(EBNF.DocumentTypeStartingTerminal, EBNF.DocumentTypeEndingTerminal, TokenType.DocumentType);

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
                ReadMultipleOpaqueValue(EBNF.CommentStartingTerminal, EBNF.CommentEndingTerminal, TokenType.Comment) : 
                ReadSingleOpaqueValue(EBNF.CommentStartingTerminal, EBNF.CommentEndingTerminal, TokenType.Comment);

        /// <summary>
        /// Read opaque value from processing instruction non-terminal
        /// </summary>
        /// <returns></returns>
        internal bool ReadProcessingInstruction()
        {
            bool advancement = _isMultipleSegments ?
                ReadMultipleOpaqueValue(EBNF.ProcessingInstructionStartingTerminal, EBNF.ProcessingInstructionEndingTerminal, TokenType.ProcessInstruction) : 
                ReadSingleOpaqueValue(EBNF.ProcessingInstructionStartingTerminal, EBNF.ProcessingInstructionEndingTerminal, TokenType.ProcessInstruction);

            if (advancement)
            {
                if (HasValueSequence ? IsMultipleProcessingInstructionMalformed() : IsSingleProcessingInstructionMalformed())
                {
                    Throwing.ThrowXmlException(ref this, Throwing.ExceptionType.WhenProcessingInstructionTargetMalformed);
                }
            }

            return advancement;
        }

        /// <summary>
        /// Terminal declaration either well-formed, malformed or continue 
        /// where continue means more characters are required to evaluate 
        /// if the terminal is well-formed
        /// </summary>
        private enum TerminalDeclaration : byte
        {
            /// <summary>
            /// More characters are needed, e.g. the next segment of a value sequence
            /// </summary>
            Continue,
            WellFormed,
            Malformed,
        }

        /// <summary>
        /// Scans a PI target (S5 + S6) in a single loop, one character at a time, carrying
        /// its state across the segments of a value sequence
        /// </summary>
        /// <remarks>
        /// S5: the target is a <c>Name</c> (a <c>NameStartChar</c> then zero or more
        /// <c>NameChar</c>) followed by spacing or the ending terminal <c>?&gt;</c>.  S6: the
        /// target does not equal <c>xml</c> in any case, tracked while scanning so no second
        /// pass is needed.  <c>?</c> is never a <c>NameChar</c>, so stopping at the first
        /// <c>?</c> never cuts a well-formed target short.
        /// </remarks>
        private struct ProcessingInstructionTarget
        {
            /// <summary>
            /// Characters scanned in the target
            /// </summary>
            private int _length;

            /// <summary>
            /// The target scanned so far equals the start of <c>xml</c> in any case
            /// </summary>
            private bool _isXml;

            /// <summary>
            /// The target ended on <c>?</c> so the next character must be <c>&gt;</c>
            /// </summary>
            private bool _isEndingTerminal;

            /// <summary>
            /// Evaluate the character terminals after the starting terminal <c>&lt;?</c>
            /// </summary>
            /// <param name="characters"></param>
            /// <returns></returns>
            internal TerminalDeclaration EvaluateTerminalDeclaration(ReadOnlySpan<byte> characters)
            {
                foreach (byte character in characters)
                {
                    if (_isEndingTerminal)
                    {
                        return character == EBNF.GreaterThan ? TerminalDeclaration.WellFormed : TerminalDeclaration.Malformed;  // S5
                    }

                    if (character is EBNF.Space or EBNF.Tab or EBNF.CarriageReturn or EBNF.LineFeed or EBNF.QuestionMark)
                    {
                        if (_length == 0 || (_isXml && _length == 3))
                        {
                            return TerminalDeclaration.Malformed;  // S5 empty target, S6
                        }

                        if (character != EBNF.QuestionMark)
                        {
                            return TerminalDeclaration.WellFormed;
                        }

                        _isEndingTerminal = true;
                        continue;
                    }

                    if (_length == 0 ? !EBNF.IsNameStartingCharacter(character) : !EBNF.IsNameCharacter(character))
                    {
                        return TerminalDeclaration.Malformed;  // S5
                    }

                    // ASCII letters only differ by the 0x20 bit in case, and only 'X'/'x',
                    // 'M'/'m' and 'L'/'l' match "xml" with that bit set
                    _isXml = _length < 3
                        && (_length == 0 || _isXml)
                        && (byte)(character | 0x20) == "xml"u8[_length];
                    _length++;
                }

                return TerminalDeclaration.Continue;
            }
        }
    }
}