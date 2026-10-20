using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using Forestry.Deserialize.Xml.Reading;

namespace Forestry.Deserialize.Xml
{
    /// <summary>
    /// Formats XML exceptions from a category (why) and a markup (what)
    /// </summary>
    internal static partial class Throwing
    {
        #region xml exception
        /// <summary>
        /// Throw malformed markup, the optional found byte is formatted by the Expected
        /// category and the optional bytes by the Incomplete and Name categories
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="category"></param>
        /// <param name="markup"></param>
        /// <param name="foundByte"></param>
        /// <param name="bytes"></param>
        /// <param name="offset">bytes from the committed position to the offending byte (#43)</param>
        [DoesNotReturn]
        public static void ThrowXmlException(
            ref Utf8XmlReader reader,
            Category category,
            Markup markup,
            byte? foundByte = null,
            ReadOnlySpan<byte> bytes = default,
            long offset = 0
        )
        {
            throw GetXmlException(ref reader, category, markup, foundByte, bytes, offset);
        }

        /// <summary>
        /// Temporary throw for markup the POC does not support, outside the category and
        /// markup pair until the unsupported exception is designed
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="offset">bytes from the committed position to the unsupported byte (#43)</param>
        [DoesNotReturn]
        public static void ThrowUnsupportedNameCharacter(ref Utf8XmlReader reader, long offset)
        {
            string message = Formatting.WhenNameUnsupportedCharacter;
            (long lineNumber, long linePosition) = GetPosition(ref reader, offset);

            throw new XmlException(message + GetPositionMessage(lineNumber, linePosition), null, null, lineNumber, linePosition);
        }

        /// <summary>
        /// Temporary throw for a candidate non-terminal of None, outside the category and
        /// markup pair until each context and scratch pad gets a specific explanation
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="foundByte">the first byte at the reader position</param>
        /// <param name="offset">bytes from the committed position to the found byte (#43)</param>
        [DoesNotReturn]
        public static void ThrowUnexpectedMarkup(ref Utf8XmlReader reader, byte foundByte, long offset = 0)
        {
            string message = Deserialize.Formatting.Format(Formatting.Unexpected, ToString(foundByte));
            (long lineNumber, long linePosition) = GetPosition(ref reader, offset);

            throw new XmlException(message + GetPositionMessage(lineNumber, linePosition), null, null, lineNumber, linePosition);
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
        /// Sets the category, markup, line number and line position (1-based) while the
        /// message is created from the category template
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="category"></param>
        /// <param name="markup"></param>
        /// <param name="foundByte"></param>
        /// <param name="bytes"></param>
        /// <param name="offset">bytes from the committed position to the offending byte (#43)</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static XmlException GetXmlException(
            ref Utf8XmlReader reader,
            Category category,
            Markup markup,
            byte? foundByte,
            ReadOnlySpan<byte> bytes,
            long offset = 0
        ) {
            (long lineNumber, long linePosition) = GetPosition(ref reader, offset);
            string message = GetMessage(category, markup, foundByte, bytes) + GetPositionMessage(lineNumber, linePosition);

            return new XmlException(message, category.ToString(), markup.ToString(), lineNumber, linePosition);
        }

        /// <summary>
        /// One template per category filled with the markup and the optional part: a template
        /// only formats an argument the throw site passes
        /// </summary>
        /// <param name="category"></param>
        /// <param name="markup"></param>
        /// <param name="foundByte"></param>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private static string GetMessage(
            Category category,
            Markup markup,
            byte? foundByte,
            ReadOnlySpan<byte> bytes
        ) {
            string optional = category switch
            {
                Category.Expected when foundByte is byte found => $" found {ToString(found)}",
                Category.Incomplete or Category.Name when !bytes.IsEmpty => $" ({Encoding.UTF8.GetString(bytes)})",
                _ => "",
            };

            string template = category switch
            {
                Category.Incomplete => Formatting.Incomplete,
                Category.Expected => Formatting.Expected,
                Category.Name => Formatting.Name,
                Category.Order => Formatting.Order,
                Category.Unique => Formatting.Unique,
                _ => throw new UnreachableException(),
            };

            return Deserialize.Formatting.Format(template, markup.ToString(), optional);
        }

        /// <summary>
        /// Line number and line position of the offending byte, walked from the committed
        /// position without committing (#43): 0-based in the reader, 1-based in exceptions
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        private static (long, long) GetPosition(ref Utf8XmlReader reader, long offset)
        {
            (long lineNumber, long linePosition) = reader.GetLineTrackingAt(offset);
            return (lineNumber + 1, linePosition + 1);
        }

        private static string GetPositionMessage(long lineNumber, long linePosition) =>
            $" at line {lineNumber}, position {linePosition}.";

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

        #region category and markup
        /// <summary>
        /// Why markup is malformed, a broad container for most EBNF problems
        /// </summary>
        internal enum Category
        {
            /// <summary>
            /// The bytes ended first
            /// </summary>
            Incomplete,

            /// <summary>
            /// Wrong byte at a known point
            /// </summary>
            Expected,

            /// <summary>
            /// Naming rules, and start and end tag names not matching
            /// </summary>
            Name,

            /// <summary>
            /// Position in the document
            /// </summary>
            Order,

            /// <summary>
            /// Duplicates
            /// </summary>
            Unique
        }

        /// <summary>
        /// What is malformed, the terminal or non-terminal behind the exception
        /// </summary>
        internal enum Markup
        {
            Declaration,
            DocumentType,
            ProcessingInstructionTarget,
            RootElement,
            Element,
            EndTag,
            EndingTerminal,
            EmptyElementEndingTerminal,
            Name,
            AttributeValue,
            CharacterData,
            EqualsSignTerminal,
            Spacing
        }
        #endregion
    }
}
