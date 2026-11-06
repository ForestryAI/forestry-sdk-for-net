using System.Text;
using Xunit;
using Forestry.Xml.Reading;

namespace Forestry.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Reader spec 001, User Story 3: the byte order mark (tasks.md T050-T054, #67) -------
        //
        // Test shells written before the code (C4), reviewed at T059. Documents are bytes here
        // (a BOM is not text), read whole and cut up with `ReadWhole`, `ReadEverySplit` and
        // `ReadInParts` (T003), and a malformed one with `ReadThrown`, `ReadThrownEverySplit` and
        // `ReadThrownInParts`. A UTF-8 BOM at the start is skipped without line tracking; another
        // BOM throws "not supported" at line 1, position 1; every incomplete start of a BOM halts
        // when reading is not completed (research.md R7). Today's generic "starts no markup"
        // (`ThrowUnexpectedMarkup`) has no category or markup, so it is asserted by its position.

        #region helpers
        private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

        private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

        private static byte[] Join(params byte[][] parts) => parts.SelectMany(part => part).ToArray();

        /// <summary>
        /// The document with and without a prefix give the same tokens, whole, split at every
        /// byte and in parts
        /// </summary>
        private static void AssertSameTokens(byte[] prefixed, byte[] plain)
        {
            List<ReadToken> expected = ReadWhole(plain);

            Assert.Equal(expected, ReadEverySplit(prefixed));
            Assert.Equal(expected, ReadInParts(prefixed));
        }

        /// <summary>
        /// The document throws, whole, split at every byte and in parts, an exception with the
        /// category, markup, line and position given
        /// </summary>
        private static void AssertBytesThrown(byte[] document, string? category, string? markup, long lineNumber, long linePosition)
        {
            XmlException thrown = ReadThrownEverySplit(document);
            ReadThrownInParts(document);

            Assert.Equal(category, thrown.Category);
            Assert.Equal(markup, thrown.Markup);
            Assert.Equal(lineNumber, thrown.LineNumber);
            Assert.Equal(linePosition, thrown.LinePosition);
        }

        /// <summary>
        /// A reader over the bytes with reading not completed halts at once, nothing committed
        /// </summary>
        private static void AssertHaltsCommittingNothing(byte[] bytes)
        {
            Utf8XmlReader reader = new(bytes, isReadingCompleted: false, new ReaderState());

            Assert.False(reader.Read());
            Assert.Equal(0, reader.Position);
            Assert.Equal(TokenType.None, reader.TokenType);
            Assert.Equal(ByteOrderMark.Unchecked, reader.ReaderState._byteOrderMark);
        }
        #endregion

        #region T050-T051 a UTF-8 BOM is skipped
        /// <summary>
        /// T050, US3-1 / FR-012: a UTF-8 BOM before a declaration gives the same tokens as without
        /// it, the declaration still first
        /// </summary>
        [Fact]
        public void Read_ForAUtf8ByteOrderMarkBeforeADeclaration_ItShould_SkipIt()
        {
            // Arrange
            byte[] plain = Utf8("<?xml version=\"1.0\"?><a/>");

            // Act, Assert
            AssertSameTokens(Join(Utf8Bom, plain), plain);
        }

        /// <summary>
        /// T050, US3-6 / FR-014 (developer, 2026-11-06): a declaration naming another encoding
        /// after a UTF-8 BOM contradicts the BOM, a fatal error by XML 1.0 §4.3.3: malformed
        /// (Expected + Encoding) at the name, not "not supported" as without a BOM; the BOM is not
        /// counted, so the name's <c>I</c> is at position 31
        /// </summary>
        [Fact]
        public void Read_ForADeclarationContradictingAUtf8ByteOrderMark_ItShould_ThrowExpected()
        {
            // Arrange
            byte[] document = Join(Utf8Bom, Utf8("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><a/>"));

            // Act, Assert
            AssertBytesThrown(document, "Expected", "Encoding", lineNumber: 1, linePosition: 31);
        }

        /// <summary>
        /// T051, US3-2 / FR-012: the BOM is not counted, so an error reports the same line and
        /// position as without it
        /// </summary>
        [Fact]
        public void Read_ForAUtf8ByteOrderMarkBeforeAnError_ItShould_ReportTheSamePosition()
        {
            // Arrange
            byte[] plain = Utf8("<a>&</a>");
            XmlException expected = ReadThrown(plain);

            // Act, Assert
            AssertBytesThrown(Join(Utf8Bom, plain), expected.Category, expected.Markup, expected.LineNumber!.Value, expected.LinePosition!.Value);
        }
        #endregion

        #region T052 a cut-off BOM halts, a second one is not skipped
        /// <summary>
        /// T052, US3-3 / C1: a BOM cut off by the end of a segment halts with nothing committed
        /// </summary>
        [Theory]
        [InlineData(new byte[] { 0xEF })]
        [InlineData(new byte[] { 0xEF, 0xBB })]
        public void Read_ForACutOffUtf8ByteOrderMark_ItShould_HaltCommittingNothing(byte[] bytes)
        {
            // Act, Assert
            AssertHaltsCommittingNothing(bytes);
        }

        /// <summary>
        /// T052: once the BOM is complete the tokens are those without it, however it is split,
        /// including a halt right after it
        /// </summary>
        [Fact]
        public void Read_ForAUtf8ByteOrderMarkSplitAnywhere_ItShould_GiveTheSameTokens()
        {
            // Arrange
            byte[] plain = Utf8("<a/>");
            byte[] prefixed = Join(Utf8Bom, plain);

            Utf8XmlReader first = new(Utf8Bom, isReadingCompleted: false, new ReaderState());
            Assert.False(first.Read());
            List<ReadToken> tokens = [];
            Utf8XmlReader next = new(prefixed.AsSpan((int)first.Position), isReadingCompleted: true, first.ReaderState);

            // Act
            ReadAll(ref next, tokens);

            // Assert
            Assert.Equal(3, first.Position);
            Assert.Equal(ByteOrderMark.Utf8, first.ReaderState._byteOrderMark);
            Assert.Equal(ReadWhole(plain), tokens);
            AssertSameTokens(prefixed, plain);
        }

        /// <summary>
        /// T052, R7: only the first BOM is skipped, also after a halt right after it: the second
        /// <c>EF</c> starts no markup, at line 1, position 1 (the first BOM is not counted)
        /// </summary>
        [Fact]
        public void Read_ForTwoUtf8ByteOrderMarks_ItShould_SkipOnlyTheFirst()
        {
            // Arrange
            byte[] document = Join(Utf8Bom, Utf8Bom, Utf8("<a/>"));

            Utf8XmlReader first = new(Utf8Bom, isReadingCompleted: false, new ReaderState());
            Assert.False(first.Read());
            Utf8XmlReader next = new(document.AsSpan(3), isReadingCompleted: true, first.ReaderState);

            // Act
            Exception? thrown = ReadThrown(ref next);

            // Assert
            XmlException exception = Assert.IsType<XmlException>(thrown);
            Assert.Null(exception.Category);
            Assert.Equal(1, exception.LinePosition);
            AssertBytesThrown(document, category: null, markup: null, lineNumber: 1, linePosition: 1);
        }
        #endregion

        #region T053 other byte order marks
        public static TheoryData<string, byte[]> OtherByteOrderMarks() => new()
        {
            { "UTF-16 big endian", Join([0xFE, 0xFF], Encoding.BigEndianUnicode.GetBytes("<a/>")) },
            { "UTF-16 little endian", Join([0xFF, 0xFE], Encoding.Unicode.GetBytes("<a/>")) },
            { "UTF-32 big endian", Join([0x00, 0x00, 0xFE, 0xFF], new UTF32Encoding(bigEndian: true, byteOrderMark: false).GetBytes("<a/>")) },
            { "UTF-32 little endian", Join([0xFF, 0xFE, 0x00, 0x00], Encoding.UTF32.GetBytes("<a/>")) },
        };

        /// <summary>
        /// T053, US3-4 / FR-013: another BOM throws "not supported" at line 1, position 1,
        /// however it is split (C1)
        /// </summary>
        [Theory]
        [MemberData(nameof(OtherByteOrderMarks))]
        public void Read_ForAnotherByteOrderMark_ItShould_ThrowNotSupported(string encoding, byte[] document)
        {
            // Arrange
            _ = encoding;

            // Act, Assert
            AssertBytesThrown(document, NotSupportedCategory, "ByteOrderMark", lineNumber: 1, linePosition: 1);
        }

        /// <summary>
        /// T053, R7: every incomplete start of another BOM halts with nothing committed when
        /// reading is not completed
        /// </summary>
        [Theory]
        [InlineData(new byte[] { 0xFE })]
        [InlineData(new byte[] { 0xFF })]
        [InlineData(new byte[] { 0x00 })]
        [InlineData(new byte[] { 0x00, 0x00 })]
        [InlineData(new byte[] { 0x00, 0x00, 0xFE })]
        public void Read_ForACutOffOtherByteOrderMark_ItShould_HaltCommittingNothing(byte[] bytes)
        {
            // Act, Assert
            AssertHaltsCommittingNothing(bytes);
        }

        /// <summary>
        /// T053, R7: with reading completed those starts are not a BOM and throw what an
        /// unrecognised start throws today: "starts no markup" at line 1, position 1
        /// </summary>
        [Theory]
        [InlineData(new byte[] { 0xFE })]
        [InlineData(new byte[] { 0xFF })]
        [InlineData(new byte[] { 0x00 })]
        [InlineData(new byte[] { 0x00, 0x00 })]
        [InlineData(new byte[] { 0x00, 0x00, 0xFE })]
        public void Read_ForAnIncompleteOtherByteOrderMarkWithReadingCompleted_ItShould_StartNoMarkup(byte[] bytes)
        {
            // Act
            XmlException thrown = ReadThrown(bytes);

            // Assert
            Assert.Null(thrown.Category);
            Assert.Equal(1, thrown.LineNumber);
            Assert.Equal(1, thrown.LinePosition);
        }
        #endregion

        #region T054 edge cases
        /// <summary>
        /// T054, spec edge case: a document of only a BOM is empty; a cut-off BOM with reading
        /// completed is incomplete; both throw Incomplete + RootElement at the end of the data
        /// (the skipped BOM is not counted, the cut-off bytes are)
        /// </summary>
        [Theory]
        [InlineData(new byte[] { 0xEF, 0xBB, 0xBF }, 1)]
        [InlineData(new byte[] { 0xEF, 0xBB }, 3)]
        public void Read_ForOnlyAByteOrderMark_ItShould_ThrowIncomplete(byte[] bytes, long linePosition)
        {
            // Act
            XmlException thrown = ReadThrown(bytes);

            // Assert
            Assert.Equal("Incomplete", thrown.Category);
            Assert.Equal("RootElement", thrown.Markup);
            Assert.Equal(1, thrown.LineNumber);
            Assert.Equal(linePosition, thrown.LinePosition);
        }

        /// <summary>
        /// T054: a UTF-8 BOM in text is plain bytes (≥ 0x80) of the Value
        /// </summary>
        [Fact]
        public void Read_ForAByteOrderMarkInText_ItShould_KeepItInTheValue()
        {
            // Arrange
            byte[] document = Join(Utf8("<a>"), Utf8Bom, Utf8("</a>"));

            // Act
            List<ReadToken> tokens = ReadEverySplit(document);
            ReadInParts(document);

            // Assert
            Assert.Equal(
                new[] { Plain(TokenType.Element, "a"), Plain(TokenType.Value, "﻿"), Plain(TokenType.ElementEnd, "a") },
                tokens);
        }

        /// <summary>
        /// T054: a UTF-8 BOM after the root element starts no markup, as any such byte today
        /// </summary>
        [Fact]
        public void Read_ForAByteOrderMarkAfterTheRoot_ItShould_StartNoMarkup()
        {
            // Act, Assert
            AssertBytesThrown(Join(Utf8("<a/>"), Utf8Bom), category: null, markup: null, lineNumber: 1, linePosition: 5);
        }
        #endregion
    }
}
