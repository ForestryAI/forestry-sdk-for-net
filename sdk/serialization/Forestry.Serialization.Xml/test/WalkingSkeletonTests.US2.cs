using Forestry.Serialization.Xml.Tests.Fixtures;
using Xunit;

namespace Forestry.Serialization.Xml.Tests
{
    /// <summary>
    /// Spec 001, User Story 2: work with telemetries without knowing their class (tasks.md T045, quickstart 5)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4), so they do not compile until T047 declares
    /// <c>Telemetry.AsType&lt;T&gt;()</c>, which these tests read telemetries with (US1's tests use a plain cast).
    /// For the review gate (T046), the assumed surface is US1's plus <c>AsType&lt;T&gt;()</c> (research R4).
    ///
    /// </remarks>
    public sealed class WalkingSkeletonTestsUS2
    {
        private static DeserializeXmlOptions OptionsWith<TTelemetry>() where TTelemetry : Telemetry
        {
            DeserializeXmlOptions options = new();
            options.Telemetries.Add<TTelemetry>();
            return options;
        }

        // T045 - quickstart 5 / US2-4: a member missing from the document keeps its type's default
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForALogWithoutLogVolume_ItShould_LeaveTheDefault(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                  </Log>
                </Root>
                """;

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, OptionsWith<Log>());

            // Assert
            Log log = Assert.Single(telemetries).AsType<Log>();
            Assert.Equal(1, log.LogKey);
            Assert.Equal(0m, log.LogVolume);
        }

        // T045 - US2-4: a missing member that allows null stays null
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForANullableMemberMissing_ItShould_LeaveItNull(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <LogWithNullableVolume>
                    <LogKey>1</LogKey>
                  </LogWithNullableVolume>
                </Root>
                """;

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, OptionsWith<LogWithNullableVolume>());

            // Assert
            LogWithNullableVolume log = Assert.Single(telemetries).AsType<LogWithNullableVolume>();
            Assert.Equal(1, log.LogKey);
            Assert.Null(log.LogVolume);
        }
    }
}
