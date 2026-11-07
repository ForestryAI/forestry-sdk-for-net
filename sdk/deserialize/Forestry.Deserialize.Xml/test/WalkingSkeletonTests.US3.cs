using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using Forestry.Deserialize.Xml.Tests.Fixtures;
using Forestry.Xml;
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
    /// <item>Input split anywhere, including inside a name or a value, gives the same rows (FR-010),
    /// whether the two parts come in one buffer of two segments or in two reads (T064 review).</item>
    /// <item><c>Deserialization.DeserializeAsync(Stream, DeserializeOptions, CancellationToken = default)</c>,
    /// the stream read through a <see cref="PipeReader"/> (R3).</item>
    /// <item>R18 (T083): the media decides whether the core skips a byte order mark; the XML media leaves it
    /// to <c>Utf8XmlReader</c>, which checks it against the declaration. (a), (b) and (d) already pass
    /// while the core still skips the mark, and guard that nothing breaks once it does not; (c) fails until
    /// T085, since the reader never sees the mark.</item>
    /// </list>
    /// The synchronous result is the reference each async result is compared with (SC-001). A read that
    /// waits for input that never comes fails after a timeout instead of hanging the test run.
    /// </remarks>
    public sealed class WalkingSkeletonTestsUS3
    {
        private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The UTF-8 byte order mark as text: encoded to UTF-8 it is EF BB BF
        /// </summary>
        private const string ByteOrderMark = "﻿";

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

        /// <summary>
        /// How T063 splits the document in two (T064 review, developer 2026-11-06)
        /// </summary>
        public enum Splitting
        {
            /// <summary>One read of a buffer made of two segments: the reader across segments</summary>
            TwoSegments,

            /// <summary>Two reads from a stream: a refill at the split, through the Stream overload</summary>
            TwoReads
        }

        // T063 - quickstart 7 / US3-3 / FR-010: the document split in two at every byte position (inside
        // names, values and tags), as one buffer of two segments or as two reads, gives the same rows
        [Theory]
        [InlineData(Splitting.TwoSegments)]
        [InlineData(Splitting.TwoReads)]
        public async Task DeserializeAsync_ForADocumentSplitAtEveryBytePosition_ItShould_YieldTheSameRows(Splitting splitting)
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
            List<string> failedSplits = [];

            // Act
            for (int split = 1; split < utf8.Length; split++)
            {
                IAsyncEnumerable<Row> rows = splitting switch
                {
                    Splitting.TwoSegments => Deserialization.DeserializeAsync(PipeReader.Create(TwoSegments(utf8, split)), OptionsWith<Log>()),
                    Splitting.TwoReads => Deserialization.DeserializeAsync(new TwoReadsStream(utf8, split), OptionsWith<Log>()),
                    _ => throw new ArgumentOutOfRangeException(nameof(splitting))
                };

                try
                {
                    if (!SameLogs(expected, await ReadAllAsync(rows)))
                    {
                        failedSplits.Add($"{split}: other rows");
                    }
                }
                catch (Exception exception)
                {
                    failedSplits.Add($"{split}: {exception.GetType().Name}");
                }
            }

            // Assert
            Assert.Empty(failedSplits);
        }

        // T083 (a) - R18: a byte order mark before a UTF-8 declaration is the reader's to read; the rows
        // are those of document 1
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAByteOrderMarkAndAUtf8Declaration_ItShould_YieldTheRows(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <?xml version="1.0" encoding="UTF-8"?>
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

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, ByteOrderMark + document, OptionsWith<Log>());

            // Assert
            Assert.Equal([1, 2], rows.Select(row => row.AsType<Log>().LogKey));
            Assert.Equal([0.59m, 0.50m], rows.Select(row => row.AsType<Log>().LogVolume));
        }

        // T083 (b) - R18: a byte order mark with no declaration gives the same rows
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAByteOrderMarkWithoutADeclaration_ItShould_YieldTheRows(EntryPoint entryPoint)
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

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, ByteOrderMark + document, OptionsWith<Log>());

            // Assert
            Assert.Equal([1, 2], rows.Select(row => row.AsType<Log>().LogKey));
            Assert.Equal([0.59m, 0.50m], rows.Select(row => row.AsType<Log>().LogVolume));
        }

        // T083 (c) - R18: a byte order mark contradicting the declaration's encoding throws the reader's
        // own error (Expected Encoding, as Forestry.Xml's T051), not the generic unsupported encoding the
        // reader reports when it never saw the mark
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAByteOrderMarkContradictingTheDeclaration_ItShould_ThrowTheReadersError(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <?xml version="1.0" encoding="ISO-8859-1"?>
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                    <LogVolume>0.59</LogVolume>
                  </Log>
                </Root>
                """;

            // Act
            XmlException thrown = Assert.Throws<XmlException>(() => EntryPoints.Read(entryPoint, ByteOrderMark + document, OptionsWith<Log>()));

            // Assert
            Assert.Equal("Expected", thrown.Category);
            Assert.Equal("Encoding", thrown.Markup);
        }

        // T083 (d) - R18: a byte order mark split across the first reads of a stream (after its first or
        // second byte, or right after it) gives the rows
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task DeserializeAsync_ForAByteOrderMarkSplitAcrossReads_ItShould_YieldTheRows(int split)
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
            byte[] utf8 = Encoding.UTF8.GetBytes(ByteOrderMark + document);

            // Act
            List<Row> rows = await ReadAllAsync(Deserialization.DeserializeAsync(new TwoReadsStream(utf8, split), OptionsWith<Log>()));

            // Assert
            Assert.Equal([1, 2], rows.Select(row => row.AsType<Log>().LogKey));
            Assert.Equal([0.59m, 0.50m], rows.Select(row => row.AsType<Log>().LogVolume));
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

        /// <summary>
        /// A stream handing out the bytes in two reads, the second starting at the split
        /// </summary>
        private sealed class TwoReadsStream(byte[] utf8, int split) : Stream
        {
            private int _position;

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

            public override int Read(Span<byte> buffer)
            {
                int end = _position < split ? split : utf8.Length;
                int length = Math.Min(buffer.Length, end - _position);

                utf8.AsSpan(_position, length).CopyTo(buffer);
                _position += length;
                return length;
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(Read(buffer.Span));

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                Task.FromResult(Read(buffer.AsSpan(offset, count)));

            public override void Flush() { }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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
