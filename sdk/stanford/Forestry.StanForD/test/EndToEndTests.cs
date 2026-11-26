using Forestry.Serialization;
using Forestry.Serialization.Xml;
using Xunit;

namespace Forestry.StanForD.Tests
{
    /// <summary>
    /// Spec 001, User Story 7: real Harvesting documents end to end (tasks.md T057; spec FR-018, SC-001)
    /// </summary>
    /// <remarks>
    /// Reads every document in <c>STANFORD_HPR_FOLDER</c> with the default options (unmatched categories
    /// fail), so an element or category the fragments do not have shows up here. Skipped when the folder
    /// is not set.
    /// </remarks>
    public sealed class EndToEndTests
    {
        /// <summary>
        /// How often <paramref name="tag"/> starts an element in <paramref name="xml"/>
        /// </summary>
        private static int Count(string xml, string tag)
        {
            int count = 0;

            for (int index = xml.IndexOf(tag, StringComparison.Ordinal); index >= 0; index = xml.IndexOf(tag, index + tag.Length, StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }

        /// <summary>
        /// T057, quickstart 11, US7-4, FR-018: every real document gives one Stem telemetry per stem and one Log telemetry
        /// per log, every telemetry identified, with no failure
        /// </summary>
        [RealDocumentFact]
        public void RealDocuments_ItShould_GiveOneTelemetryPerStemAndLog()
        {
            // Arrange
            List<(string Name, string Xml)> documents = RealDocuments.Read().ToList();

            // Act, Assert
            Assert.NotEmpty(documents);

            foreach ((string name, string xml) in documents)
            {
                List<Telemetry> telemetries = TelemetrySerialization.Deserialize(xml, new DeserializeXmlOptions().AddHarvesting()).ToList();
                List<Stem> stems = telemetries.OfType<Stem>().ToList();
                List<Log> logs = telemetries.OfType<Log>().ToList();

                Assert.True(Count(xml, "<Stem>") == stems.Count, $"{name}: {stems.Count} Stem telemetries for {Count(xml, "<Stem>")} stems");
                Assert.True(Count(xml, "<Log>") == logs.Count, $"{name}: {logs.Count} Log telemetries for {Count(xml, "<Log>")} logs");
                Assert.All(stems, stem => Assert.False(string.IsNullOrEmpty(stem.Id), name));
                Assert.All(logs, log => Assert.False(string.IsNullOrEmpty(log.Id), name));
            }
        }
    }
}
