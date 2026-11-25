using Forestry.Serialization;
using Forestry.Serialization.Xml;
using Forestry.StanForD.Tests.Fragments;
using Xunit;

namespace Forestry.StanForD.Tests
{
    /// <summary>
    /// Spec 001, User Story 2: identification (tasks.md T017-T024; spec FR-001 to FR-005)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C6), so they do not compile until the US2 implementation
    /// declares the surface they assume. For the review gate (T025), the assumed surface, in namespace
    /// <c>Forestry.StanForD</c>:
    /// <list type="bullet">
    /// <item><c>DeserializeXmlOptions AddHarvesting(this DeserializeXmlOptions options,
    /// Action&lt;HarvestingOptions&gt;? configure = null)</c>: adds <see cref="Stem"/> and <see cref="Log"/>
    /// (the only rows that come out), <see cref="Machine"/> and the common definitions (never come out),
    /// converters for the StanForD enums the rows use, and the policies.</item>
    /// <item>Identification is set when a row completes: <see cref="Machine"/> is found as an ancestor,
    /// the stem of a log as an ancestor, and the common definitions are kept in the deserialization
    /// context until the document ends.</item>
    /// <item><c>Id</c> is <c>{machine key, GUID format "N"}-{stem key}</c> on a stem and
    /// <c>{machine key}-{stem key}-{log key}</c> on a log.</item>
    /// <item>A stem or log with no machine key, or referring to a common definition the document has not
    /// defined before it, throws <see cref="InvalidDataException"/> naming the stem and the key.</item>
    /// </list>
    /// </remarks>
    public sealed class IdentificationTests
    {
        private static readonly string MachineKeyN = Fragment.MachineKey.ToString("N");

        private static List<Row> Read(string xml) =>
            TelemetrySerializer.Deserialize(xml, new DeserializeXmlOptions().AddHarvesting()).ToList();

        /// <summary>
        /// T017, quickstart 1, US2-1, FR-001: a stem carries the machine key, its stem key and both as its Id
        /// </summary>
        [Fact]
        public void Stem_ItShould_CarryMachineKeyStemKeyAndId()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting);

            // Act
            List<Stem> stems = Read(xml).OfType<Stem>().ToList();

