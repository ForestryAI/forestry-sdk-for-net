using Forestry.Deserialize;
using Forestry.Deserialize.Xml;
using Forestry.StanForD.Tests.Fragments;
using Xunit;

namespace Forestry.StanForD.Tests
{
    /// <summary>
    /// Spec 001, User Story 4: conditional mapping (tasks.md T032-T038, T074-T076; spec FR-010 to FR-013)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C6), so they do not compile until the US4 implementation
    /// declares the surface they assume. For the review gate (T039), the assumed surface, in namespace
    /// <c>Forestry.StanForD</c>:
    /// <list type="bullet">
    /// <item><c>AddHarvesting(this DeserializeXmlOptions options, Action&lt;HarvestingOptions&gt;? configure)</c>,
    /// with <c>HarvestingOptions.Unmatched</c> an <c>UnmatchedOccurrence</c>: <c>Fail</c> (the default) or
    /// <c>Ignore</c>.</item>
    /// <item><c>LogVolume</c> (its <c>logVolumeCategory</c> and <c>logMeasurementCategory</c> attributes and
    /// its value), <c>LogMeasurement</c> (its <c>logMeasurementCategory</c> attribute and <c>LogLength</c>) and
    /// <c>LogDiameter</c> (its <c>logDiameterCategory</c> attribute and its value) are row types that never come
    /// out. When one completes, its policy matches its categories (a diameter's measurement category is its
    /// enclosing <c>LogMeasurement</c>'s) and sets the matching member of the open <see cref="Log"/>.</item>
    /// <item>The conditions, all with measurement category "Machine": volumes "m3sob", "m3sub", "m3 (price)";
    /// diameters "Butt ob", "Butt ub", "Mid ob", "Mid ub", "Mid HKS ob", "Mid HKS ub", "Top ob", "Top ub";
    /// the log length.</item>
    /// <item>An occurrence matching no condition, by default, or a second occurrence for a member already set,
    /// throws <see cref="InvalidDataException"/> naming the element, its categories and the log.</item>
    /// </list>
    /// A member no occurrence sets stays 0, StanForD's "not measured" (developer, 2026-11-13).
    /// </remarks>
    public sealed class ConditionalMappingTests
    {
        private static List<Row> Read(string xml, Action<HarvestingOptions>? configure = null) =>
            Deserialization.Deserialize(xml, new DeserializeXmlOptions().AddHarvesting(configure)).ToList();

        /// <summary>
        /// The first log of stem 619254, which carries every measurement
        /// </summary>
        private static Log FirstLog(List<Row> rows) =>
            rows.OfType<Log>().First(log => log.StemKey == 619254 && log.LogKey == 1);

        /// <summary>
        /// T032, quickstart 6, US4-1: three volumes told apart by their categories fill their own members
        /// </summary>
        [Fact]
        public void ThreeVolumes_ItShould_FillTheirOwnMembers()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting);

            // Act
            Log log = FirstLog(Read(xml));

