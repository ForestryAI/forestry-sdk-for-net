using System.Text;
using Forestry.Deserialize.Xml.Tests.Fixtures;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    /// <summary>
    /// Spec 001, SC-003 / C1: memory does not grow with the number of rows (tasks.md T070, quickstart 15)
    /// </summary>
    /// <remarks>
    /// Test shell written before T072 (C4). What is measured is the live heap that grows while rows are
    /// read and discarded, from the first row to the last, not the input: the memory entry point holds
    /// the whole document by design, so the document is generated before measuring. If the core kept a
    /// reference to every handed-out <see cref="Log"/>, a million rows would hold tens of megabytes; a
    /// thousand would not.
    /// <para>
    /// The documents are generated, not written one element per line (A4): a million rows cannot be.
    /// Live heap is process-wide, so this class runs alone (<see cref="MemoryCollection"/>). The async
    /// case, where the input is not held either, comes with US3 (T066).
    /// </para>
    /// <para>
    /// Slow (10-20 seconds per framework), so it is a performance test: the test project's default
    /// run settings leave it out, and <c>dotnet test -s Performance.runsettings</c> runs only these, as
    /// the gate at the end of every user story (tasks.md).
    /// </para>
    /// </remarks>
    [Collection(MemoryCollection.Name)]
    [Trait("Category", "Performance")]
    public sealed class MemoryTests
    {
        /// <summary>
        /// Live heap growth, in bytes, allowed while reading; far below what a million kept rows hold
        /// </summary>
        private const long AllowedGrowth = 1024 * 1024;

        // T070 - quickstart 15 / SC-003 / C1: 1,000 vs 1,000,000 Log rows, rows discarded
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async, Skip = "US3 (T066)")]
        public void Deserialize_ForAMillionDiscardedRows_ItShould_HoldNoMoreMemoryThanForAThousand(EntryPoint entryPoint)
        {
            // Arrange
            byte[] thousand = GenerateLogs(1_000);
            byte[] million = GenerateLogs(1_000_000);

            // Act
            long thousandGrowth = GrowthWhileReading(entryPoint, thousand, 1_000);
            long millionGrowth = GrowthWhileReading(entryPoint, million, 1_000_000);

            // Assert
            Assert.InRange(thousandGrowth, long.MinValue, AllowedGrowth);
            Assert.InRange(millionGrowth, long.MinValue, AllowedGrowth);
            Assert.InRange(millionGrowth - thousandGrowth, long.MinValue, AllowedGrowth);
        }

        /// <summary>
        /// Read every row, discarding each, and return how much the live heap grew between the first
        /// row and the last, while the enumeration is still open
        /// </summary>
        /// <param name="entryPoint"></param>
        /// <param name="document"></param>
        /// <param name="count">Number of rows in the document</param>
        /// <returns></returns>
        private static long GrowthWhileReading(EntryPoint entryPoint, byte[] document, int count)
        {
            DeserializeXmlOptions options = new();
            options.Rows.Add<Log>();

            using IEnumerator<Row> rows = Rows(entryPoint, document, options).GetEnumerator();

            Assert.True(rows.MoveNext());
            long first = LiveBytes();
            int read = 1;

            while (read < count && rows.MoveNext())
            {
                read++;
            }

            long last = LiveBytes();

            Assert.Equal(count, read);
            Assert.False(rows.MoveNext());

            return last - first;
        }

        private static IEnumerable<Row> Rows(EntryPoint entryPoint, byte[] document, DeserializeOptions options) =>
            entryPoint switch
            {
                EntryPoint.Sync => Deserialization.Deserialize(document, options),
                EntryPoint.Async => throw new NotSupportedException("The async entry point comes with US3 (T066)"),
                _ => throw new ArgumentOutOfRangeException(nameof(entryPoint))
            };

        private static long LiveBytes()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            return GC.GetTotalMemory(forceFullCollection: true);
        }

        /// <summary>
        /// A UTF-8 document with <paramref name="count"/> <c>Log</c> rows, each with a distinct key
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        private static byte[] GenerateLogs(int count)
        {
            using MemoryStream stream = new(count * 64);

            stream.Write("<Root>"u8);

            for (int i = 0; i < count; i++)
            {
                stream.Write(Encoding.UTF8.GetBytes($"<Log><LogKey>{i}</LogKey><LogVolume>0.59</LogVolume></Log>"));
            }

            stream.Write("</Root>"u8);

            return stream.ToArray();
        }
    }

    /// <summary>
    /// Memory tests measure the process-wide live heap, so they never run beside other tests
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class MemoryCollection
    {
        public const string Name = "Memory";
    }
}
