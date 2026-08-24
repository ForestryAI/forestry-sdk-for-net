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
        /// <param name="source"></param>
        /// <param name="terminal"></param>
        /// <param name="bytesRead"></param>
        /// <returns></returns>
        public static bool TryMatch(
            ReadOnlySpan<byte> source,
            ReadOnlySpan<byte> terminal,
            out int bytesRead
        )
        {
            bytesRead = 0;
            int offset = 0;

            if (source.Length == 0 || terminal.Length == 0 || source.Length < terminal.Length)
            {
                return false;
            }

            bool matched = source[..terminal.Length].SequenceEqual(terminal);
            if (matched)
            {
                bytesRead = offset + terminal.Length;  // TODO: Review potential options slash offset needs
            }
                        
            return matched;
        }

        /// <summary>
        /// Try skip source bytes until the end EBNF terminal
        /// </summary>
        /// <returns></returns>
        public static bool TrySkip(
            ReadOnlySpan<byte> source,
            ReadOnlySpan<byte> terminal,
            out int bytesRead
        )
        {
            bytesRead = 0;

            if (source.Length == 0 || terminal.Length == 0 || source.Length < terminal.Length)
            {
                return false;
            }

            int position = source.IndexOf(terminal);
            if (position != -1)
            {
                bytesRead = position + terminal.Length;
                return true;
            }

            return false;
        }
    }
}