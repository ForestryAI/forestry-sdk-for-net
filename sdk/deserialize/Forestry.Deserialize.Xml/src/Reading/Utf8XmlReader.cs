using System.Buffers;
using Forestry.Deserialize.Xml;

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

            IsSequencing = false;
            Value = [];
            ValueSequence = ReadOnlySequence<byte>.Empty;

        }

        #region Shape
        public readonly TokenType TokenType => _tokenType;

        public bool IsSequencing { get; private set; }

        public ReadOnlySequence<byte> ValueSequence { get; private set; }

        public ReadOnlySpan<byte> Value { get; private set; }
        #endregion

        #region Reading 
        /// <summary>
        /// Reads next element || attribute
        /// </summary>
        /// <returns></returns>
        public bool Read()
        {
            return false;
        }

        /// <summary>
        /// Skip current element || attribute
        /// </summary>
        public void Skip()
        {}
        #endregion

        internal ReadOnlySpan<byte> GetUnescapedValue()
        {
            ReadOnlySpan<byte> value = IsSequencing ? ValueSequence.ToArray() : Value;
            // TODO: When escaped convert

            return value;
        }
    }
}