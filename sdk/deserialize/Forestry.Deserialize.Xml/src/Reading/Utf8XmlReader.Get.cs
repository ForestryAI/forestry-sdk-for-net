using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Forestry.Deserialize.Xml.Reading
{
    internal ref partial struct Utf8XmlReader
    {
        public static readonly UTF8Encoding Encoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// <summary>
        /// Get unescaped value
        /// </summary>
        /// <returns></returns>
        internal ReadOnlySpan<byte> GetUnescapedValue()
        {
            ReadOnlySpan<byte> value = HasValueSequence ? ValueSequence.ToArray() : Value;
            // TODO: When escaped convert

            return value;
        }

        /// <summary>
        /// Get string from the name || value of an attribute or element 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public readonly ReadOnlySpan<byte> GetString()
        {
            if (_currentTokenType == TokenType.Null)
            {
                return null;
            }

            if (_currentTokenType != TokenType.Value)
            {
                throw new InvalidOperationException();  // TODO Throwing + Formatting
            }

            ReadOnlySpan<byte> value = HasValueSequence ? ValueSequence.ToArray() : Value;
            return value;
        }

        /// <summary>
        /// Try get string
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetString([NotNullWhen(true)] out string? value)
        {
            ReadOnlySpan<byte> source = GetString();

            try
            {
                value = Encoding.GetString(source);
            } catch
            {
                value = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Try get boolean
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetBoolean([NotNullWhen(true)] out bool? value)
        {
            
            ReadOnlySpan<byte> source = GetString();
            // TODO: maybe case-sensitive which the Utf8Parser is not

            if (Utf8Parser.TryParse(source, out bool tmp, out int bytesConsumed) && source.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Try get short (integer 16)
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetShort(out short value)
        {
            
            ReadOnlySpan<byte> source = GetString();
            // TODO: maybe case-sensitive which the Utf8Parser is not

            if (Utf8Parser.TryParse(source, out short tmp, out int bytesConsumed) && source.Length == bytesConsumed)
            {
                value = tmp;
                return true;
            }

            value = 0;
            return false;
        }
    }
}