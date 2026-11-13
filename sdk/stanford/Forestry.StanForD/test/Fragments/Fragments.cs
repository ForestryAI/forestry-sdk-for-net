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
    }
}
