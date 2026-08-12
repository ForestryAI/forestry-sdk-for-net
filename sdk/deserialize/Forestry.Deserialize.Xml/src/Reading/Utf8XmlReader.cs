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
        /// Reads next token
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
        /// Skip current token
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

            byte value = _buffer[_bufferPosition];

            // TODO: Spaces

            if (_tokenType == TokenType.None)
            {
                goto ReadFirstToken;
            }

            // TODO: Comments

            // TODO: Declaration
            
            if (_tokenType == TokenType.StartingTag)
            {
                // TODO: value within XML element + attribute valid characters

                // TODO: when space then attribute name
                // TODO: when equals then attribute value

                goto ReadingCompleted;
            } else
            {
                // TODO: when nothing matches
                goto ReadingCompleted;
            }

            ReadingCompleted:
                return readable;

            ReadFirstToken:
                readable = ReadFirstToken(value);
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
        /// Read first token
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool ReadFirstToken(byte value)
        {
            // TODO: More than one value is needed to determine the first token
            if (value == Constants.LessThan)
            {
            } else if (value == Constants.Slash)
            {
            }


            return true;
        }
        #endregion
    }
}