using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Xml.Reading
{
    /// <summary>
    /// Unlike reader structs the reader state can live over async || sync bounderies
    /// </summary>
    public readonly struct ReaderState: IReaderState<ReaderState>
    {
        internal readonly long _lineNumber;

        internal readonly long _lineNumberPosition;

        internal readonly bool _isObject;

        internal readonly bool _isNotPrimitive;

        internal readonly TokenType _tokenType;

        internal readonly TokenType _lastTokenType;

        internal readonly ReaderOptions _readerOptions;

        public ReaderState(ReaderOptions readerOptions = default)
        {
            _lineNumber = default;
            _lineNumberPosition = default;
            _isObject = default;
            _isNotPrimitive = default;
            _tokenType = default;
            _lastTokenType = default;
            _readerOptions = readerOptions;
        }

        internal ReaderState(
            long lineNumber,
            long lineNumberPosition,
            bool isObject,
            bool isNotPrimitive,
            TokenType tokenType,
            TokenType lastTokenType,
            ReaderOptions readerOptions
        )
        {
            _lineNumber = lineNumber;
            _lineNumberPosition = lineNumberPosition;
            _isObject = isObject;
            _isNotPrimitive = isNotPrimitive;
            _tokenType = tokenType;
            _lastTokenType = lastTokenType;
            _readerOptions = readerOptions;
        }

        // Explicit interface implementation: IReaderState<TState>'s members are `internal`, and an
        // internal interface member can only ever be satisfied by a `public` implicitly-implementing
        // member - even across an InternalsVisibleTo friend assembly. Forwarding explicitly here
        // keeps the fields above `internal`/`_`-prefixed/readonly, matching every other field on
        // this type, instead of promoting four of them to `public` just to satisfy the interface.
        long IReaderState<ReaderState>._lineNumber => _lineNumber;

        long IReaderState<ReaderState>._lineNumberPosition => _lineNumberPosition;

        bool IReaderState<ReaderState>._isObject => _isObject;

        bool IReaderState<ReaderState>._isNotPrimitive => _isNotPrimitive;

        /// <summary>
        /// Any deviations from strict adherence to the XML specification
        /// </summary>
        public ReaderOptions ReaderOptions => _readerOptions;
    }
}