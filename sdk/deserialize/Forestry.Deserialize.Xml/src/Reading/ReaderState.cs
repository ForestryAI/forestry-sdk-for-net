using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Xml.Reading
{
    internal readonly struct ReaderState: IReaderState<ReaderState>
    {
        internal readonly long _readerPositionLineNumber;

        internal readonly string _readerPositionName;

        internal readonly long _readerPosition;

        internal readonly bool _isObject;

        internal readonly TokenType _tokenType;

        internal ReaderState(
            long readerPositionLineNumber,
            string readerPositionName,
            long readerPosition,
            bool isObject,
            TokenType tokenType
        )
        {
            _readerPositionLineNumber = readerPositionLineNumber;
            _readerPositionName = readerPositionName;
            _readerPosition = readerPosition;
            _isObject = isObject;
            _tokenType = tokenType;
        }

        public readonly long ReaderPositionLineNumber => _readerPositionLineNumber;

        public readonly string ReaderPositionName => _readerPositionName;

        public readonly long ReaderPosition => _readerPosition;

        public readonly bool IsObject => _isObject;

        public readonly TokenType TokenType => _tokenType;
    }
}