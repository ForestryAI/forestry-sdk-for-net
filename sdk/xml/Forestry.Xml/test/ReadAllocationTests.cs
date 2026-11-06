using System.Buffers;
using System.Text;
using Forestry.Xml.Reading;
using Xunit;

namespace Forestry.Xml.Tests
{
    /// <summary>
    /// Spec 001, SC-003 / C2: a read loop allocates nothing per token or per <c>Read()</c>
    /// (tasks.md T002, T016)
    /// </summary>
    /// <remarks>
    /// The first performance test of this package (research R9). What is measured is the bytes the
    /// current thread allocates from after the reader is constructed to after the last
    /// <c>Read()</c>: the document and its segments are built before measuring. The document is
    /// generated, not written one element per line (A4): ten thousand elements cannot be.
    /// <para>
    /// Slow, so it is a performance test: the test project's default run settings leave it out,
    /// and <c>dotnet test -s test/Performance.runsettings</c> runs only these, as the gate at the
    /// end of every user story (tasks.md).
    /// </para>
    /// </remarks>
    [Trait("Category", "Performance")]
    public sealed class ReadAllocationTests
    {
        private const int ElementCount = 10_000;

        private const int SegmentLength = 4 * 1024;

        // T002 - SC-003 / C2: a document without references, as one span and as 4 KB segments
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Read_ForADocumentWithoutReferences_ItShould_AllocateNothing(bool isSegmented)
        {
            // Arrange
            byte[] document = GenerateLogs(ElementCount, "Spruce and pine", "{0}");
            WarmUp(document, isSegmented);

            // Act
            long allocated = isSegmented ? AllocatedReadingSegments(document) : AllocatedReadingSpan(document);

            // Assert
            Assert.Equal(0, allocated);
        }

        // T016 - SC-003 / C2: the same document with references in text and attribute values
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Read_ForADocumentWithReferences_ItShould_AllocateNothing(bool isSegmented)
        {
            // Arrange
            byte[] document = GenerateLogs(ElementCount, "Spruce &amp; pine", "&#x31;{0}");
            WarmUp(document, isSegmented);

            // Act
            long allocated = isSegmented ? AllocatedReadingSegments(document) : AllocatedReadingSpan(document);

            // Assert
            Assert.Equal(0, allocated);
        }

        /// <summary>
        /// Read the document once before measuring: the first segmented read in a process
        /// allocates a few hundred bytes once (376 on net9, 432 on net10, measured 2026-11-05),
        /// whatever the document's size, and never again, so it is the runtime's one-time work,
        /// not an allocation per token or per <c>Read()</c>
        /// </summary>
        private static void WarmUp(byte[] document, bool isSegmented)
        {
            _ = isSegmented ? AllocatedReadingSegments(document) : AllocatedReadingSpan(document);
        }

        /// <summary>
        /// Bytes allocated by the current thread while reading the document to the end as one span
        /// </summary>
        private static long AllocatedReadingSpan(byte[] document)
        {
            Utf8XmlReader reader = new(document.AsSpan(), new ReaderOptions());

            long before = GC.GetAllocatedBytesForCurrentThread();
            while (reader.Read())
            {
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            return after - before;
        }

        /// <summary>
        /// Bytes allocated by the current thread while reading the document to the end as a
        /// sequence of <see cref="SegmentLength"/> byte segments
        /// </summary>
        private static long AllocatedReadingSegments(byte[] document)
        {
            ReadOnlySequence<byte> sequence = Segments(document, SegmentLength);
            Utf8XmlReader reader = new(sequence, new ReaderOptions());

            long before = GC.GetAllocatedBytesForCurrentThread();
            while (reader.Read())
            {
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            return after - before;
        }

        /// <summary>
        /// A UTF-8 document with <paramref name="count"/> <c>Log</c> elements, each with a
        /// <c>Key</c> attribute (<paramref name="key"/> formatted with the element's index) and a
        /// <c>Species</c> text
        /// </summary>
        internal static byte[] GenerateLogs(int count, string species, string key)
        {
            using MemoryStream stream = new(count * 64);

            stream.Write("<Root>"u8);

            for (int i = 0; i < count; i++)
            {
                string keyText = string.Format(key, i);
                stream.Write(Encoding.UTF8.GetBytes($"<Log Key=\"{keyText}\"><Species>{species}</Species></Log>"));
            }

            stream.Write("</Root>"u8);

            return stream.ToArray();
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

        /// <summary>
        /// The document as a sequence of segments of <paramref name="length"/> bytes, the last
        /// one shorter
        /// </summary>
        private static ReadOnlySequence<byte> Segments(byte[] document, int length)
        {
            Segment first = new(document.AsMemory(0, Math.Min(length, document.Length)));
            Segment last = first;

            for (int start = length; start < document.Length; start += length)
            {
                last = last.Append(document.AsMemory(start, Math.Min(length, document.Length - start)));
            }

            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }
    }
}
