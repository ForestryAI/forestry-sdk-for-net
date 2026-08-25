using System.Buffers;
using System.Runtime.CompilerServices;

namespace Forestry.Deserialize.Xml.Reading
{
    /// <summary>
    /// Utf8 Reader
    /// </summary>
    internal static partial class Utf8Reader
    {
        /// <summary>
        /// Try matching source bytes against the starting EBNF terminal
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="terminal"></param>
        /// <param name="bytesRead"></param>
        /// <returns></returns>
        public static bool TryMatch(
            ReadOnlySpan<byte> bytes,
            ReadOnlySpan<byte> terminal,
            out int bytesRead
        )
        {
            bytesRead = 0;
            int offset = 0;

            if (bytes.Length == 0 || terminal.Length == 0 || bytes.Length < terminal.Length)
            {
                return false;
            }

            bool matched = bytes[..terminal.Length].SequenceEqual(terminal);
            if (matched)
            {
                bytesRead = offset + terminal.Length;  // TODO: Review potential options slash offset needs
            }
                        
            return matched;
        }

        /// <summary>
        /// Try skip source bytes and spacing until the end EBNF terminal 
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="terminal"></param>
        /// <param name="bytesRead"></param>
        /// <param name="lineNumbersRead"></param>
        /// <param name="linePosition"></param>
        /// <returns></returns>
        public static bool TrySkip(
            ReadOnlySpan<byte> bytes,
            ReadOnlySpan<byte> terminal,
            out int bytesRead,
            out int lineNumbersRead,
            out int linePosition
        )
        {
            bytesRead = 0;
            lineNumbersRead = 0;
            linePosition = 0;

            if (bytes.Length == 0 || terminal.Length == 0 || bytes.Length < terminal.Length)
            {
                return false;
            }

            int position = bytes.IndexOf(terminal);
            if (position != -1)
            {
                bytesRead = position + terminal.Length;
                ReadOnlySpan<byte> consumed = bytes[..bytesRead];

                (lineNumbersRead, int lastLineFeedIndex) = LineFeeds(consumed);
                linePosition = lastLineFeedIndex >= 0 ? bytesRead - lastLineFeedIndex - 1 : bytesRead;
                return true;
            }

            return false;
        }

        #region spacing
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static (int, int) LineFeeds(ReadOnlySpan<byte> bytes)
        {
            int lineFeedCount = 0;
            int lastLineFeedIndex = bytes.LastIndexOf(EBNF.LineFeed);

            if (lastLineFeedIndex >= 0)
            {
                lineFeedCount +=  bytes[..lastLineFeedIndex].Count(EBNF.LineFeed) + 1;
            }

            return (lineFeedCount, lastLineFeedIndex);
        }

        private static readonly SearchValues<byte> s_whiteSpace = SearchValues.Create(" \t\r\n"u8);

        /// <summary>
        /// NOTE: Example of Search Values if necessary later otherwise 
        /// at the moment not useful
        /// </summary>
        /// <param name="span"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOfExceptWhiteSpace(this ReadOnlySpan<byte> span)
        {
            int index = span.IndexOfAnyExcept(s_whiteSpace);
            return index < 0 ? span.Length : index;
        }
        #endregion
    }
}