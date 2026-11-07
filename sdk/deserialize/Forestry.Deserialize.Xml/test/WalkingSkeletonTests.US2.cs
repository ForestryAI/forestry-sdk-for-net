using Forestry.Deserialize.Xml.Tests.Fixtures;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    /// <summary>
    /// Spec 001, User Story 2: work with rows without knowing their class (tasks.md T045, quickstart 5)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4), so they do not compile until T047 declares
    /// <c>Row.AsType&lt;T&gt;()</c>, which these tests read rows with (US1's tests use a plain cast).
    /// For the review gate (T046), the assumed surface is US1's plus <c>AsType&lt;T&gt;()</c> (research R4).
    ///
    /// </remarks>
    public sealed class WalkingSkeletonTestsUS2
    {
        private static DeserializeXmlOptions OptionsWith<TRow>() where TRow : Row
        {
            DeserializeXmlOptions options = new();
            options.Rows.Add<TRow>();
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
            List<Row> rows = EntryPoints.Read(entryPoint, document, OptionsWith<Log>());

            // Assert
            Log log = Assert.Single(rows).AsType<Log>();
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
            List<Row> rows = EntryPoints.Read(entryPoint, document, OptionsWith<LogWithNullableVolume>());

            // Assert
            LogWithNullableVolume log = Assert.Single(rows).AsType<LogWithNullableVolume>();
            Assert.Equal(1, log.LogKey);
            Assert.Null(log.LogVolume);
        }
    }
}
