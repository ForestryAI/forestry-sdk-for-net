using Forestry.Serialization;
using Forestry.Serialization.Xml;
using Forestry.StanForD.Tests.Fragments;
using Xunit;

namespace Forestry.StanForD.Tests
{
    /// <summary>
    /// Spec 001, User Story 8: stem metrics (tasks.md T082; spec FR-024)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C6), so they do not compile until the US8 implementation
    /// declares <c>Stem.DBH</c>. For the review gate (T083), the assumed behaviour: <c>StemCoordinates</c>,
    /// <c>Latitude</c>, <c>Longitude</c>, <c>BoomPositioning</c> and <c>SingleTreeProcessedStem</c> are telemetry
    /// types that never come out; when one completes, its policy sets the open <see cref="Stem"/>'s members.
    /// Coordinates are signed by their category (north and east positive, south and west negative). A boom
    /// positioning or coordinate category matching no member fails by default, as unmatched log measurements
    /// do (<c>HarvestingOptions.Unmatched</c>). The values are the fragments' made-up ones (tasks.md T087).
    /// </remarks>
    public sealed class StemMetricTests
    {
        private static List<Stem> Read(string xml, Action<HarvestingOptions>? configure = null) =>
            TelemetrySerializer.Deserialize(xml, new DeserializeXmlOptions().AddHarvesting(configure)).OfType<Stem>().ToList();

        private static Stem FirstStem(List<Stem> stems) => stems.Single(stem => stem.StemKey == 619254);

        /// <summary>
        /// T082, US8-1, FR-024: north and east coordinates are positive, with altitude and coordinate reference
        /// system
        /// </summary>
        [Fact]
        public void NorthEastCoordinates_ItShould_BePositive()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting);

            // Act
            Stem stem = FirstStem(Read(xml));

            // Assert
            Assert.Equal(56.6349311m, stem.Latitude);
            Assert.Equal(15.1924476m, stem.Longitude);
            Assert.Equal(139m, stem.Altitude);
            Assert.Equal(CoordinateReferenceSystem.WGS84, stem.CoordinateReferenceSystem);
        }

        /// <summary>
        /// T082, US8-1, FR-024: south and west coordinates are negative
        /// </summary>
        [Fact]
        public void SouthWestCoordinates_ItShould_BeNegative()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting)
                .Replace("latitudeCategory=\"North\"", "latitudeCategory=\"South\"", StringComparison.Ordinal)
                .Replace("longitudeCategory=\"East\"", "longitudeCategory=\"West\"", StringComparison.Ordinal);

            // Act
            Stem stem = FirstStem(Read(xml));

            // Assert
            Assert.Equal(-56.6349311m, stem.Latitude);
            Assert.Equal(-15.1924476m, stem.Longitude);
        }

        /// <summary>
        /// T082, US8-2, FR-024: each boom positioning category sets its own angle and extension
        /// </summary>
        [Fact]
        public void BoomPositions_ItShould_SetTheirOwnMembers()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting);

            // Act
            Stem stem = FirstStem(Read(xml));

            // Assert
            Assert.Equal(36, stem.BoomAngleFelling);
            Assert.Equal(820, stem.BoomExtensionFelling);
            Assert.Equal(55, stem.BoomAngleLastLogCut);
            Assert.Equal(610, stem.BoomExtensionLastLogCut);
        }

        /// <summary>
        /// T082, US8-3, FR-024: a single-tree processed stem carries its DBH
        /// </summary>
        [Fact]
        public void SingleTreeProcessedStem_ItShould_CarryDbh()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting);

            // Act
            List<Stem> stems = Read(xml);

            // Assert
            Assert.Equal([399, 352], stems.Select(stem => stem.DBH));
        }

        /// <summary>
        /// T082, FR-012: by default, an unknown boom positioning category fails, naming the element and the
        /// category
        /// </summary>
        [Fact]
        public void UnknownBoomPositioningCategory_ItShould_ThrowByDefault()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting)
                .Replace("boomPositioningCategory=\"Felling\"", "boomPositioningCategory=\"Other\"", StringComparison.Ordinal);

            // Act
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Read(xml));

            // Assert
            Assert.Contains("BoomPositioning", exception.Message, StringComparison.Ordinal);
            Assert.Contains("Other", exception.Message, StringComparison.Ordinal);
            Assert.Contains("619254", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// T082, FR-012: with <c>Ignore</c>, an unknown boom positioning category is ignored and the other
        /// metrics are set
        /// </summary>
        [Fact]
        public void UnknownBoomPositioningCategoryWithIgnore_ItShould_SetTheOtherMetrics()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting)
                .Replace("boomPositioningCategory=\"Felling\"", "boomPositioningCategory=\"Other\"", StringComparison.Ordinal);

            // Act
            Stem stem = FirstStem(Read(xml, options => options.Unmatched = UnmatchedOccurrence.Ignore));

            // Assert
            Assert.Equal(0, stem.BoomAngleFelling);
            Assert.Equal(55, stem.BoomAngleLastLogCut);
            Assert.Equal(399, stem.DBH);
        }

        /// <summary>
        /// T082, FR-012: by default, an unknown latitude category fails, naming the element and the category
        /// </summary>
        [Fact]
        public void UnknownLatitudeCategory_ItShould_ThrowByDefault()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting)
                .Replace("latitudeCategory=\"North\"", "latitudeCategory=\"Up\"", StringComparison.Ordinal);

            // Act
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Read(xml));

            // Assert
            Assert.Contains("Latitude", exception.Message, StringComparison.Ordinal);
            Assert.Contains("Up", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// T088 (found by T057, 2026-11-14): a latitude outside a stem's coordinates, e.g. in a species group's
        /// bark function, is not a stem metric and is ignored
        /// </summary>
        [Fact]
        public void LatitudeOutsideStemCoordinates_ItShould_BeIgnored()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting).Replace(
                "      <SpeciesGroupKey>221</SpeciesGroupKey>\n    </SpeciesGroupDefinition>",
                "      <BarkFunction barkFunctionCategory=\"Skogforsk 2004, Scots pine\">\n" +
                "        <Skogforsk2004ScotsPine>\n" +
                "          <Latitude>56.6421937</Latitude>\n" +
                "        </Skogforsk2004ScotsPine>\n" +
                "      </BarkFunction>\n" +
                "      <SpeciesGroupKey>221</SpeciesGroupKey>\n    </SpeciesGroupDefinition>",
                StringComparison.Ordinal);

            // Act
            Stem stem = FirstStem(Read(xml));

            // Assert
            Assert.Contains("<BarkFunction", xml, StringComparison.Ordinal);
            Assert.Equal(56.6349311m, stem.Latitude);
        }

        /// <summary>
        /// T082, R2: the wrapper elements never come out; only stems and logs do
        /// </summary>
        [Fact]
        public void HarvestingDocument_ItShould_GiveNoMetricTelemetries()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting);

            // Act
            List<Telemetry> telemetries = TelemetrySerializer.Deserialize(xml, new DeserializeXmlOptions().AddHarvesting()).ToList();

            // Assert
            Assert.Equal(5, telemetries.Count);
            Assert.All(telemetries, telemetry => Assert.True(telemetry is Stem or Log, telemetry.GetType().Name));
        }
    }
}
