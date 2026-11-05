using System.Buffers;
using System.Text;
using Xunit;
using Forestry.Xml.Reading;

namespace Forestry.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Shared helpers, spec 001 (tasks.md T004) ------------------------------------------
        //
        // Every document in spec 001 is read whole and cut up (C1, SC-001): `ReadWhole` reads it
        // as one span, `ReadEverySplit` as two segments split at every byte position, and
        // `ReadInParts` one more byte at a time, a new reader per halt built from the previous
        // `ReaderState`. The three give identical tokens, values and `HasEscapedValue`. A
        // malformed document throws the same exception (category, markup, line and position)
        // whichever way it is read: `ReadThrown`, `ReadThrownEverySplit`, `ReadThrownInParts`.

        #region segments
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
        /// Byte sequence with one segment per part, empty parts become empty segments
        /// </summary>
        private static ReadOnlySequence<byte> Sequence(params string[] parts)
        {
            Segment first = new(Encoding.UTF8.GetBytes(parts[0]));
            Segment last = first;
            for (int i = 1; i < parts.Length; i++)
            {
                last = last.Append(Encoding.UTF8.GetBytes(parts[i]));
            }

            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }

        /// <summary>
        /// The text encoded as UTF-8 once, then split into segments at byte offsets, so a split
        /// can fall inside a multi-byte character; a repeated offset gives an empty segment
        /// </summary>
        private static ReadOnlySequence<byte> SequenceSplitAt(string text, params int[] byteOffsets) =>
            SequenceSplitAt(Encoding.UTF8.GetBytes(text), byteOffsets);

        /// <summary>
        /// The bytes split into segments at byte offsets; a repeated offset gives an empty segment
        /// </summary>
        private static ReadOnlySequence<byte> SequenceSplitAt(byte[] bytes, params int[] byteOffsets)
        {
            Segment first = new(bytes.AsMemory(0, byteOffsets.Length > 0 ? byteOffsets[0] : bytes.Length));
            Segment last = first;
            for (int i = 0; i < byteOffsets.Length; i++)
            {
                int start = byteOffsets[i];
                int end = i + 1 < byteOffsets.Length ? byteOffsets[i + 1] : bytes.Length;
                last = last.Append(bytes.AsMemory(start, end - start));
            }

            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }
        #endregion

        #region tokens
        /// <summary>
        /// One token as a test compares it: its type, its value as text and the references flag
        /// </summary>
        internal readonly record struct ReadToken(TokenType Type, string Value, bool HasEscapedValue)
        {
            public override string ToString() => $"{Type} '{Value}'{(HasEscapedValue ? " escaped" : "")}";
        }

        /// <summary>
        /// The current token, its value copied out (a value sequence joined)
        /// </summary>
        private static ReadToken Token(ref Utf8XmlReader reader)
        {
            byte[] value = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.Value.ToArray();
            return new ReadToken(reader.TokenType, Encoding.UTF8.GetString(value), reader.HasEscapedValue);
        }

        /// <summary>
        /// Every token a reader gives until it halts or reaches the end
        /// </summary>
        private static void ReadAll(ref Utf8XmlReader reader, List<ReadToken> tokens)
        {
            while (reader.Read())
            {
                tokens.Add(Token(ref reader));
            }
        }
        #endregion

        #region read whole and cut up
        /// <summary>
        /// Every token of the document read as one span, reading completed
        /// </summary>
        private static List<ReadToken> ReadWhole(byte[] document, ReaderOptions options = default)
        {
            List<ReadToken> tokens = [];
            Utf8XmlReader reader = new(document.AsSpan(), isReadingCompleted: true, new ReaderState(options));
            ReadAll(ref reader, tokens);

            return tokens;
        }

        /// <summary>
        /// Every token of the document read as two segments split at every byte position, reading
        /// completed; asserts each split gives <see cref="ReadWhole"/>'s tokens, and returns them
        /// </summary>
        private static List<ReadToken> ReadEverySplit(byte[] document, ReaderOptions options = default)
        {
            List<ReadToken> whole = ReadWhole(document, options);

            for (int split = 0; split <= document.Length; split++)
            {
                List<ReadToken> tokens = ReadSplitAt(document, split, options);
                Assert.True(whole.SequenceEqual(tokens), $"Split at byte {split}: [{string.Join(", ", tokens)}] instead of [{string.Join(", ", whole)}]");
            }

            return whole;
        }

        private static List<ReadToken> ReadSplitAt(byte[] document, int split, ReaderOptions options)
        {
            List<ReadToken> tokens = [];
            Utf8XmlReader reader = new(SequenceSplitAt(document, split), isReadingCompleted: true, new ReaderState(options));
            ReadAll(ref reader, tokens);

            return tokens;
        }

        /// <summary>
        /// Every token of the document fed one more byte at a time with reading not completed, a
        /// new reader per halt built from the previous <see cref="ReaderState"/> over the bytes
        /// from the committed position on, the last part read with reading completed; asserts it
        /// gives <see cref="ReadWhole"/>'s tokens, and returns them
        /// </summary>
        private static List<ReadToken> ReadInParts(byte[] document, ReaderOptions options = default)
        {
            List<ReadToken> whole = ReadWhole(document, options);
            List<ReadToken> tokens = ReadParts(document, options);

            Assert.True(whole.SequenceEqual(tokens), $"In parts: [{string.Join(", ", tokens)}] instead of [{string.Join(", ", whole)}]");

            return whole;
        }

        private static List<ReadToken> ReadParts(byte[] document, ReaderOptions options)
        {
            List<ReadToken> tokens = [];
            ReaderState state = new(options);
            int committed = 0;

            for (int end = Math.Min(1, document.Length); ; end = Math.Max(end + 1, committed + 1))
            {
                end = Math.Min(end, document.Length);
                bool isReadingCompleted = end == document.Length;

                Utf8XmlReader reader = new(document.AsSpan(committed, end - committed), isReadingCompleted, state);
                ReadAll(ref reader, tokens);

                committed += (int)reader.Position;
                state = reader.ReaderState;

                if (isReadingCompleted)
                {
                    return tokens;
                }
            }
        }
        #endregion

        #region throw whole and cut up
        /// <summary>
        /// The exception of reading the document as one span; fails when it reads without one
        /// </summary>
        private static XmlException ReadThrown(byte[] document, ReaderOptions options = default)
        {
            try
            {
                ReadWhole(document, options);
            }
            catch (XmlException exception)
            {
                return exception;
            }

            Assert.Fail("The document read without an XmlException");
            return null!;
        }

        /// <summary>
        /// Asserts the document throws, split at every byte position, the exception
        /// <see cref="ReadThrown"/> throws (same category, markup, line and position), and
        /// returns that one
        /// </summary>
        private static XmlException ReadThrownEverySplit(byte[] document, ReaderOptions options = default)
        {
            XmlException whole = ReadThrown(document, options);

            for (int split = 0; split <= document.Length; split++)
            {
                XmlException? thrown = null;
                try
                {
                    ReadSplitAt(document, split, options);
                }
                catch (XmlException exception)
                {
                    thrown = exception;
                }

                AssertSameException(whole, thrown, $"Split at byte {split}");
            }

            return whole;
        }

        /// <summary>
        /// Asserts the document throws, fed in parts as <see cref="ReadInParts"/> does, the
        /// exception <see cref="ReadThrown"/> throws, and returns that one
        /// </summary>
        private static XmlException ReadThrownInParts(byte[] document, ReaderOptions options = default)
        {
            XmlException whole = ReadThrown(document, options);

            XmlException? thrown = null;
            try
            {
                ReadParts(document, options);
            }
            catch (XmlException exception)
            {
                thrown = exception;
            }

            AssertSameException(whole, thrown, "In parts");

            return whole;
        }

        private static void AssertSameException(XmlException expected, XmlException? actual, string how)
        {
            Assert.True(actual is not null, $"{how}: read without an XmlException");
            Assert.True(
                expected.Category == actual!.Category && expected.Markup == actual.Markup
                    && expected.LineNumber == actual.LineNumber && expected.LinePosition == actual.LinePosition,
                $"{how}: {actual.Category} {actual.Markup} at {actual.LineNumber}:{actual.LinePosition} instead of {expected.Category} {expected.Markup} at {expected.LineNumber}:{expected.LinePosition}");
        }
        #endregion
    }
}
