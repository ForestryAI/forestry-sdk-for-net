using System.Text;
using Forestry.Deserialize.Xml.Tests.Fixtures;
using Forestry.Xml;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    /// <summary>
    /// Spec 001, User Story 4: change behavior with policies (tasks.md T050-T052, T087, T088, T091;
    /// quickstart 8-9)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4), so they do not compile until T056-T058 declare the
    /// surface they assume (research R19). For the review gate (T055), the assumed surface:
    /// <list type="bullet">
    /// <item><c>EnumerationPolicy</c> (namespace <c>Forestry.Deserialize.Policies</c>) with virtual
    /// <c>Construct(TypeDefinition, Func&lt;Row&gt; next)</c>,
    /// <c>Set&lt;T&gt;(Row, PropertyDefinition&lt;T&gt;, T, Action&lt;Row, T&gt; next)</c>,
    /// <c>Enumerate(Row, TypeDefinition, Func&lt;Row, Row?&gt; next)</c> and <c>Clear()</c>; a policy
    /// takes part only in the methods it overrides.</item>
    /// <item><c>[Policy&lt;TPolicy&gt;]</c> on a row class attaches an enumeration policy to the type; the
    /// options create it once, when they lock.</item>
    /// <item><c>options.Policies.Add&lt;TRow&gt;(policy)</c> attaches an instance through the options;
    /// several for the same method and target chain in the order added, the first outermost.</item>
    /// <item>Policies live as long as the options: each enumeration clears every enumeration policy once
    /// when its enumerator is disposed, and an enumeration starting while another is still running on
    /// the same options throws <see cref="InvalidOperationException"/>.</item>
    /// </list>
    /// The fixtures <see cref="RecordingPolicy"/> and <see cref="RecordingSetPolicy"/> write to a
    /// <see cref="Recording"/> scoped to the running test. Theories run both entry points (SC-001).
    /// </remarks>
    public sealed class WalkingSkeletonTestsUS4
    {
        private static DeserializeXmlOptions OptionsWith<TRow>() where TRow : Row
        {
            DeserializeXmlOptions options = new();
            options.Rows.Add<TRow>();
            return options;
        }

        // T050 - quickstart 8 / US4-1 / FR-011: without policies the built-in defaults give US1's rows,
        // and no policy is called
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_WithoutPolicies_ItShould_GiveTheBuiltInDefaultsRows(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                    <LogVolume>0.59</LogVolume>
                  </Log>
                  <Log>
                    <LogKey>2</LogKey>
                    <LogVolume>0.50</LogVolume>
                  </Log>
                </Root>
                """;
            using Recording recording = Recording.Start();

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, OptionsWith<Log>());

            // Assert
            Assert.Equal([1, 2], rows.Select(row => row.AsType<Log>().LogKey));
            Assert.Equal([0.59m, 0.50m], rows.Select(row => row.AsType<Log>().LogVolume));
            Assert.Empty(recording.Records);
        }

        // T051 - quickstart 9 / US4-2 / FR-012: a policy attached by attribute acts once per row, in
        // document order
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAPolicyAttachedByAttribute_ItShould_ActOncePerRowInDocumentOrder(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                    <LogVolume>0.59</LogVolume>
                  </Log>
                  <Log>
                    <LogKey>2</LogKey>
                    <LogVolume>0.50</LogVolume>
                  </Log>
                </Root>
                """;
            using Recording recording = Recording.Start();

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, OptionsWith<RecordedLog>());

            // Assert
            Assert.Equal([1, 2], rows.Select(row => row.AsType<RecordedLog>().LogKey));
            Assert.Equal(
                ["construct RecordedLog", "enumerate RecordedLog", "construct RecordedLog", "enumerate RecordedLog"],
                recording.Records.Where(record => record.StartsWith("construct") || record.StartsWith("enumerate")));
        }

        // T052 - flow 7: across moments, per row: construct, set each member in document order, enumerate;
        // the policy is cleared once at the end (R7 minimal order, R19)
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAPolicyInEverySlot_ItShould_SeeConstructSetAndEnumerateInOrder(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                    <LogVolume>0.59</LogVolume>
                  </Log>
                  <Log>
                    <LogKey>2</LogKey>
                    <LogVolume>0.50</LogVolume>
                  </Log>
                </Root>
                """;
            using Recording recording = Recording.Start();

            // Act
            EntryPoints.Read(entryPoint, document, OptionsWith<RecordedLog>());

            // Assert
            Assert.Equal(
                [
                    "construct RecordedLog", "set LogKey=1", "set LogVolume=0.59", "enumerate RecordedLog",
                    "construct RecordedLog", "set LogKey=2", "set LogVolume=0.50", "enumerate RecordedLog",
                    "clear"
                ],
                recording.Records);
        }

        // T087 - R19 (chains): two policy instances through the options for Log act in the order added,
        // the first outermost, and the built-in step still gives the rows
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForTwoPoliciesAddedThroughTheOptions_ItShould_ChainThemInTheOrderAdded(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                    <LogVolume>0.59</LogVolume>
                  </Log>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith<Log>();
            options.Policies.Add<Log>(new RecordingPolicy("first"));
            options.Policies.Add<Log>(new RecordingPolicy("second"));
            using Recording recording = Recording.Start();

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Log log = Assert.Single(rows).AsType<Log>();
            Assert.Equal(1, log.LogKey);
            Assert.Equal(0.59m, log.LogVolume);
            Assert.Equal(
                [
                    "first construct Log", "second construct Log",
                    "first set LogKey=1", "second set LogKey=1",
                    "first set LogVolume=0.59", "second set LogVolume=0.59",
                    "first enumerate Log", "second enumerate Log"
                ],
                recording.Records.Where(record => !record.EndsWith("clear")));
            Assert.Contains("first clear", recording.Records);
            Assert.Contains("second clear", recording.Records);
        }

        // T088 - R19: a policy takes part only in the methods it overrides: Set only, for every member of Log
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAPolicyOverridingOnlySet_ItShould_ActOnEveryMemberAndNothingElse(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                    <LogVolume>0.59</LogVolume>
                  </Log>
                  <Log>
                    <LogKey>2</LogKey>
                    <LogVolume>0.50</LogVolume>
                  </Log>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith<Log>();
            options.Policies.Add<Log>(new RecordingSetPolicy());
            using Recording recording = Recording.Start();

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([1, 2], rows.Select(row => row.AsType<Log>().LogKey));
            Assert.Equal(
                ["set LogKey=1", "set LogVolume=0.59", "set LogKey=2", "set LogVolume=0.50"],
                recording.Records);
        }

        // T091 - R19 (lifetime): cleared exactly once at the end of an enumeration
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ToTheEnd_ItShould_ClearThePolicyOnce(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                    <LogVolume>0.59</LogVolume>
                  </Log>
                </Root>
                """;
            using Recording recording = Recording.Start();

            // Act
            EntryPoints.Read(entryPoint, document, OptionsWith<RecordedLog>());

            // Assert
            Assert.Single(recording.ClearedPolicies);
        }

        // T091 - R19 (lifetime): cleared exactly once on an early stop, and the next row is never read
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public async Task Deserialize_StoppedAfterTheFirstRow_ItShould_ClearThePolicyOnce(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                    <LogVolume>0.59</LogVolume>
                  </Log>
                  <Log>
                    <LogKey>2</LogKey>
                    <LogVolume>0.50</LogVolume>
                  </Log>
                </Root>
                """;
            using Recording recording = Recording.Start();

            // Act
            await ReadFirstRowThenStopAsync(entryPoint, document, OptionsWith<RecordedLog>());

            // Assert
            Assert.Single(recording.ClearedPolicies);
            Assert.DoesNotContain("set LogKey=2", recording.Records);
        }

        // T091 - R19 (lifetime): cleared exactly once after a throw (a document cut off inside a row)
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForADocumentCutOffInsideALog_ItShould_ClearThePolicyOnce(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                """;
            using Recording recording = Recording.Start();

            // Act
            Assert.Throws<XmlException>(() => EntryPoints.Read(entryPoint, document, OptionsWith<RecordedLog>()));

            // Assert
            Assert.Single(recording.ClearedPolicies);
        }

        // T091 - R19 (lifetime): the policy lives as long as the options: the same instance serves the
        // next enumeration, cleared after each
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_TwiceWithTheSameOptions_ItShould_UseTheSamePolicyClearedAfterEach(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                    <LogVolume>0.59</LogVolume>
                  </Log>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith<RecordedLog>();
            using Recording recording = Recording.Start();

            // Act
            EntryPoints.Read(entryPoint, document, options);
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(2, recording.ClearedPolicies.Count);
            Assert.Same(recording.ClearedPolicies[0], recording.ClearedPolicies[1]);
        }

        // T091 - R19 (lifetime): an enumeration starting while another is still running on options with
        // enumeration policies throws; once the first ends, the options can be used again
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public async Task Deserialize_WhileAnotherEnumerationIsRunning_ItShould_Throw(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                    <LogVolume>0.59</LogVolume>
                  </Log>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith<RecordedLog>();

            // Act + Assert
            if (entryPoint == EntryPoint.Sync)
            {
                using (IEnumerator<Row> running = Deserialization.Deserialize(document, options).GetEnumerator())
                {
                    Assert.True(running.MoveNext());
                    Assert.Throws<InvalidOperationException>(() => Deserialization.Deserialize(document, options).GetEnumerator());
                }
            }
            else
            {
                await using (IAsyncEnumerator<Row> running = Deserialization
                    .DeserializeAsync(new MemoryStream(Encoding.UTF8.GetBytes(document)), options)
                    .GetAsyncEnumerator())
                {
                    Assert.True(await running.MoveNextAsync());
                    Assert.Throws<InvalidOperationException>(() => Deserialization
                        .DeserializeAsync(new MemoryStream(Encoding.UTF8.GetBytes(document)), options)
                        .GetAsyncEnumerator());
                }
            }

            Assert.Single(EntryPoints.Read(entryPoint, document, options));
        }

        #region helpers
        /// <summary>
        /// Read one row through the entry point, then stop by disposing the enumerator
        /// </summary>
        private static async Task ReadFirstRowThenStopAsync(EntryPoint entryPoint, string xml, DeserializeOptions options)
        {
            if (entryPoint == EntryPoint.Sync)
            {
                using IEnumerator<Row> rows = Deserialization.Deserialize(xml, options).GetEnumerator();
                Assert.True(rows.MoveNext());
                return;
            }

            await using IAsyncEnumerator<Row> asyncRows = Deserialization
                .DeserializeAsync(new MemoryStream(Encoding.UTF8.GetBytes(xml)), options)
                .GetAsyncEnumerator();
            Assert.True(await asyncRows.MoveNextAsync());
        }
        #endregion
    }
}
