using Forestry.Serialization;
using Forestry.Serialization.Xml;
using Forestry.StanForD.Tests.Fragments;
using Xunit;

namespace Forestry.StanForD.Tests
{
    /// <summary>
    /// Spec 001, User Story 7: Log and Stem dimensions (tasks.md T078-T081; spec FR-021 to FR-023)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C6), so they do not compile until the US7 implementation
    /// declares the members they assume (data-model.md, "Dimensions (US7)"). Every dimension is copied
    /// onto each telemetry it applies to, a stem's onto its logs too, so no query needs a join. The values are
    /// the fragments' made-up ones (tasks.md T077).
    /// </remarks>
    public sealed class DimensionTests
    {
        private static List<Telemetry> Read() =>
            TelemetrySerialization.Deserialize(Fragment.Read(Fragment.MinimalHarvesting), new DeserializeXmlOptions().AddHarvesting()).ToList();

        /// <summary>
        /// T078, US7-1, FR-021, FR-022: every stem carries its stem number and its object, sub-object,
        /// species group and operator dimensions
        /// </summary>
        [Fact]
        public void Stem_ItShould_CarryItsDimensions()
        {
            // Arrange, Act
            List<Stem> stems = Read().OfType<Stem>().ToList();

            // Assert
            Assert.Equal([527, 528], stems.Select(stem => stem.StemNumber));
            Assert.All(stems, stem =>
            {
                Assert.Equal(283, stem.ObjectKey);
                Assert.Equal("Example object", stem.ObjectName);
                Assert.Equal("4200001", stem.ObjectUserID);

                Assert.Equal(1, stem.SubObjectKey);
                Assert.Equal("Standard", stem.SubObjectName);
                Assert.Equal("sub-0001", stem.SubObjectUserID);

                Assert.Equal(221, stem.SpeciesGroupKey);
                Assert.Equal("GRAN", stem.SpeciesGroupName);
                Assert.Equal("2", stem.SpeciesGroupInfo);
                Assert.Equal("EX_Gran", stem.SpeciesGroupUserID);
                Assert.Equal("V1", stem.SpeciesGroupVersion);

                Assert.Equal(2, stem.OperatorKey);
                Assert.Equal("Example Logging AB", stem.OperatorBusinessName);
                Assert.Equal("op-0002", stem.OperatorUserID);
            });
        }

        /// <summary>
        /// T079, US7-1, FR-021: every log carries the same stem dimensions as its stem, so no query joins
        /// Log to Stem
        /// </summary>
        [Fact]
        public void Log_ItShould_CarryItsStemsDimensions()
        {
            // Arrange
            List<Telemetry> telemetries = Read();
            Dictionary<int, Stem> stems = telemetries.OfType<Stem>().ToDictionary(stem => stem.StemKey);

            // Act
            List<Log> logs = telemetries.OfType<Log>().ToList();

            // Assert
            Assert.Equal(3, logs.Count);
            Assert.All(logs, log =>
            {
                Stem stem = stems[log.StemKey];

                Assert.Equal(stem.StemNumber, log.StemNumber);
                Assert.Equal(stem.ObjectKey, log.ObjectKey);
                Assert.Equal(stem.ObjectName, log.ObjectName);
                Assert.Equal(stem.ObjectUserID, log.ObjectUserID);
                Assert.Equal(stem.SubObjectKey, log.SubObjectKey);
                Assert.Equal(stem.SubObjectName, log.SubObjectName);
                Assert.Equal(stem.SubObjectUserID, log.SubObjectUserID);
                Assert.Equal(stem.SpeciesGroupKey, log.SpeciesGroupKey);
                Assert.Equal(stem.SpeciesGroupName, log.SpeciesGroupName);
                Assert.Equal(stem.SpeciesGroupInfo, log.SpeciesGroupInfo);
                Assert.Equal(stem.SpeciesGroupUserID, log.SpeciesGroupUserID);
                Assert.Equal(stem.SpeciesGroupVersion, log.SpeciesGroupVersion);
                Assert.Equal(stem.OperatorKey, log.OperatorKey);
                Assert.Equal(stem.OperatorBusinessName, log.OperatorBusinessName);
                Assert.Equal(stem.OperatorUserID, log.OperatorUserID);
            });
            Assert.Equal([527, 527, 528], logs.Select(log => log.StemNumber));
        }

        /// <summary>
        /// T080, US7-2, FR-022: every log carries its product's key, name, info, user id and version, and its
        /// destination's business name and id
        /// </summary>
        [Fact]
        public void Log_ItShould_CarryItsProductAndDestination()
        {
            // Arrange, Act
            List<Log> logs = Read().OfType<Log>().ToList();

            // Assert
            Assert.Equal(3, logs.Count);
            Assert.All(logs, log =>
            {
                Assert.Equal(3350, log.ProductKey);
                Assert.Equal("Spruce sawlog", log.ProductName);
                Assert.Equal("012-EX", log.ProductInfo);
                Assert.Equal("Gran_timmer_EX", log.ProductUserID);
                Assert.Equal("EX121", log.ProductVersion);
                Assert.Equal("Example Sawmill", log.ProductDestinationBusinessName);
                Assert.Equal("89001", log.ProductDestinationBusinessID);
            });
        }

        /// <summary>
        /// T081, US7-3, FR-023: every stem and log carries the machine's user id and its logging contractor's
        /// business id
        /// </summary>
        [Fact]
        public void EveryTelemetry_ItShould_CarryMachineUserIdAndLoggingContractor()
        {
            // Arrange, Act
            List<Telemetry> telemetries = Read();

            // Assert
            Assert.Equal(5, telemetries.Count);
            Assert.All(telemetries.OfType<Stem>(), stem =>
            {
                Assert.Equal("EX-H01", stem.MachineUserID);
                Assert.Equal("99001", stem.LoggingContractorBusinessID);
            });
            Assert.All(telemetries.OfType<Log>(), log =>
            {
                Assert.Equal("EX-H01", log.MachineUserID);
                Assert.Equal("99001", log.LoggingContractorBusinessID);
            });
        }
    }
}
