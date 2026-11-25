using Forestry.Serialization;
using Forestry.Serialization.Xml;
using Forestry.StanForD.Tests.Fragments;
using Xunit;

namespace Forestry.StanForD.Tests
{
    /// <summary>
    /// Spec 001, User Story 3: partitioning (tasks.md T045-T047, T070-T072; spec FR-006, FR-007, FR-009)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C6). They compile against the members the rows already have,
    /// and fail until the US3 implementation sets them. For the review gate (T048), the assumed behaviour
    /// of <c>AddHarvesting()</c>:
    /// <list type="bullet">
    /// <item><c>HarvestedProductionHeader</c> is a row type that never comes out, kept in the
    /// deserialization context until the document ends; its <c>CreationDate</c> is read.</item>
    /// <item>When a stem or log completes, <c>HarvestingCreationTime</c> is set to the header's
    /// <c>CreationDate</c> converted to UTC (offset zero), and <c>ContractNumber</c> and <c>ObjectName</c>
    /// from the <see cref="ObjectDefinition"/> the stem refers to by <c>ObjectKey</c> (a log through its
    /// stem).</item>
    /// <item>No header <c>CreationDate</c>, or a stem with no <c>ObjectKey</c>, throws
    /// <see cref="InvalidDataException"/> naming the stem and what is missing.</item>
    /// </list>
    /// </remarks>
    public sealed class PartitioningTests
    {
        /// <summary>
        /// The fragment's header creation date, 2026-01-14T15:25:33+01:00, in UTC
        /// </summary>
        private static readonly DateTimeOffset CreationTimeUtc = new(2026, 1, 14, 14, 25, 33, TimeSpan.Zero);

        /// <summary>
        /// The next fragment's header creation date, 2026-01-14T17:14:48+01:00, in UTC
        /// </summary>
        private static readonly DateTimeOffset NextCreationTimeUtc = new(2026, 1, 14, 16, 14, 48, TimeSpan.Zero);

        private static List<Row> Read(string xml) =>
            TelemetrySerializer.Deserialize(xml, new DeserializeXmlOptions().AddHarvesting()).ToList();

        /// <summary>
        /// T045, US3-1, FR-007: every stem and log carries the header's creation date, in UTC
        /// </summary>
        [Fact]
        public void HarvestingDocument_ItShould_GiveEveryRowTheCreationTimeInUtc()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting);

            // Act
            List<DateTimeOffset> times = Read(xml).Select(HarvestingCreationTime).ToList();

            // Assert
            Assert.Equal(5, times.Count);
            Assert.All(times, time =>
            {
                Assert.Equal(CreationTimeUtc, time);
                Assert.Equal(TimeSpan.Zero, time.Offset);
            });
        }

        /// <summary>
        /// T046, US3-2, FR-009: every stem and log carries the contract number and object name of the object
        /// its stem refers to
        /// </summary>
        [Fact]
        public void HarvestingDocument_ItShould_GiveEveryRowTheContractNumberAndObjectName()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting);

            // Act
            List<Row> rows = Read(xml);

            // Assert
            Assert.Equal(5, rows.Count);
            Assert.All(rows.OfType<Stem>(), stem =>
            {
                Assert.Equal("10000001", stem.ContractNumber);
                Assert.Equal("Example object", stem.ObjectName);
            });
            Assert.All(rows.OfType<Log>(), log =>
            {
                Assert.Equal("10000001", log.ContractNumber);
                Assert.Equal("Example object", log.ObjectName);
            });
        }

        /// <summary>
        /// T070, findings-us5.md section 2: the stem a document repeats from the previous one keeps its Id but
        /// carries the later document's creation time, so it lands in another time partition
        /// </summary>
        [Fact]
        public void StemRepeatedInNextDocument_ItShould_CarryTheLaterCreationTime()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting);
            string next = Fragment.Read(Fragment.MinimalHarvestingNext);

            // Act
            Stem first = Read(xml).OfType<Stem>().Single(stem => stem.StemKey == 619255);
            Stem repeated = Read(next).OfType<Stem>().Single(stem => stem.StemKey == 619255);

            // Assert
            Assert.Equal(first.Id, repeated.Id);
            Assert.Equal(CreationTimeUtc, first.HarvestingCreationTime);
            Assert.Equal(NextCreationTimeUtc, repeated.HarvestingCreationTime);
        }

        /// <summary>
        /// T071: a creation date with seven fraction digits, as in the real documents, keeps them
        /// </summary>
        [Fact]
        public void CreationDateWithSevenFractionDigits_ItShould_KeepThem()
        {
            // Arrange
            string xml = Fragment.WithValue(Fragment.Read(Fragment.MinimalHarvesting), "CreationDate", "2026-01-14T15:25:33.6351430+01:00");

            // Act
            DateTimeOffset time = HarvestingCreationTime(Read(xml).OfType<Stem>().First());

            // Assert
            Assert.Equal(CreationTimeUtc.AddTicks(6351430), time);
            Assert.Equal(TimeSpan.Zero, time.Offset);
        }

        /// <summary>
        /// T047, FR-006: a document whose header has no creation date fails, naming the stem and the date
        /// </summary>
        [Fact]
        public void HeaderWithoutCreationDate_ItShould_Throw()
        {
            // Arrange
            string xml = Fragment.Without(Fragment.Read(Fragment.MinimalHarvesting), "CreationDate");

            // Act
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Read(xml));

            // Assert
            Assert.Contains("619254", exception.Message, StringComparison.Ordinal);
            Assert.Contains("CreationDate", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// T072, FR-009 (developer, 2026-11-13): a stem with no object key has no contract, and fails, naming
        /// the stem and the key
        /// </summary>
        [Fact]
        public void StemWithoutObjectKey_ItShould_Throw()
        {
            // Arrange
            string xml = Fragment.WithoutFirstStemElement(Fragment.Read(Fragment.MinimalHarvesting), nameof(Stem.ObjectKey));

            // Act
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Read(xml));

            // Assert
            Assert.Contains("619254", exception.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(Stem.ObjectKey), exception.Message, StringComparison.Ordinal);
        }

        private static DateTimeOffset HarvestingCreationTime(Row row) => row switch
        {
            Stem stem => stem.HarvestingCreationTime,
            Log log => log.HarvestingCreationTime,
            _ => throw new InvalidOperationException(row.GetType().Name)
        };
    }
}
