using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Forestry.Xml.Reading
{
    /// <summary>
    /// Typed value getters
    /// </summary>
    public ref partial struct Utf8XmlReader
    {
        /// <summary>
        /// UTF-8 without a byte order mark, throwing on invalid bytes, used to decode values to strings
        /// </summary>
        public static readonly UTF8Encoding Encoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        internal ReadOnlySpan<byte> GetUnescapedValue()
        {
            ReadOnlySpan<byte> value = HasValueSequence ? ValueSequence.ToArray() : Value;
            // TODO: When escaped convert

            return value;
        }

        /// <summary>
        /// Get string from the name || value of an attribute or element
        /// </summary>
        /// <remarks>
        /// The bytes are raw: character and entity references are not resolved, and
        /// <see cref="HasEscapedValue"/> tells the caller the value contains some
        /// </remarks>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public readonly ReadOnlySpan<byte> GetString()
        {
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
        /// <remarks>
        /// Decodes the raw bytes of <see cref="GetString"/>: references are not resolved, so a
        /// value with <see cref="HasEscapedValue"/> set still contains them (e.g. <c>&amp;amp;</c>)
        /// </remarks>
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
    }
}