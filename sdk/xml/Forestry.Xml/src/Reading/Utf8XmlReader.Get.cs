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
        /// Get the current token's value as a string, one span or a sequence alike: the text of a
        /// <see cref="TokenType.Value"/>, the name of an element, attribute or end tag, or the
        /// whole markup of a declaration, document type, comment or processing instruction
        /// </summary>
        /// <remarks>
        /// <para>
        /// The end of an empty element tag (<c>/&gt;</c>) has an empty value. After
        /// <see cref="Read"/> returned false the value is cleared, so this returns an empty string
        /// when a token was read before it.
        /// </para>
        /// <para>
        /// Character and entity references are not resolved: a value with
        /// <see cref="HasEscapedValue"/> set still contains them (e.g. <c>&amp;amp;</c>).
        /// </para>
        /// </remarks>
        /// <returns>The value decoded from UTF-8, allocated on request</returns>
        /// <exception cref="InvalidOperationException">
        /// No token has been read, or the value is not valid UTF-8
        /// </exception>
        public readonly string GetString()
        {
            if (_currentTokenType == TokenType.None)
            {
                Throwing.WhenNoTokenRead();
            }

            try
            {
                return DecodeValue();
            }
            catch (DecoderFallbackException exception)
            {
                Throwing.WhenValueNotUtf8(exception);
                throw;  // unreachable
            }
        }

        /// <summary>
        /// Try get the current token's value as a string, the same value as <see cref="GetString"/>
        /// without throwing
        /// </summary>
        /// <remarks>
        /// References are not resolved, so a value with <see cref="HasEscapedValue"/> set still
        /// contains them (e.g. <c>&amp;amp;</c>)
        /// </remarks>
        /// <param name="value">The value, null when false is returned</param>
        /// <returns>False when no token has been read or the value is not valid UTF-8</returns>
        public bool TryGetString([NotNullWhen(true)] out string? value)
        {
            value = null;

            if (_currentTokenType == TokenType.None)
            {
                return false;
            }

            try
            {
                value = DecodeValue();
            }
            catch (DecoderFallbackException)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Value decoded from UTF-8 whether it is a byte span or a byte sequence
        /// </summary>
        /// <returns></returns>
        private readonly string DecodeValue() => HasValueSequence
            ? Encoding.GetString(ValueSequence)
            : Encoding.GetString(Value);

        /// <summary>
        /// Try get the value of a <see cref="TokenType.Value"/> token as a boolean. A name or
        /// opaque markup is never a boolean, even when its text would parse
        /// </summary>
        /// <param name="value">The boolean, null when false is returned</param>
        /// <returns>False when the token is not a Value or the whole value is not a boolean</returns>
        public bool TryGetBoolean([NotNullWhen(true)] out bool? value)
        {
            if (_currentTokenType != TokenType.Value)
            {
                value = null;
                return false;
            }

            ReadOnlySpan<byte> source = HasValueSequence ? ValueSequence.ToArray() : Value;
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