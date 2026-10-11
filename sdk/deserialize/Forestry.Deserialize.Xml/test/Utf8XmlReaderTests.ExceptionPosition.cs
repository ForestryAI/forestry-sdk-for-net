using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Exception position, from #43 ----------------------------------------------------
        //
        // An XML exception reports the line number and line position of the offending byte, not
        // the reader's committed position: a throw site passes an offset (bytes from the
        // committed position to the offending byte) and the exception walks the bytes in between
        // with the same line rules as reading (Utf8Reader.Track). Incomplete reports the end of
        // the data, except an opaque value missing its ending terminal, which reports its start.
        //
        // Three groups:
        // - line rules: Utf8Reader.Track over the carriage return and line feed table
        // - the walk: GetXmlException with an offset, within one segment and across segments
        // - throw sites: each site's reported position (1-based), and nothing committed
        //
        // Positions in exceptions are 1-based (#42), Track's are 0-based.

        #region helpers
        /// <summary>
        /// The thrown exception is an XML exception at the 1-based line number and line position
        /// </summary>
        private static void AssertXmlExceptionAt(Exception? thrown, long lineNumber, long linePosition)
        {
            XmlException exception = Assert.IsType<XmlException>(thrown);
            Assert.Equal(lineNumber, exception.LineNumber);
            Assert.Equal(linePosition, exception.LinePosition);
        }

        /// <summary>
        /// The exception <see cref="Throwing.GetXmlException"/> returns for an offset ahead of a
        /// reader committed at its start
        /// </summary>
        private static XmlException GetXmlExceptionAtOffset(ref Utf8XmlReader reader, long offset) =>
            Throwing.GetXmlException(ref reader, Throwing.Category.Expected, Throwing.Markup.Spacing, null, default, offset);
        #endregion

        #region line rules
        /// <summary>
        /// Only a line feed ends a line: it restarts the line position, every other byte
        /// (carriage return included) adds one
        /// </summary>
        [Theory]
        [InlineData("", 0, 0)]
        [InlineData("ab", 0, 2)]
        [InlineData("ab\n", 1, 0)]
        [InlineData("ab\r\n", 1, 0)]
        [InlineData("ab\r", 0, 3)]
        [InlineData("\n\n  ", 2, 2)]
        [InlineData("a\rb", 0, 3)]
        public void Track_ForBytes_ItShould_FollowTheLineRules(
            string text,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Act
            (long lineNumber, long linePosition) = Utf8Reader.Track(0, 0, Encoding.UTF8.GetBytes(text));

            // Assert
            Assert.Equal(expectedLineNumber, lineNumber);
            Assert.Equal(expectedLinePosition, linePosition);
        }

        /// <summary>
        /// Tracking continues from a line number and line position: the position adds up on the
        /// same line and restarts after a line feed
        /// </summary>
        [Theory]
        [InlineData("ab", 3, 9)]
        [InlineData("a\nbc", 4, 2)]
        public void Track_FromAPosition_ItShould_ContinueFromIt(
            string text,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Act
            (long lineNumber, long linePosition) = Utf8Reader.Track(3, 7, Encoding.UTF8.GetBytes(text));

            // Assert
            Assert.Equal(expectedLineNumber, lineNumber);
            Assert.Equal(expectedLinePosition, linePosition);
        }
        #endregion

        #region the walk
        /// <summary>
        /// The walk covers the bytes before the offending byte (#43's table, + 1 in the exception)
        /// </summary>
        [Theory]
        [InlineData("abc", 2, 1, 3)]
        [InlineData("ab\nc", 3, 2, 1)]
        [InlineData("ab\r\nc", 4, 2, 1)]
        [InlineData("ab\r\n", 3, 1, 4)]
        [InlineData("\n\n  c", 4, 3, 3)]
        [InlineData("ab\rc", 3, 1, 4)]
        [InlineData("abc", 0, 1, 1)]
        public void Throwing_ForAnOffset_ItShould_ReportTheOffendingByte(
            string text,
            long offset,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));

            // Act
            XmlException exception = GetXmlExceptionAtOffset(ref reader, offset);

            // Assert
            Assert.Equal(expectedLineNumber, exception.LineNumber);
            Assert.Equal(expectedLinePosition, exception.LinePosition);
        }

        /// <summary>
        /// The walk starts from the committed line number and line position
        /// </summary>
        [Fact]
        public void Throwing_ForAnOffsetFromACommittedPosition_ItShould_WalkFromIt()
        {
            // Arrange - e.g. committed at line 3, position 8 (0-based 2, 7)
            Utf8XmlReader reader = new(" \n  c"u8, isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None, lineNumber: 2, linePosition: 7));

            // Act
            XmlException same = GetXmlExceptionAtOffset(ref reader, 1);
            XmlException next = GetXmlExceptionAtOffset(ref reader, 4);

            // Assert
            Assert.Equal((3L, 9L), (same.LineNumber!.Value, same.LinePosition!.Value));
            Assert.Equal((4L, 3L), (next.LineNumber!.Value, next.LinePosition!.Value));
        }

        /// <summary>
        /// Across segments the line number and line position carry from one segment to the next,
        /// wherever the boundary falls (between <c>\r</c> and <c>\n</c>, inside spacing, empty
        /// segments)
        /// </summary>
        [Theory]
        [InlineData(new[] { "ab", "c" }, 2, 1, 3)]
        [InlineData(new[] { "ab\r", "\nc" }, 4, 2, 1)]
        [InlineData(new[] { "a\n", " ", " c" }, 4, 2, 3)]
        [InlineData(new[] { "a", "", "\n", "", "c" }, 2, 2, 1)]
        public void Throwing_ForAnOffsetStraddlingSegments_ItShould_ReportTheOffendingByte(
            string[] parts,
            long offset,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));

            // Act
            XmlException exception = GetXmlExceptionAtOffset(ref reader, offset);

            // Assert
            Assert.Equal(expectedLineNumber, exception.LineNumber);
            Assert.Equal(expectedLinePosition, exception.LinePosition);
        }

        /// <summary>
        /// The walk never commits: the reader keeps its position, line number and line position
        /// </summary>
        [Fact]
        public void Throwing_ForAnOffset_ItShould_NotCommit()
        {
            // Arrange
            Utf8XmlReader reader = new("ab\nc"u8, isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));

            // Act
            GetXmlExceptionAtOffset(ref reader, 3);

            // Assert
            Assert.Equal(0, reader.Position);
            Assert.Equal(0, reader.ReaderState._lineNumber);
            Assert.Equal(0, reader.ReaderState._linePosition);
        }
        #endregion

        #region throw sites - end of data
        /// <summary>
        /// No root element: the end of the data, after the skipped spacing
        /// </summary>
        [Fact]
        public void Throwing_ForNoRootElement_ItShould_ReportTheEndOfData()
        {
            // Arrange
            Utf8XmlReader reader = new("\n "u8, isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 2, 2);
        }

        /// <summary>
        /// Element not ended: the end of the data, after the spacing left in content
        /// </summary>
        /// <remarks>
        /// Not through content ready's <c>&gt;</c>: its skip doesn't add to the line position
        /// yet (bug #30), so the end of the data would be reported one byte early
        /// </remarks>
        [Fact]
        public void Throwing_ForElementNotEnded_ItShould_ReportTheEndOfData()
        {
            // Arrange
            Utf8XmlReader reader = new("\n   "u8, isReadingCompleted: true, ContextState(Context.Content, TokenType.Element));

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 2, 4);
        }

        /// <summary>
        /// Empty element end S3 (#35): <c>/</c> is the last byte, the end of the data is after it
        /// </summary>
        [Theory]
        [InlineData(0, 1, 2)]
        [InlineData(5, 1, 7)]
        public void Throwing_ForSlashAtTheEndOfData_ItShould_ReportTheEndOfData(
            long linePosition,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new("/"u8, isReadingCompleted: true, StartTagState(TokenType.Element, hasSkippedSpacingInStartTag: false, linePosition: linePosition));

            // Act
            Exception? thrown = ReadEmptyElementEndThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, expectedLineNumber, expectedLinePosition);
            Assert.Equal(0, reader.Position);
        }

        /// <summary>
        /// Read name S9 (#38): no name when reading is completed, at the end of the data
        /// </summary>
        [Fact]
        public void Throwing_ForNoNameWhenReadingCompleted_ItShould_ReportTheEndOfData()
        {
            // Arrange
            Utf8XmlReader reader = new(""u8, isReadingCompleted: true, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadNameThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 1, 1);
        }
        #endregion

        #region throw sites - start of the opaque value
        /// <summary>
        /// Opaque value S4 (#24): reading completed inside the opaque value, reported at its
        /// start, not the end of the data
        /// </summary>
        [Theory]
        [InlineData("<!-- comment\n more")]
        [InlineData("<?pi data?")]
        [InlineData("<!DOCTYPE root\n")]
        public void Throwing_ForOpaqueValueEndingTerminalMissing_ItShould_ReportItsStart(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None, lineNumber: 2, linePosition: 4));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 3, 5);
            Assert.Equal(0, reader.Position);
        }

        /// <summary>
        /// Opaque value S4 straddling segments
        /// </summary>
        [Fact]
        public void Throwing_ForOpaqueValueEndingTerminalMissingStraddlingSegments_ItShould_ReportItsStart()
        {
            // Arrange
            Utf8XmlReader reader = new(Sequence("<!-- com", "ment\n -"), isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None, lineNumber: 2, linePosition: 4));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 3, 5);
        }
        #endregion

        #region throw sites - known offset
        /// <summary>
        /// Empty element end S4 (#35): the offending byte is the one after <c>/</c>
        /// </summary>
        [Theory]
        [InlineData("/a", 0, 2)]
        [InlineData("/\n>", 0, 2)]
        [InlineData("/ >", 6, 8)]
        public void Throwing_ForSlashNotFollowedByGreaterThan_ItShould_ReportTheByteAfterTheSlash(
            string text,
            long linePosition,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, StartTagState(TokenType.Element, hasSkippedSpacingInStartTag: false, linePosition: linePosition));

            // Act
            Exception? thrown = ReadEmptyElementEndThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 1, expectedLinePosition);
            Assert.Equal(0, reader.Position);
        }

        /// <summary>
        /// Empty element end S4 straddling segments
        /// </summary>
        [Fact]
        public void Throwing_ForSlashNotFollowedByGreaterThanStraddlingSegments_ItShould_ReportTheByteAfterTheSlash()
        {
            // Arrange
            Utf8XmlReader reader = new(Sequence("/", "", "a>"), isReadingCompleted: true, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadEmptyElementEndThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 1, 2);
        }

        /// <summary>
        /// Read name S5 (#38): the unsupported byte, after the name characters before it
        /// </summary>
        [Theory]
        [InlineData("é>", 1)]
        [InlineData("aé>", 2)]
        [InlineData("Stemé", 5)]
        public void Throwing_ForUnsupportedNameCharacter_ItShould_ReportTheUnsupportedByte(
            string text,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: false, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadNameThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 1, expectedLinePosition);
            Assert.Equal(0, reader.Position);
        }

        /// <summary>
        /// Read name S5 straddling segments: the name characters of every segment before it
        /// </summary>
        [Theory]
        [InlineData(new[] { "Stem", "é>" }, 5)]
        [InlineData(new[] { "Stem", "", "Keyé>" }, 8)]
        [InlineData(new[] { "", "é>" }, 1)]
        public void Throwing_ForUnsupportedNameCharacterStraddlingSegments_ItShould_ReportTheUnsupportedByte(
            string[] parts,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: false, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadNameThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 1, expectedLinePosition);
        }

        /// <summary>
        /// Processing instruction S5/S6 (#24): checked before committing, the offending byte
        /// inside the value - the target's start when it equals <c>xml</c>
        /// </summary>
        [Theory]
        [InlineData("<?1pi?>", 3)]
        [InlineData("<?pi!x?>", 5)]
        [InlineData("<??>", 3)]
        [InlineData("<? pi?>", 3)]
        [InlineData("<?pi?x?>", 6)]
        [InlineData("<?xMl?>", 3)]
        [InlineData("<?XML version=\"1.0\"?>", 3)]
        public void Throwing_ForMalformedProcessingInstructionTarget_ItShould_ReportTheOffendingByteWithoutCommitting(
            string text,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.Comment));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 1, expectedLinePosition);
            Assert.Equal(0, reader.Position);
            Assert.Equal(TokenType.Comment, reader.TokenType);
        }

        /// <summary>
        /// Processing instruction S5/S6 straddling segments, checked before committing
        /// </summary>
        [Theory]
        [InlineData(new[] { "<?p", "i!x?>" }, 5)]
        [InlineData(new[] { "<?", "1pi?>" }, 3)]
        [InlineData(new[] { "<?x", "M", "l?>" }, 3)]
        public void Throwing_ForMalformedProcessingInstructionTargetStraddlingSegments_ItShould_ReportTheOffendingByteWithoutCommitting(
            string[] parts,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.Comment));
            SequencePosition sequencePosition = reader.SequencePosition;

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 1, expectedLinePosition);
            Assert.Equal(sequencePosition, reader.SequencePosition);
            Assert.Equal(TokenType.Comment, reader.TokenType);
        }
        #endregion

        #region throw sites - offset 0
        /// <summary>
        /// Sites whose offending byte is at the committed position report it + 1
        /// </summary>
        [Fact]
        public void Throwing_ForAttributeNameBeforeContentReady_ItShould_ReportTheGreaterThan()
        {
            // Arrange - <a b> with > at 0-based position 4
            Utf8XmlReader reader = new(">"u8, isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, linePosition: 4));

            // Act
            Exception? thrown = ContentReadyThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 1, 5);
        }

        /// <summary>
        /// Empty element end S5.a (#40): <c>&lt;a b/&gt;</c> reports the <c>/</c>
        /// </summary>
        [Fact]
        public void Throwing_ForAttributeNameBeforeEmptyElementEnd_ItShould_ReportTheSlash()
        {
            // Arrange
            Utf8XmlReader reader = new("/>"u8, isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, linePosition: 4));

            // Act
            Exception? thrown = ReadEmptyElementEndThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 1, 5);
        }

        /// <summary>
        /// Attribute delegate S0 (#40): the attribute name's first byte
        /// </summary>
        [Fact]
        public void Throwing_ForNoSpacingBeforeAttribute_ItShould_ReportTheAttributeName()
        {
            // Arrange
            Utf8XmlReader reader = new("c=\"2\">"u8, isReadingCompleted: true, StartTagState(TokenType.Value, hasSkippedSpacingInStartTag: false, TokenType.Attribute, linePosition: 9));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 1, 10);
        }

        /// <summary>
        /// Read name S6 (#38): a malformed first character
        /// </summary>
        [Theory]
        [InlineData("1a>")]
        [InlineData("-a>")]
        public void Throwing_ForMalformedName_ItShould_ReportTheFirstCharacter(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadNameThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 1, 1);
        }

        /// <summary>
        /// Declaration S0 (#24): the declaration's first byte
        /// </summary>
        [Fact]
        public void Throwing_ForDeclarationNotFirst_ItShould_ReportTheDeclaration()
        {
            // Arrange
            Utf8XmlReader reader = new("<?xml version=\"1.0\"?>"u8, isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None, lineNumber: 1, linePosition: 2));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 2, 3);
        }

        /// <summary>
        /// Document type S1 (#24): the second document type's first byte
        /// </summary>
        [Fact]
        public void Throwing_ForDocumentTypeRepeated_ItShould_ReportTheDocumentType()
        {
            // Arrange
            Utf8XmlReader reader = new("<!DOCTYPE b>"u8, isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.Comment, lineNumber: 3, linePosition: 0, documentType: true));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 4, 1);
        }
        #endregion
    }
}
