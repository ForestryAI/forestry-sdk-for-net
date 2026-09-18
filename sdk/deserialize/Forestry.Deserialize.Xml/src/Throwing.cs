using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using Forestry.Deserialize.Xml.Reading;

namespace Forestry.Deserialize.Xml
{
    /// <summary>
    /// Formats different exception types
    /// </summary>
    internal static partial class Throwing
    {
        #region xml exception
        [DoesNotReturn]
        public static void ThrowXmlException(
            ref Utf8XmlReader reader, 
            ExceptionType resource, 
            byte nextByte = default, 
            ReadOnlySpan<byte> bytes = default
        )
        {
            throw GetXmlException(ref reader, resource, nextByte, bytes);
        }
        #endregion

        #region invalid operation
        [DoesNotReturn]
        public static void WhenValueNotPositive(int value, string name)
        {
            throw new InvalidOperationException(Deserialize.Formatting.Format(Formatting.WhenNotPositive, value, name));
        }
        #endregion

        #region exception formatting
        /// <summary>
        /// Sets the line number and line position from the reader state while 
        /// the message is crated from a <see cref="ExceptionType"/>
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="resource"></param>
        /// <param name="nextByte"></param>
        /// <param name="bytes"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static XmlException GetXmlException(
            ref Utf8XmlReader reader, 
            ExceptionType resource, 
            byte nextByte, 
            ReadOnlySpan<byte> bytes
        ) {
            string message = GetExceptionType(ref reader, resource, nextByte, Encoding.UTF8.GetString(bytes));

            long lineNumber = reader.ReaderState._lineNumber;
            long linePosition = reader.ReaderState._linePosition;

            message += $" LineNumber: {lineNumber} | LinePosition: {linePosition}.";
            return new XmlException(message, lineNumber, linePosition);
        }

        /// <summary>
        /// Get <see cref="ExceptionType"/> as a string
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="resource"></param>
        /// <param name="nextByte"></param>
        /// <param name="characters"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GetExceptionType(
            ref Utf8XmlReader reader, 
            ExceptionType resource, 
            byte nextByte, 
            string characters
        ) {
            string character = ToString(nextByte);

            string message = "";
            switch (resource)
            {
                case ExceptionType.WhenDocumentHasNoRootElement:
                    message = Formatting.WhenDocumentHasNoRootElement;
                    break;
                case ExceptionType.WhenDocumentHasElementNotEnded:
                    message = Formatting.WhenDocumentHasElementNotEnded;
                    break;
                case ExceptionType.WhenDocumentHasNoTokens:
                    message = Formatting.WhenDocumentHasNoTokens;
                    break;
                case ExceptionType.WhenNoNameAfterElementStartTerminal:
                    message = Deserialize.Formatting.Format(Formatting.WhenNoNameAfterElementStartTerminal, character);
                    break;
                default:
                    break;
            }

            return message;
        }

        /// <summary>
        /// Return the byte value as a char string when it is a string 
        /// otherwise char data = "0x{value:X2}
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string ToString(byte value)
        {
            return IsString(value) ? ((char)value).ToString() : $"0x{value:X2}";
        }

        /// <summary>
        /// Byte value is an ASCII character when greater or equal to a space and less than a 
        /// delete i.e. invalid or non‑text means anything ASCII printable:
        /// - Whitespace
        /// - A-Z uppercase, a-z lowercase
        /// - Digits 0-9
        /// - Punctuation and symbols
        /// - Everything printable up to delete
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool IsString(byte value) => value is >= 0x20 and < 0x7F;
        #endregion

        #region read exception type
        /// <summary>
        /// XML exception or invalid operation exception types
        /// </summary>
        internal enum ExceptionType
        {
            WhenDocumentHasNoRootElement,
            WhenDocumentHasElementNotEnded,
            WhenDocumentHasNoTokens,
            WhenNoNameAfterElementStartTerminal
        }
        #endregion
    }
}