            // Assert
            Assert.Collection(
                stems,
                stem =>
                {
                    Assert.Equal(Fragment.MachineKey, stem.MachineKey);
                    Assert.Equal(619254, stem.StemKey);
                    Assert.Equal($"{MachineKeyN}-619254", stem.Id);
                },
                stem =>
                {
                    Assert.Equal(Fragment.MachineKey, stem.MachineKey);
                    Assert.Equal(619255, stem.StemKey);
                    Assert.Equal($"{MachineKeyN}-619255", stem.Id);
                });
        }

        /// <summary>
        /// T018, quickstart 1, US2-2, FR-002: a log carries the machine key, its stem's key, its log key and
        /// all three as its Id
        /// </summary>
        [Fact]
        public void Log_ItShould_CarryMachineKeyStemKeyLogKeyAndId()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting);

            // Act
            List<Log> logs = Read(xml).OfType<Log>().ToList();

            // Assert
            Assert.Collection(
                logs,
                log =>
                {
                    Assert.Equal(Fragment.MachineKey, log.MachineKey);
                    Assert.Equal(619254, log.StemKey);
                    Assert.Equal(1, log.LogKey);
                    Assert.Equal($"{MachineKeyN}-619254-1", log.Id);
                },
                log =>
                {
                    Assert.Equal(619254, log.StemKey);
                    Assert.Equal(2, log.LogKey);
                    Assert.Equal($"{MachineKeyN}-619254-2", log.Id);
                },
                log =>
                {
                    Assert.Equal(619255, log.StemKey);
                    Assert.Equal(1, log.LogKey);
                    Assert.Equal($"{MachineKeyN}-619255-1", log.Id);
                });
        }

        /// <summary>
        /// T019, quickstart 2, US2-4, SC-001: two machines using the same stem and log keys give no Id twice
        /// </summary>
        [Fact]
        public void TwoMachinesWithEqualKeys_ItShould_GiveNoIdTwice()
        {
            // Arrange
            string first = Fragment.Read(Fragment.MinimalHarvesting);
            string second = first.Replace(Fragment.MachineKey.ToString("D"), "0a9b8c7d-6e5f-4a3b-9c2d-1e0f2a3b4c5d", StringComparison.Ordinal);

            // Act
            List<string> ids = Read(first).Concat(Read(second))
                .Select(row => row switch { Stem stem => stem.Id, Log log => log.Id, _ => throw new InvalidOperationException() })
                .ToList();

            // Assert
            Assert.Equal(10, ids.Count);
            Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        }

        /// <summary>
        /// T020, edge case "log keys restart per stem": log key 1 under two stems gives two Ids
        /// </summary>
        [Fact]
        public void LogKeysRestartingPerStem_ItShould_GiveDifferentIds()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting);

            // Act
            List<Log> firstLogs = Read(xml).OfType<Log>().Where(log => log.LogKey == 1).ToList();

            // Assert
            Assert.Equal(2, firstLogs.Count);
            Assert.NotEqual(firstLogs[0].Id, firstLogs[1].Id);
        }

        /// <summary>
        /// T021, quickstart 3, FR-005: a stem in a machine with no machine key fails, naming the stem
        /// </summary>
        [Fact]
        public void StemWithoutMachineKey_ItShould_Throw()
        {
            // Arrange
            string xml = """
                <HarvestedProduction>
                  <Machine>
                    <Stem>
                      <StemKey>619254</StemKey>
                    </Stem>
                  </Machine>
                </HarvestedProduction>
                """;

            // Act
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Read(xml));

            // Assert
            Assert.Contains("619254", exception.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(Machine.MachineKey), exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// T022, quickstart 4, US2-3, FR-003: a stem or log referring to a common definition not defined
        /// before it fails, naming the stem and the key
        /// </summary>
        [Theory]
        [InlineData(nameof(Stem.ObjectKey))]
        [InlineData(nameof(Stem.SpeciesGroupKey))]
        [InlineData(nameof(Stem.OperatorKey))]
        [InlineData(nameof(Log.ProductKey))]
        public void ReferenceToUndefinedDefinition_ItShould_Throw(string key)
        {
            // Arrange
            string xml = Fragment.WithFirstStemValue(Fragment.Read(Fragment.MinimalHarvesting), key, "999");

            // Act
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Read(xml));

            // Assert
            Assert.Contains("619254", exception.Message, StringComparison.Ordinal);
            Assert.Contains(key, exception.Message, StringComparison.Ordinal);
            Assert.Contains("999", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// T023, FR-004: the same document deserialized twice gives the same Ids
        /// </summary>
        [Fact]
        public void SameDocumentTwice_ItShould_GiveSameIds()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting);

            // Act
            List<string> first = Read(xml).Select(Id).ToList();
            List<string> second = Read(xml).Select(Id).ToList();

            // Assert
            Assert.Equal(first, second);
        }

        /// <summary>
        /// FR-004, findings-us5.md section 2: the stem a document repeats from the previous one has the same
        /// Ids in both, so duplicates can be removed downstream
        /// </summary>
        [Fact]
        public void StemRepeatedInNextDocument_ItShould_HaveSameIds()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting);
            string next = Fragment.Read(Fragment.MinimalHarvestingNext);

            // Act
            List<string> ids = Read(xml).Select(Id).ToList();
            List<string> nextIds = Read(next).Select(Id).ToList();

            // Assert
            Assert.Equal(
                [$"{MachineKeyN}-619255", $"{MachineKeyN}-619255-1"],
                ids.Intersect(nextIds, StringComparer.Ordinal).Order(StringComparer.Ordinal));
        }

        /// <summary>
        /// T024, R2: only stems and logs come out; the machine and the common definitions never do
        /// </summary>
        [Fact]
        public void HarvestingDocument_ItShould_GiveOnlyStemsAndLogs()
        {
            // Arrange
            string xml = Fragment.Read(Fragment.MinimalHarvesting);

            // Act
            List<Row> rows = Read(xml);

            // Assert
            Assert.Equal(2, rows.OfType<Stem>().Count());
            Assert.Equal(3, rows.OfType<Log>().Count());
            Assert.All(rows, row => Assert.True(row is Stem or Log, row.GetType().Name));
        }

        private static string Id(Row row) => row switch
        {
            Stem stem => stem.Id,
            Log log => log.Id,
            _ => throw new InvalidOperationException(row.GetType().Name)
        };
    }
}
