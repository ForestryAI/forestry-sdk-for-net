using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Xml.Reading
{
    /// <summary>
    /// The reader state is meant to live over async || sync bounderies to 
    /// reconstruct a reader with debuging, assertions and options fields.
    /// </summary>
    public readonly struct ReaderState: IReaderState<ReaderState>
    {
        internal readonly long _lineNumber;

        internal readonly long _linePosition;
        
        internal readonly TokenType _currentTokenType;

        internal readonly TokenType _previousTokenType;

        internal readonly ElementStack _elementStack;
        
        internal readonly ReaderOptions _readerOptions;

        public ReaderState(ReaderOptions readerOptions = default)
        {
            _lineNumber = default;
            _linePosition = default;

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
            TokenType currentTokenType,
            TokenType previousTokenType,
            ElementStack elementStack,
            ReaderOptions readerOptions
        )
        {
            _lineNumber = lineNumber;
            _linePosition = linePosition;

            _currentTokenType = currentTokenType;
            _previousTokenType = previousTokenType;

            _elementStack = elementStack;

            _readerOptions = readerOptions;
        }

        // Explicit interface implementation: IReaderState<TState>'s members are `internal`, and an
        // internal interface member can only ever be satisfied by a `public` implicitly-implementing
        // member - even across an InternalsVisibleTo friend assembly. Forwarding explicitly here
        // keeps the fields above `internal`/`_`-prefixed/readonly, matching every other field on
        // this type, instead of promoting four of them to `public` just to satisfy the interface.
        long IReaderState<ReaderState>._lineNumber => _lineNumber;

        long IReaderState<ReaderState>._linePosition => _linePosition;

        /// <summary>
        /// Any deviations from strict adherence to the XML specification
        /// </summary>
        public ReaderOptions ReaderOptions => _readerOptions;
    }
}