            // Assert
            Assert.Equal(0.5925430m, log.MachineVolumeOverBark);
            Assert.Equal(0.5332270m, log.MachineVolumeUnderBark);
            Assert.Equal(0.5332271m, log.MachineVolumePrice);
        }

        /// <summary>
        /// T034, quickstart 7, FR-010, FR-011: eight diameters told apart by their category, under a machine
        /// measurement, fill their own members, and the length is set
        /// </summary>
        [Fact]
        public void EightDiametersAndLength_ItShould_FillTheirOwnMembers()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting);

            // Act
            Log log = FirstLog(Read(xml));

            // Assert
            Assert.Equal(495, log.MachineDiameterButtOverBark);
            Assert.Equal(471, log.MachineDiameterButtUnderBark);
            Assert.Equal(383, log.MachineDiameterMidOverBark);
            Assert.Equal(363, log.MachineDiameterMidUnderBark);
            Assert.Equal(380, log.MachineDiameterMidHKSOverBark);
            Assert.Equal(360, log.MachineDiameterMidHKSUnderBark);
            Assert.Equal(362, log.MachineDiameterTopOverBark);
            Assert.Equal(343, log.MachineDiameterTopUnderBark);
            Assert.Equal(491, log.MachineLength);
        }

        /// <summary>
        /// T074: a log measuring only some categories keeps 0 for the others ("not measured")
        /// </summary>
        [Fact]
        public void LogWithSomeMeasurements_ItShould_KeepZeroForTheOthers()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting);

            // Act
            Log log = Read(xml).OfType<Log>().Single(log => log.StemKey == 619254 && log.LogKey == 2);

            // Assert
            Assert.Equal(0.4012000m, log.MachineVolumeOverBark);
            Assert.Equal(0m, log.MachineVolumeUnderBark);
            Assert.Equal(301, log.MachineDiameterTopOverBark);
            Assert.Equal(0, log.MachineDiameterButtOverBark);
            Assert.Equal(430, log.MachineLength);
        }

        /// <summary>
        /// T033, US4-2, FR-010: a volume matching the volume category but not the measurement category sets
        /// nothing
        /// </summary>
        [Fact]
        public void VolumeMatchingOneCategoryOnly_ItShould_SetNothing()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting).Replace(
                """<LogVolume logVolumeCategory="m3sob" logMeasurementCategory="Machine">0.4012000</LogVolume>""",
                """<LogVolume logVolumeCategory="m3sob" logMeasurementCategory="Other">0.4012000</LogVolume>""",
                StringComparison.Ordinal);

            // Act
            Log log = Read(xml, options => options.Unmatched = UnmatchedOccurrence.Ignore)
                .OfType<Log>().Single(log => log.StemKey == 619254 && log.LogKey == 2);

            // Assert
            Assert.Equal(0m, log.MachineVolumeOverBark);
            Assert.Equal(430, log.MachineLength);
        }

        /// <summary>
        /// T035, quickstart 8, US4-4, FR-012: by default, a volume with an unknown category fails, naming the
        /// element, its categories and the log
        /// </summary>
        [Fact]
        public void UnknownVolumeCategory_ItShould_ThrowByDefault()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting).Replace("m3sub", "m3xyz", StringComparison.Ordinal);

            // Act
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Read(xml));

            // Assert
            Assert.Contains("LogVolume", exception.Message, StringComparison.Ordinal);
            Assert.Contains("m3xyz", exception.Message, StringComparison.Ordinal);
            Assert.Contains("619254", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// T075, FR-012: by default, a diameter with an unknown category fails, naming the element and its
        /// category
        /// </summary>
        [Fact]
        public void UnknownDiameterCategory_ItShould_ThrowByDefault()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting).Replace("\"Mid HKS ub\"", "\"Mid XYZ ub\"", StringComparison.Ordinal);

            // Act
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Read(xml));

            // Assert
            Assert.Contains("LogDiameter", exception.Message, StringComparison.Ordinal);
            Assert.Contains("Mid XYZ ub", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// T076, FR-010, FR-012: by default, diameters under a measurement that is not the machine's fail,
        /// naming the measurement category
        /// </summary>
        [Fact]
        public void DiametersUnderAnotherMeasurement_ItShould_ThrowByDefault()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting).Replace(
                """<LogMeasurement logMeasurementCategory="Machine">""",
                """<LogMeasurement logMeasurementCategory="Other">""",
                StringComparison.Ordinal);

            // Act
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Read(xml));

            // Assert
            Assert.Contains("LogDiameter", exception.Message, StringComparison.Ordinal);
            Assert.Contains("Other", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// T086, FR-011 (developer, 2026-11-14): an operator's (M2) volume, beside the machine's (M1) ones, is
        /// deliberately ignored by default, so it neither fails nor changes the machine's members
        /// </summary>
        [Fact]
        public void OperatorVolume_ItShould_BeIgnoredByDefault()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting).Replace(
                """<LogVolume logVolumeCategory="m3 (price)" logMeasurementCategory="Machine">0.5332271</LogVolume>""",
                """<LogVolume logVolumeCategory="m3 (price)" logMeasurementCategory="Machine">0.5332271</LogVolume>""" +
                """<LogVolume logVolumeCategory="m3sob" logMeasurementCategory="Operator">0.6000000</LogVolume>""",
                StringComparison.Ordinal);

            // Act
            Log log = FirstLog(Read(xml));

            // Assert
            Assert.Equal(0.5925430m, log.MachineVolumeOverBark);
            Assert.Equal(0.5332270m, log.MachineVolumeUnderBark);
            Assert.Equal(0.5332271m, log.MachineVolumePrice);
        }

        /// <summary>
        /// T036, quickstart 9, US4-3: with <c>Ignore</c>, an unknown category is ignored and every other member is
        /// set
        /// </summary>
        [Fact]
        public void UnknownCategoryWithIgnore_ItShould_SetTheOtherMembers()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting).Replace("m3sub", "m3xyz", StringComparison.Ordinal);

            // Act
            Log log = FirstLog(Read(xml, options => options.Unmatched = UnmatchedOccurrence.Ignore));

            // Assert
            Assert.Equal(0.5925430m, log.MachineVolumeOverBark);
            Assert.Equal(0m, log.MachineVolumeUnderBark);
            Assert.Equal(0.5332271m, log.MachineVolumePrice);
            Assert.Equal(495, log.MachineDiameterButtOverBark);
        }

        /// <summary>
        /// T037, quickstart 10, FR-013: two volumes with the same categories in one log fail, naming the
        /// element and the log
        /// </summary>
        [Fact]
        public void TwoVolumesWithSameCategories_ItShould_Throw()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting).Replace("\"m3sub\"", "\"m3sob\"", StringComparison.Ordinal);

            // Act
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Read(xml));

            // Assert
            Assert.Contains("LogVolume", exception.Message, StringComparison.Ordinal);
            Assert.Contains("m3sob", exception.Message, StringComparison.Ordinal);
            Assert.Contains("619254", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// T038, R2: volumes, measurements and diameters never come out; only stems and logs do
        /// </summary>
        [Fact]
        public void HarvestingDocument_ItShould_GiveNoMeasurementRows()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting);

            // Act
            List<Row> rows = Read(xml);

            // Assert
            Assert.Equal(5, rows.Count);
            Assert.All(rows, row => Assert.True(row is Stem or Log, row.GetType().Name));
        }
    }
}
