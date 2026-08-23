namespace Forestry.Deserialize.Xml.Reading
{
    /// <summary>
    /// Utf8 Reader
    /// </summary>
    internal static partial class Utf8Reader
    {
        /// <summary>
        /// Try matching source bytes against the target
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="bytesRead"></param>
        /// <returns></returns>
        public static bool TryMatch(
            ReadOnlySpan<byte> source,
            ReadOnlySpan<byte> target,
            out int bytesRead
        )
        {
            bytesRead = 0;
            int offset = 0;

            if (source.Length == 0 || target.Length == 0 || source.Length < target.Length)
            {
                return false;
            }

            bool matched = source[..target.Length].SequenceEqual(target);
            if (matched)
            {
                bytesRead = offset + target.Length;  // TODO: Review potential options slash offset needs
            }
                        
            return source[..target.Length].SequenceEqual(target);
        }
    }
}