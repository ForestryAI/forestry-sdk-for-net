namespace Forestry.StanForD.Tests.Fragments
{
    /// <summary>
    /// Handwritten Harvesting fragments in <c>test/Fragments/</c>, copied next to the tests (A4,
    /// tasks.md T007)
    /// </summary>
    public static class Fragment
    {
        /// <summary>
        /// The smallest Harvesting document: header, machine, the common definitions, two stems
        /// with three logs (stem 619254: logs 1 and 2; stem 619255: log 1)
        /// </summary>
        public const string MinimalHarvesting = "MinimalHarvesting.xml";

        /// <summary>
        /// The same machine's next document: the definitions again, stem 619255 repeated, and stem 619256
        /// </summary>
        public const string MinimalHarvestingNext = "MinimalHarvestingNext.xml";

        /// <summary>
        /// Machine key in both fragments
        /// </summary>
        public static readonly Guid MachineKey = Guid.Parse("6f1d2c3b-4a5e-4f60-8a71-92b3c4d5e6f7");

        /// <summary>
        /// A fragment's text
        /// </summary>
        public static string Read(string name) =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fragments", name));

        /// <summary>
        /// The fragment with the value of the first stem's <paramref name="element"/> replaced (the
        /// first such element after the first <c>&lt;Stem&gt;</c>, a log's included)
        /// </summary>
        public static string WithFirstStemValue(string xml, string element, string value)
        {
            (int start, int end) = FirstStemElement(xml, element);
            int valueStart = start + element.Length + 2;
            int valueEnd = end - element.Length - 3;

            return string.Concat(xml.AsSpan(0, valueStart), value, xml.AsSpan(valueEnd));
        }

        /// <summary>
        /// The fragment without the first stem's <paramref name="element"/>
        /// </summary>
        public static string WithoutFirstStemElement(string xml, string element)
        {
            (int start, int end) = FirstStemElement(xml, element);

            return string.Concat(xml.AsSpan(0, start), xml.AsSpan(end));
        }

        /// <summary>
        /// The fragment with the value of its first <paramref name="element"/> replaced
        /// </summary>
        public static string WithValue(string xml, string element, string value)
        {
            int valueStart = xml.IndexOf($"<{element}>", StringComparison.Ordinal) + element.Length + 2;
            int valueEnd = xml.IndexOf($"</{element}>", valueStart, StringComparison.Ordinal);

            return string.Concat(xml.AsSpan(0, valueStart), value, xml.AsSpan(valueEnd));
        }

        /// <summary>
        /// The fragment without its first <paramref name="element"/>
        /// </summary>
        public static string Without(string xml, string element)
        {
            int start = xml.IndexOf($"<{element}>", StringComparison.Ordinal);
            int end = xml.IndexOf($"</{element}>", start, StringComparison.Ordinal) + element.Length + 3;

            return string.Concat(xml.AsSpan(0, start), xml.AsSpan(end));
        }

        /// <summary>
        /// Where the first stem's <paramref name="element"/> starts and ends, its tags included
        /// </summary>
        private static (int Start, int End) FirstStemElement(string xml, string element)
        {
            int stem = xml.IndexOf("<Stem>", StringComparison.Ordinal);
            int start = xml.IndexOf($"<{element}>", stem, StringComparison.Ordinal);
            int end = xml.IndexOf($"</{element}>", start, StringComparison.Ordinal) + element.Length + 3;

            return (start, end);
        }
    }
}
