using Forestry.StanForD.Tests.Fixtures;
using Forestry.StanForD.Tests.Fragments;
using Xunit;

namespace Forestry.StanForD.Tests
{
    /// <summary>
    /// Serialization spec 005, User Story 5 (#153): a StanForD Harvesting document makes a round trip, HPR XML →
    /// Stem and Log telemetries → JSON Lines → parsed and compared (tasks.md T053-T054; the milestone of #137 and
    /// #138, serialization constitution C6)
    /// </summary>
    /// <remarks>
    /// Test shells written before the review gate (C4). They assume what US2 and US3 built: serialize-side
    /// <c>AddHarvesting</c> and <c>SerializeJsonLinesOptions</c>. <see cref="RoundTrip"/> writes one output per
    /// telemetry type and compares each line with its telemetry, member by member; a test passes when there is
    /// no difference.
    /// </remarks>
    public sealed class RoundTripTests
    {
        /// <summary>
        /// T053, quickstart 10, US5-1, SC-004: the committed fragment's stems and logs, one line each, every line
        /// equal to its telemetry
        /// </summary>
        [Fact]
        public void MinimalHarvesting_ItShould_MakeTheRoundTripWithoutADifference()
        {
            // Arrange
            string xml = Fragment.Read("MinimalHarvesting.xml");

            // Act
            RoundTrip.Result result = RoundTrip.Of(xml);

            // Assert
            Assert.NotEmpty(result.Stems);
            Assert.NotEmpty(result.Logs);
            Assert.Equal(result.Stems.Count, result.StemLines.Length);
            Assert.Equal(result.Logs.Count, result.LogLines.Length);
            Assert.Empty(result.Differences());
        }

        /// <summary>
        /// T053: the comparison is not vacuous: a line that lost its members, or holds another telemetry's
        /// values, is reported
        /// </summary>
        [Fact]
        public void Differences_ForATamperedLine_ItShould_ReportIt()
        {
            // Arrange
            RoundTrip.Result result = RoundTrip.Of(Fragment.Read("MinimalHarvesting.xml"));
            string[] emptied = [.. result.StemLines];
            emptied[0] = "{}";
            string[] swapped = [.. result.LogLines];
            (swapped[0], swapped[^1]) = (swapped[^1], swapped[0]);

            // Act
            IReadOnlyList<string> missing = (result with { StemLines = emptied }).Differences();
            IReadOnlyList<string> different = (result with { LogLines = swapped }).Differences();

            // Assert
            Assert.Contains(missing, difference => difference.StartsWith("Stem 0: ", StringComparison.Ordinal) && difference.EndsWith(" missing", StringComparison.Ordinal));
            Assert.True(result.Logs.Count < 2 || different.Any(difference => difference.StartsWith("Log 0: ", StringComparison.Ordinal)), "swapping two logs' lines is reported");
        }

        /// <summary>
        /// T054, quickstart 11, US5-2: every real Harvesting document makes the round trip without a difference;
        /// skipped when <c>STANFORD_HPR_FOLDER</c> is not set
        /// </summary>
        [RealDocumentFact]
        public void RealDocuments_ItShould_MakeTheRoundTripWithoutADifference()
        {
            // Arrange
            List<(string Name, string Xml)> documents = RealDocuments.Read().ToList();
            Assert.NotEmpty(documents);

            // Act
            List<string> differences = [.. documents.SelectMany(document => RoundTrip.Of(document.Xml).Differences().Select(difference => $"{document.Name}: {difference}"))];

            // Assert
            Assert.Empty(differences);
        }
    }
}
