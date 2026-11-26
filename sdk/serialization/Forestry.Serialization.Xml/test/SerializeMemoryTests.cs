using System.Text;
using Forestry.Serialization.Tests.Fixtures;
using Xunit;
using Log = Forestry.Serialization.Xml.Tests.Fixtures.Log;

namespace Forestry.Serialization.Xml.Tests
{
    /// <summary>
    /// Spec 004 US2-4 / C8 / SC-005: serializing in a <c>foreach</c> over deserialized telemetries, with no
    /// pipeline, holds no more memory for ten times as many telemetries (tasks.md T020, quickstart 9, R55)
    /// </summary>
    /// <remarks>
    /// Test shell written before any code (C4). It lives here, not in the core's tests, because it reads a
    /// document, which needs a real format reader (C3). It writes with the core tests' string test format,
    /// linked into this project (<see cref="TestingSerializeOptions"/>), into a stream that keeps nothing
    /// and counts the lines it is given, one per telemetry. When US3 adds <c>MonitoringCollectionStream</c>,
    /// this test can count through its <c>Sequence</c> instead (T046).
    /// <para>
    /// Measured as in <see cref="MemoryTests"/>: live heap from the first telemetry to the last, after the
    /// document is generated, in the same collection so it never runs beside other tests, and as a
    /// performance test, run by <c>dotnet test -s Performance.runsettings</c>.
    /// </para>
    /// </remarks>
    [Collection(MemoryCollection.Name)]
    [Trait("Category", "Performance")]
    public sealed class SerializeMemoryTests
    {
        /// <summary>
        /// Live heap growth, in bytes, allowed while reading and writing, as in <see cref="MemoryTests"/>
        /// </summary>
        private const long AllowedGrowth = 1024 * 1024;

        // T020 - US2-4 / C8 / SC-005 (quickstart 9): 10,000 vs 100,000 Log telemetries read and written
        [Fact]
        public void Serialize_ForEachDeserializedTelemetry_ItShould_HoldNoMoreMemoryForTenTimesAsMany()
        {
            // Arrange
            byte[] tenThousand = GenerateLogs(10_000);
            byte[] hundredThousand = GenerateLogs(100_000);

            // Act
            long smallGrowth = GrowthWhileReadingAndWriting(tenThousand, 10_000);
            long largeGrowth = GrowthWhileReadingAndWriting(hundredThousand, 100_000);

            // Assert
            Assert.InRange(smallGrowth, long.MinValue, AllowedGrowth);
            Assert.InRange(largeGrowth, long.MinValue, AllowedGrowth);
            Assert.InRange(largeGrowth - smallGrowth, long.MinValue, AllowedGrowth);
        }

        /// <summary>
        /// Read every telemetry with <c>foreach</c> and serialize each into one counting stream, no pipeline;
        /// return how much the live heap grew between the first telemetry and the last
        /// </summary>
        /// <param name="document"></param>
        /// <param name="count">Number of telemetries in the document</param>
        /// <returns></returns>
        private static long GrowthWhileReadingAndWriting(byte[] document, int count)
        {
            DeserializeXmlOptions readOptions = new();
            readOptions.Telemetries.Add<Log>();
            TestingSerializeOptions writeOptions = new();
            writeOptions.Telemetries.Add<Log>();
            LineCountingStream output = new();

            long first = 0;
            int read = 0;

            foreach (Telemetry telemetry in TelemetrySerialization.Deserialize(document, readOptions))
            {
                TelemetrySerialization.Serialize(telemetry, output, writeOptions);
                read++;

                if (read == 1)
                {
                    first = LiveBytes();
                }
            }

            long last = LiveBytes();

            Assert.Equal(count, read);
            Assert.Equal(count, output.Lines);

            return last - first;
        }

        private static long LiveBytes()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            return GC.GetTotalMemory(forceFullCollection: true);
        }

        /// <summary>
        /// A UTF-8 document with <paramref name="count"/> <c>Log</c> telemetries, each with a distinct key, as
        /// in <see cref="MemoryTests"/>
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

        /// <summary>
        /// A write-only stream that keeps nothing and counts new lines, one per telemetry in the string
        /// test format
        /// </summary>
        private sealed class LineCountingStream : Stream
        {
            public long Lines { get; private set; }

            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Write(ReadOnlySpan<byte> buffer) => Lines += buffer.Count((byte)'\n');

            public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
