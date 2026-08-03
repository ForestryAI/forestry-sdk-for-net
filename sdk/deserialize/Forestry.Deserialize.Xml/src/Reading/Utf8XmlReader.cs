using System.Buffers;
using Forestry.Deserialize.Xml;

namespace Forestry.Deserialize.Xml.Reading
{
    internal ref partial struct Utf8XmlReader
    {
        private ReadOnlySpan<byte> _bytes;

        private TokenType _tokenType;

        internal Utf8XmlReader(
            ReadOnlySpan<byte> bytes,
            ReaderState readerState
        ) {
            _bytes = bytes;

            _tokenType = readerState.TokenType;
        }

        #region Position
        public readonly TokenType TokenType => _tokenType;

        public bool HasValues { get; private set; }

        public ReadOnlySequence<byte> Values { get; private set; }

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
            ReadOnlySpan<byte> value = HasValues ? Values.ToArray() : Value;
            // TODO: When escaped convert

            return value;
        }
    }
}