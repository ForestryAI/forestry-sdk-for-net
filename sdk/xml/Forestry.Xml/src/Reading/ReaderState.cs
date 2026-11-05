namespace Forestry.Xml.Reading
{
    /// <summary>
    /// The reader state is meant to live over async || sync bounderies to 
    /// reconstruct a reader with debuging, assertions and options fields.
    /// </summary>
    public readonly struct ReaderState
    {
        internal readonly long _lineNumber;

        internal readonly long _linePosition;

        internal readonly bool _documentType;
        
        internal readonly TokenType _currentTokenType;

        internal readonly TokenType _previousTokenType;

        internal readonly ElementStack _elementStack;
        
        internal readonly ReaderOptions _readerOptions;

        internal readonly bool _hasSkippedSpacingInStartTag;

        internal readonly bool _hasSkippedAttributeDelimiter;
        
        /// <summary>
        /// Reader state at the start of a document, with optional reader options
        /// </summary>
        /// <param name="readerOptions"></param>
        public ReaderState(ReaderOptions readerOptions = default)
        {
            _lineNumber = default;
            _linePosition = default;
            _documentType = false;
            _hasSkippedSpacingInStartTag = false;
            _hasSkippedAttributeDelimiter = false;

            _currentTokenType = default;
            _previousTokenType = default;

            _elementStack = default;

            _readerOptions = readerOptions;
        }

        /// <summary>
        /// Explicit bare-constructor with a default <see cref="ReaderOptions"/>
        /// </summary>
        public ReaderState() : this(default) {}

        internal ReaderState(
            long lineNumber,
            long linePosition,
            bool documentType,
            bool hasSkippedSpacingInStartTag,
            bool hasSkippedAttributeDelimiter,
            TokenType currentTokenType,
            TokenType previousTokenType,
            ElementStack elementStack,
            ReaderOptions readerOptions
        )
        {
            _lineNumber = lineNumber;
            _linePosition = linePosition;
            _documentType = documentType;

            _hasSkippedSpacingInStartTag = hasSkippedSpacingInStartTag;
            _hasSkippedAttributeDelimiter = hasSkippedAttributeDelimiter;

            _currentTokenType = currentTokenType;
            _previousTokenType = previousTokenType;

            _elementStack = elementStack;

            _readerOptions = readerOptions;
        }

        /// <summary>
        /// Any deviations from strict adherence to the XML specification
        /// </summary>
        public ReaderOptions ReaderOptions => _readerOptions;
    }
}