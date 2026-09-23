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
        #region constructor
        /// <summary>
        /// Reader sourcing a byte span where reading is implicitly completed 
        /// and the reader state is created from optional reader options
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="readerOptions"></param>
        public Utf8XmlReader(
            ReadOnlySpan<byte> segment,
            ReaderOptions readerOptions = default
        ): this(segment, isReadingCompleted: true, new ReaderState(readerOptions)) {}

        /// <summary>
        /// Reader sourcing a byte span where the reader completed flag and reader 
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
        /// Reader sourcing a byte sequence where reading is implicitly completed 
        /// and the reader state is created from optional reader options
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="readerOptions"></param>
        public Utf8XmlReader(
            ReadOnlySequence<byte> segments,
            ReaderOptions readerOptions = default
        ): this(segments, isReadingCompleted: true, new ReaderState(readerOptions)) {}

        /// <summary>
        /// Reader sourcing a byte sequence where the reader completed flag and reader 
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
        /// Position in the sequence starting at the first segment
        /// </summary>
        private int _sequencePosition;

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
        public readonly long Position => _sequencePosition + _segmentPosition;

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
        /// Scratch buffer for starting terminals that is transient i.e. rewritten on 
        /// rollbacks
        /// </summary>
        private readonly byte[] _startingTerminals = new byte[9];
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

            // Skip miscellaneous spacing
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
                    goto Completed;
                }
            }

            // Content ready
            ContentReady();

            // Read starting terminal, value and set token
            return ReadValue();

            Completed:
                return advancement;
        }

        /// <summary>
        /// Skip the next token only when the current segment is the last
        /// </summary>
        public void Skip()
        {
            
        }

        /// <summary>
        /// Try skipping the next token otherwise rollback despite an unreliable value
        /// </summary>
        /// <returns></returns>
        public bool TrySkip()
        {
            return false;
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
        /// When first character == '>' then determine if the element non-terminal 
        /// has a content non-terminal after the start tag
        /// </summary>
        /// <returns></returns>
        internal void ContentReady()
        {
            byte character = _segment[_segmentPosition];

           
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private bool ReadValue()
        {
            return false;
        }

        /// <summary>
        /// Read starting terminal into a transient scratch pad memory
        /// </summary>
        /// <returns></returns>
        private bool ReadStartingTerminal()
        {
            _startingTerminals.AsSpan().Clear();  // transient
            byte character = _segment[_segmentPosition];

            if (character == EBNF.StartTagStartingTerminal)
            {
                // miscellaneous non-terminals i.e. anywhere in markup
                // TODO: Must have 2 or 3 characters when processing instruction or comment

                if (!_elementStack.RootElement)
                {
                    // prolog non-terminals or first start tag i.e. root element non-terminal
                    if (_currentTokenType == TokenType.None) // TODO: BOM + add declaration starting tag check
                    {
                        // TODO: Must have 4 characters == declaration non-terminal starting tag
                    }

                    // TODO: document type check
                } else
                {
                    // start tag i.e. child element non-terminal when next terminal not '/' otherwise end tag
                }

                return true;
            }
            

            if (_elementStack.Depth != 0 && _elementStack.ContentReady && character == EBNF.Equal)
            {
                // value non-terminal (attribute)
                return true;
            }

            if (_elementStack.Depth != 0 && _elementStack.ContentReady && EBNF.IsNameStartingCharacter(character))
            {
                // attribute non-terminal
                return true;
            }

            if (_elementStack.Depth != 0 && _currentTokenType == TokenType.ElementEnd && _elementStack.ContentReady && EBNF.IsCharacterData(character))
            {
                // value non-terminal (character data)
                return true;
            }

            if (_elementStack.Depth != 0 && _elementStack.ContentReady && character == EBNF.Slash)
            {
                // value (empty element non-terminal)    
                return true;
            }

            if (_elementStack.Depth != 0 && !_elementStack.ContentReady && character == EBNF.StartTagEndingTerminal)
            {
                // end element (empty element non-terminal)
                return true;
            }

            return false;
        }
    }
}