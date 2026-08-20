using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Xml.Reading
{
    /// <summary>
    /// The reader state is meant to live over async || sync bounderies to 
    /// reconstruct a reader with debuging, assertions and options fields.
    /// </summary>
    public readonly struct ReaderState: IReaderState<ReaderState>
    {
        #region debug
        internal readonly long _lineNumber;

        internal readonly long _linePosition;
        #endregion

        #region assertions
        internal readonly EBNF.Document _documentNonTerminal;
        
        internal readonly TokenType _currentTokenType;

        internal readonly TokenType _previousTokenType;

        internal readonly ulong[] _elementName;
        
        internal readonly ElementNameStack _elementNameStack;
        #endregion

        #region options
        internal readonly ReaderOptions _readerOptions;
        #endregion

        public ReaderState(ReaderOptions readerOptions = default)
        {
            _lineNumber = default;
            _linePosition = default;

            _documentNonTerminal = default;
            _currentTokenType = default;
            _previousTokenType = default;
            _elementName = [];
            _elementNameStack = default;

            _readerOptions = readerOptions;
        }

        internal ReaderState(
            long lineNumber,
            long linePosition,
            EBNF.Document documentNonTerminal,
            TokenType currentTokenType,
            TokenType previousTokenType,
            ulong[] elementName,
            ElementNameStack elementNameStack,
            ReaderOptions readerOptions
        )
        {
            _lineNumber = lineNumber;
            _linePosition = linePosition;

            _documentNonTerminal = documentNonTerminal;
            _currentTokenType = currentTokenType;
            _previousTokenType = previousTokenType;
            _elementName = elementName;
            _elementNameStack = elementNameStack;

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