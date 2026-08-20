using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Forestry.Deserialize.Xml;
using Forestry.Deserialize.Xml.Deserializers;

namespace Forestry.Deserialize.Xml.Reading
{
    internal ref partial struct Utf8XmlReader
    {
        #region segments
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

        /// <summary>
        /// Position in the document
        /// </summary>
        private int _documentPosition;  // TODO: Explain usage

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

            _elementName = [];
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
        /// When segment position within the segment length
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsSegmentReadable()
        {
            if (_segmentPosition >= (uint)_segment.Length)
            {
                if (_isFinalSegment)
                {
                    // TODO: Maybe validation
                }

                if (!_isSequence)
                {
                    return false;
                }

                if (!ReadNextSegment())
                {
                    if (_isFinalSegment)
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
        #endregion

        #region read 
        /// <summary>
        /// Read token
        /// </summary>
        /// <returns></returns>
        public bool Read()
        {
            bool readable = false;
            Value = default;

            if (!IsSegmentReadable())
            {
                // Not invalid token just not enough read
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

        internal bool ReadDocument()
        {
            return false;
        }
        #endregion

        /// <summary>
        /// Skip whitespace
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool SkipWhitespace(ReadOnlySpan<byte> value)
        {
            return false;
        }
    }
}