using System.Buffers;
using Forestry.Deserialize.Xml;
using Forestry.Deserialize.Xml.Deserializers;

namespace Forestry.Deserialize.Xml.Reading
{
    internal ref partial struct Utf8XmlReader
    {
        private ReadOnlySpan<byte> _buffer;

        private long _lineNumber;

        private long _lineNumberPosition;

        private bool _isObject;

        private bool _isNotPrimitive;

        private TokenType _tokenType;

        private TokenType _lastTokenType;

        private ReaderOptions _readerOptions;

        /// <summary>
        /// Is buffering completed
        /// </summary>
        private bool _isBufferingCompleted;

        /// <summary>
        /// Position in buffer
        /// </summary>
        private int _bufferPosition;

        /// <summary>
        /// When has multiple segements then true otherwise false if a single segment when sequencing
        /// </summary>
        private bool _isMultipleSegments;

        /// <summary>
        /// When not sequencing and when sequencing the last segment
        /// </summary>
        private bool _isLastSegment;

        private readonly bool IsSequencingCompleted => _isBufferingCompleted && (!_isMultipleSegments || _isLastSegment);

        /// <summary>
        /// Reading bytes without buffering i.e. meant for in-memory smaller XML
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
        /// Reading bytes with or without buffering
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

            _lineNumber = readerState._lineNumber;
            _lineNumberPosition = readerState._lineNumberPosition;

            _isObject = readerState._isObject;
            _isNotPrimitive = readerState._isNotPrimitive;

            _tokenType = readerState._tokenType;
            _lastTokenType = readerState._lastTokenType;

            _readerOptions = readerState.ReaderOptions;

            if (_readerOptions.MaxDepth == 0)
            {
                _readerOptions.MaxDepth = ReaderOptions.DefaultMaxDepth;
            }

            IsWithSequencing = false;
            Value = [];
            ValueSequence = ReadOnlySequence<byte>.Empty;

            _bufferPosition = 0;
            _isMultipleSegments = false;
            _isLastSegment = isBufferingCompleted;
        }

        #region Shape
        public readonly TokenType TokenType => _tokenType;

        /// <summary>
        /// When reading is against sequencing 
        /// </summary>
        public bool IsWithSequencing { get; private set; }

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
        /// Read
        /// </summary>
        /// <returns></returns>
        public bool Read()
        {
            bool readable = IsWithSequencing ? TryReadWithSequencing() : TryReadWithoutSequencing();
            if (!readable)
            {
                if (_isBufferingCompleted && _tokenType is TokenType.None)
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
        /// Try read when not sequencing
        /// </summary>
        /// <returns></returns>
        private bool TryReadWithoutSequencing()
        {
            bool readable = false;
            Value = default;

            if (!IsBufferPositionReadable())
            {
                goto ReadingCompleted;
            }
            
            if (_tokenType == TokenType.None)
            {
                goto ReadDocument;
            }

            byte value = _buffer[_bufferPosition];
            
            if (_tokenType == TokenType.StartingTag)
            {
                // TODO: Read element

                readable = true;
                goto ReadingCompleted;
            } 
            else
            {
                // TODO: when nothing matches
                goto ReadingCompleted;
            }

            ReadingCompleted:
                return readable;

            ReadDocument:
                readable = ReadDocument(_buffer);
                goto ReadingCompleted;
        }

        /// <summary>
        /// Try read when sequencing
        /// </summary>
        /// <returns></returns>
        private bool TryReadWithSequencing()
        {
            bool readable = false;

            return readable;
        }

        /// <summary>
        /// When buffer position less than the buffer length then 
        /// the buffer is readable
        /// </summary>
        /// <returns></returns>
        private bool IsBufferPositionReadable()
        {
            if (_bufferPosition >= (uint)_buffer.Length)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Read document defined as: prolog element miscellaneous*
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <see cref="https://www.w3.org/TR/REC-xml/">Using EBNF concatenation-by-juxtaposition</see>
        private bool ReadDocument(ReadOnlySpan<byte> value)
        {            
            return SkipProlog(value) && ReadElement(value) && SkipMiscellaneous(value);
        }

        /// <summary>
        /// Skip prolog defined as: declaration? miscellaneous* (document-type miscellaneous*)?
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool SkipProlog(ReadOnlySpan<byte> value)
        {
            return false;
        }

        /// <summary>
        /// Skip declaration
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool SkipDeclaration(ReadOnlySpan<byte> value)
        {
            return false;
        }

        /// <summary>
        /// Read element
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool ReadElement(ReadOnlySpan<byte> value)
        {
            return false;
        }

        /// <summary>
        /// Skip miscellaneous: comment | processing-instruction | whitespace
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool SkipMiscellaneous(ReadOnlySpan<byte> value)
        {
            return false;
        }

        /// <summary>
        /// Skip comment
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool SkipComment(ReadOnlySpan<byte> value)
        {
            return false;
        }

        /// <summary>
        /// Skip process instruction
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool SkipProcessInstruction(ReadOnlySpan<byte> value)
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