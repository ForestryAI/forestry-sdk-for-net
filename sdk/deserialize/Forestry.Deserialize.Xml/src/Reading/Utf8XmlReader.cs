using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Forestry.Deserialize.Xml;
using Forestry.Deserialize.Xml.Deserializers;

namespace Forestry.Deserialize.Xml.Reading
{
    internal ref partial struct Utf8XmlReader
    {
        /// <summary>
        /// Internal buffer deriving from a span (i.e. single segment) or 
        /// sequencing which can have a single or multiple segments
        /// </summary>
        private ReadOnlySpan<byte> _buffer;

        /// <summary>
        /// Line number i.e. top to bottom
        /// </summary>
        private long _lineNumber;

        /// <summary>
        /// Line position i.e. left to right
        /// </summary>
        private long _linePosition;

        /// <summary>
        /// When reading is inside markup
        /// </summary>
        private bool _isElement;

        /// <summary>
        /// When reading is inside prolog i.e. the document start
        /// </summary>
        private bool _isProlog;

        /// <summary>
        /// Current XML Token
        /// </summary>
        private TokenType _currentTokenType;

        /// <summary>
        /// Previous XML Token
        /// </summary>
        private TokenType _previousTokenType;

        /// <summary>
        /// Reader options
        /// </summary>
        private ReaderOptions _readerOptions;

        /// <summary>
        /// Is buffering completed
        /// </summary>
        private bool _isBufferingCompleted;

        /// <summary>
        /// Position in the internal buffer
        /// </summary>
        private int _bufferPosition;

        /// <summary>
        /// Position in the document
        /// </summary>
        private int _documentPosition;

        /// <summary>
        /// Only true when the internal buffer derives from sequencing with multiple segments i.e. 
        /// false if contains only a single segment or if the buffer dervires from a span
        /// </summary>
        private bool _isMultipleSegments;

        /// <summary>
        /// When at the last segment in the document
        /// </summary>
        private bool _isLastSegment;

        /// <summary>
        /// Element name when the element contains a value
        /// </summary>
        private ulong[] _elementName;

        /// <summary>
        /// Element names when the element contains a child element
        /// </summary>
        private ElementNameStack _elementNameStack;

        /// <summary>
        /// When buffering completed and is either the last or not multiple segments
        /// </summary>
        private readonly bool IsReadingCompleted => _isBufferingCompleted && (!_isMultipleSegments || _isLastSegment);

        /// <summary>
        /// When a sequence backs the internal buffer
        /// </summary>
        private readonly bool _isSequencing;

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

        /// <summary>
        /// Reading bytes from a single span i.e. meant for in-memory smaller XML
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="readerOptions"></param>
        public Utf8XmlReader(
            ReadOnlySpan<byte> bytes,
            ReaderOptions readerOptions = default
        ): this(bytes, isBufferingCompleted: true, new ReaderState(readerOptions))
        {
            
        }

        /// <summary>
        /// Reading bytes from a single span
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="isBufferingCompleted"></param>
        /// <param name="readerState"></param>
        public Utf8XmlReader(
            ReadOnlySpan<byte> bytes,
            bool isBufferingCompleted,
            ReaderState readerState
        ) {
            _buffer = bytes;
            _isBufferingCompleted = isBufferingCompleted;
            _isLastSegment = isBufferingCompleted;

            _bufferPosition = 0;
            _documentPosition = 0;

            _lineNumber = readerState._lineNumber;
            _linePosition = readerState._linePosition;

            _isElement = readerState._isElement;
            _isProlog = readerState._isProlog;

            _currentTokenType = readerState._tokenType;
            _previousTokenType = readerState._lastTokenType;

            _readerOptions = readerState.ReaderOptions;

            if (_readerOptions.MaxDepth == 0)
            {
                _readerOptions.MaxDepth = ReaderOptions.DefaultMaxDepth;
            }

            _elementName = [];
            _elementNameStack = readerState._elementNameStack;

            HasValueSequence = false;
            Value = [];
            ValueSequence = ReadOnlySequence<byte>.Empty;

            _isSequencing = false;
            _isMultipleSegments = false;

            _sequence = default;
            _currentSequencePosition = default;
            _nextSequencePosition = default;
        }

        /// <summary>
        /// Reading byte sequences i.e. when piping larger XML documents
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="readerOptions"></param>
        public Utf8XmlReader(
            ReadOnlySequence<byte> bytes,
            ReaderOptions readerOptions = default
        ): this(bytes, isBufferingCompleted: true, new ReaderState(readerOptions))
        {
            
        }

        /// <summary>
        /// Reading byte sequences i.e. when piping larger XML documents
        /// </summary>
        /// <param name="bytes"></param>
        public Utf8XmlReader(
            ReadOnlySequence<byte> bytes,
            bool isBufferingCompleted,
            ReaderState readerState
        ): this(bytes.FirstSpan, isBufferingCompleted, readerState)
        {
            _isSequencing = true;
            _sequence = bytes;

            _currentSequencePosition = bytes.Start;

            if (bytes.IsSingleSegment)
            {
                _isMultipleSegments = false;
                _nextSequencePosition = default;
            } else
            {
                _nextSequencePosition = _currentSequencePosition;

                bool emptyFirstSegment = _buffer.Length == 0;
                if (emptyFirstSegment)
                {
                    SequencePosition referenceSequencePosition = _nextSequencePosition;
                    while (bytes.TryGet(ref _nextSequencePosition, out ReadOnlyMemory<byte> memory, advance: true))
                    {
                        _currentSequencePosition = referenceSequencePosition;
                        if (memory.Length != 0)
                        {
                            _buffer = memory.Span;
                            break;
                        }
                        referenceSequencePosition = _nextSequencePosition;
                    }
                }

                _isLastSegment = !bytes.TryGet(ref _nextSequencePosition, out _, advance: !emptyFirstSegment) && isBufferingCompleted; 

                Debug.Assert(!_nextSequencePosition.Equals(_currentSequencePosition));
                _isMultipleSegments = true;
            }
        }

        #region Shape
        public readonly TokenType TokenType => _currentTokenType;

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
        #endregion

        #region Reading 
        /// <summary>
        /// Read next token
        /// </summary>
        /// <returns></returns>
        public bool Read()
        {
            bool readable = TryRead();
            if (!readable)
            {
                if (_isBufferingCompleted && _currentTokenType is TokenType.None)
                {
                    throw new InvalidOperationException(); // TODO: Formatting
                }
            }

            return readable;
        }

        /// <summary>
        /// Skip
        /// </summary>
        public void Skip()
        {}

        /// <summary>
        /// Try read
        /// </summary>
        /// <returns></returns>
        private bool TryRead()
        {
            bool readable = false;
            Value = default;

            if (!IsBufferReadable())
            {
                // Not enough buffered data to make any progress right now - not "invalid,"
                // just "nothing more to look at yet." Nothing below has run, so there's
                // nothing to roll back: the caller refills and calls Read() again, and this
                // same check is where it picks back up.
                goto ReadingCompleted;
            }

            if (_currentTokenType == TokenType.None || _isProlog)
            {
                _isProlog = true;         
                goto ReadDocument;
            }

            if (_currentTokenType == TokenType.Element)
            {
                readable = true;
                goto ReadingCompleted;
            }
            else if (_currentTokenType == TokenType.Attribute)
            {
                goto ReadingCompleted;
            }
            else if (_currentTokenType == TokenType.Value)
            {

                goto ReadingCompleted;
            }
            else if (_currentTokenType == TokenType.ElementEnd)
            {
                goto ReadingCompleted;
            }
            else
            {
                goto ReadDocument;
            }

            ReadingCompleted:
                return readable;

            ReadDocument:
                readable = ReadDocument();
                goto ReadingCompleted;
        }

        /// <summary>
        /// When buffer position within the buffer
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsBufferReadable()
        {
            if (_bufferPosition >= (uint)_buffer.Length)
            {
                if (_isLastSegment)
                {
                    // TODO: Maybe validation
                }

                if (!_isSequencing)
                {
                    return false;
                }

                if (!ReadNextSegment())
                {
                    if (_isLastSegment)
                    {
                        // TODO: Maybe validation
                    }

                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Reads only non-empty segments into the internal buffer and reseting 
        /// the buffer position
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
                    _isLastSegment = true;

                    return false;
                }

                if (memory.Length != 0)
                {
                    break;
                }

                _currentSequencePosition = referenceSequencePosition;
                Debug.Assert(!_isMultipleSegments || _currentSequencePosition.GetObject() is not null);
            }

            if (_isBufferingCompleted)
            {
                _isLastSegment = !_sequence.TryGet(ref _nextSequencePosition, out _, advance: false);
            }

            _buffer = memory.Span;

            _documentPosition += _bufferPosition;
            _bufferPosition = 0;

            return true;
        }

        /// <summary>
        /// Read document as EBNF defined: prolog element miscellaneous*
        /// </summary>
        /// <returns></returns>
        /// <see cref="https://www.w3.org/TR/REC-xml/">Using EBNF concatenation-by-juxtaposition</see>
        private bool ReadDocument()
        {
            bool readable = false;

            if (_isProlog)
            {
                readable = ReadProlog();
                goto ReadingCompleted;
            }

            if (_isProlog is false && _isElement)
            {
                
            }

            if (_isProlog is false && _isElement is false)
            {
                
            }

            ReadingCompleted:
                return readable;
        }

        /// <summary>
        /// Read prolog defined as: declaration? miscellaneous* (document-type miscellaneous*)?
        /// </summary>
        /// <returns></returns>
        private bool ReadProlog()
        {
            // TODO: When an element is found then set _isProlog to false and rollback
            return false;
        }

        /// <summary>
        /// Read declaration
        /// </summary>
        /// <returns></returns>
        private bool ReadDeclaration()
        {
            if (_isProlog && _currentTokenType == TokenType.None)
            {
                
            }
            
            throw new InvalidOperationException();  // TODO: Formated throwing if declartion not preceeded by None
        }

        /// <summary>
        /// Read element
        /// </summary>
        /// <returns></returns>
        private bool ReadElement()
        {
            return false;
        }

        private bool ReadElementEnd()
        {
            return false;
        }

        /// <summary>
        /// Read miscellaneous: comment | processing-instruction | whitespace
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool ReadMiscellaneous(ReadOnlySpan<byte> value)
        {
            return false;
        }

        /// <summary>
        /// Read comment
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool ReadComment(ReadOnlySpan<byte> value)
        {
            return false;
        }

        /// <summary>
        /// Read process instruction
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool ReadProcessInstruction(ReadOnlySpan<byte> value)
        {
            return false;
        }

        /// <summary>
        /// Skip whitespace
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool SkipWhitespace(ReadOnlySpan<byte> value)
        {
            return false;
        }
        #endregion
    }
}