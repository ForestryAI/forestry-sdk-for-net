using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using Forestry.Deserialize.Xml.Tests.Fixtures;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    /// <summary>
    /// Spec 001, User Story 3: synchronous or asynchronous input (tasks.md T062-T063, quickstart 6-7)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4), so they do not compile until T066 declares the surface
    /// they assume. For the review gate (T064), the assumed surface:
    /// <list type="bullet">
    /// <item><c>Deserialization.DeserializeAsync(PipeReader, DeserializeOptions, CancellationToken = default)</c>
    /// returning <see cref="IAsyncEnumerable{T}"/> of <see cref="Row"/> (FR-001, R3).</item>
    /// <item>A row is handed out when its end tag is read, without waiting for more input (FR-009).</item>
    /// <item>Input split anywhere, including inside a name or a value, gives the same rows (FR-010).</item>
    /// </list>
    /// The synchronous result is the reference each async result is compared with (SC-001). A read that
    /// waits for input that never comes fails after a timeout instead of hanging the test run.
    /// </remarks>
    public sealed class WalkingSkeletonTestsUS3
    {
        private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);

        private static DeserializeXmlOptions OptionsWith<TRow>() where TRow : Row
        {
            DeserializeXmlOptions options = new();
            options.Rows.Add<TRow>();
            return options;
        }

        // T062 - quickstart 6 / US3-1 / FR-010: a pipe gives the same rows as synchronous input
        [Fact]
        public async Task DeserializeAsync_ForADocumentThroughAPipe_ItShould_YieldTheSameRowsAsSync()
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
            List<Row> expected = Deserialization.Deserialize(document, OptionsWith<Log>()).ToList();
            Pipe pipe = new();
            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(document));
            await pipe.Writer.CompleteAsync();

            // Act
            List<Row> rows = await ReadAllAsync(Deserialization.DeserializeAsync(pipe.Reader, OptionsWith<Log>()));

            // Assert
            Assert.True(SameLogs(expected, rows));
            Assert.Equal(2, rows.Count);
        }

        // T062 - quickstart 6 / US3-2 / FR-009: the first row is handed out at its end tag, before the
        // second Log has even been written to the pipe
        [Fact]
        public async Task DeserializeAsync_ForAnEndedLog_ItShould_HandItOutBeforeTheNextLogIsWritten()
        {
            // Arrange
            const string firstLog = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                    <LogVolume>0.59</LogVolume>
                  </Log>

                """;
            const string rest = """
                  <Log>
                    <LogKey>2</LogKey>
                    <LogVolume>0.50</LogVolume>
                  </Log>
                </Root>
                """;
            Pipe pipe = new();
            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(firstLog));
            await using IAsyncEnumerator<Row> rows = Deserialization.DeserializeAsync(pipe.Reader, OptionsWith<Log>()).GetAsyncEnumerator();

            // Act
            bool hasFirst = await rows.MoveNextAsync().AsTask().WaitAsync(s_timeout);
            Log first = rows.Current.AsType<Log>();

            await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(rest));
            await pipe.Writer.CompleteAsync();

            bool hasSecond = await rows.MoveNextAsync().AsTask().WaitAsync(s_timeout);
            Log second = rows.Current.AsType<Log>();
            bool hasMore = await rows.MoveNextAsync().AsTask().WaitAsync(s_timeout);

            // Assert
            Assert.True(hasFirst);
            Assert.Equal(1, first.LogKey);
            Assert.Equal(0.59m, first.LogVolume);
            Assert.True(hasSecond);
            Assert.Equal(2, second.LogKey);
            Assert.False(hasMore);
        }

        // T063 - quickstart 7 / US3-3 / FR-010: the document in two buffers, split at every byte position
        // (inside names, values and tags), gives the same rows
        [Fact]
        public async Task DeserializeAsync_ForADocumentSplitAtEveryBytePosition_ItShould_YieldTheSameRows()
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
            byte[] utf8 = Encoding.UTF8.GetBytes(document);
            List<Row> expected = Deserialization.Deserialize(document, OptionsWith<Log>()).ToList();
            List<int> failedSplits = [];

            // Act
            for (int split = 1; split < utf8.Length; split++)
            {
                PipeReader reader = PipeReader.Create(TwoSegments(utf8, split));
                List<Row> rows = await ReadAllAsync(Deserialization.DeserializeAsync(reader, OptionsWith<Log>()));

                if (!SameLogs(expected, rows))
                {
                    failedSplits.Add(split);
                }
            }

            // Assert
            Assert.Empty(failedSplits);
        }

        #region helpers
        private static async Task<List<Row>> ReadAllAsync(IAsyncEnumerable<Row> rows)
        {
            List<Row> result = [];
            using CancellationTokenSource timeout = new(s_timeout);

            await foreach (Row row in rows.WithCancellation(timeout.Token))
            {
                result.Add(row);
            }

            return result;
        }

        private static bool SameLogs(List<Row> expected, List<Row> actual) =>
            expected.Count == actual.Count &&
            expected.Zip(actual).All(pair =>
                pair.First.AsType<Log>().LogKey == pair.Second.AsType<Log>().LogKey &&
                pair.First.AsType<Log>().LogVolume == pair.Second.AsType<Log>().LogVolume);

        /// <summary>
        /// The bytes as two segments, the second starting at <paramref name="split"/>
        /// </summary>
        private static ReadOnlySequence<byte> TwoSegments(byte[] utf8, int split)
        {
            Segment first = new(utf8.AsMemory(0, split));
            Segment last = first.Append(utf8.AsMemory(split));

            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }

        private sealed class Segment : ReadOnlySequenceSegment<byte>
        {
            public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;

            public Segment Append(ReadOnlyMemory<byte> memory)
            {
                Segment next = new(memory) { RunningIndex = RunningIndex + Memory.Length };
                Next = next;
                return next;
            }
        }
        #endregion
    }
}
