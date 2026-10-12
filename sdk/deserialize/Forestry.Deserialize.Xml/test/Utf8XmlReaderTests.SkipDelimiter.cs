using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Skip delimiter shells, from #32 -------------------------------------------------
        //
        // The `SkipDelimiter_*` tests below are shells written from #32's architecture (a skip
        // delimiter step before skip spacing, dispatching on the current token type; skip equals
        // reading the whole `Eq ::= S? '=' S?` all-or-nothing by peeking, then committing with the
        // single / multiple segment spacing helpers that don't set the spacing flag), before the
        // code exists - see doc/dev/Velocity.md's Test shell phase.
        //
        // The regions follow #32's Assertions: dispatch (D0, D1), skip equals (S0-S9), multiple
        // segments (M0-M3) and the read cycle (R0-R2). Examples start after the attribute name,
        // e.g. `<a b ="1">` is arranged as ` ="1">` with the current token Attribute.
        //
        // Skip delimiter returns true to continue and false to halt (#32's architecture). Halting
        // commits nothing, so the position, line tracking and segment are asserted unchanged.
        //
        // Test seam - PLACEHOLDER, not decided by the architecture, to be confirmed or renamed
        // during Understanding. Won't compile until it exists:
        // - `internal bool SkipDelimiter()` on Utf8XmlReader
        //
        // Exceptions are only asserted by type (XmlException); the exception names are under
        // review in a separate bug, so R1 can't yet tell #32's missing `=` from #40's attribute
        // value missing.

        #region helpers
        /// <summary>
        /// Skip delimiter catching the exception, a ref struct can't be captured by Assert.Throws
        /// </summary>
        private static Exception? SkipDelimiterThrown(ref Utf8XmlReader reader)
        {
            try
            {
                reader.SkipDelimiter();
            }
            catch (Exception exception)
            {
                return exception;
            }

            return null;
        }

        /// <summary>
        /// Assert nothing was committed: position, line tracking and the spacing flag unchanged
        /// </summary>
        private static void AssertNothingCommitted(ref Utf8XmlReader reader, long linePositionBefore = 0)
        {
            Assert.Equal(0, reader.Position);
            Assert.Equal(0, reader.ReaderState._lineNumber);
            Assert.Equal(linePositionBefore, reader.ReaderState._linePosition);
            Assert.Equal(TokenType.Attribute, reader.TokenType);
            Assert.False(reader.ReaderState._hasSkippedSpacingInStartTag);
        }
        #endregion

        #region D0
        /// <summary>
        /// D0: the current token is not Attribute, so nothing is skipped even when the next
        /// byte is spacing or <c>=</c>
        /// </summary>
        [Theory]
        [InlineData(" b=\"1\">", TokenType.Element, TokenType.None)]
        [InlineData(">", TokenType.Element, TokenType.None)]
        [InlineData(" c=\"2\">", TokenType.Value, TokenType.Attribute)]
        [InlineData("=\"1\">", TokenType.Element, TokenType.None)]
        public void SkipDelimiter_ForD0NotAfterAttribute_ItShould_ContinueSkippingNothing(
            string text,
            TokenType current,
            TokenType previous
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, StartTagState(current, hasSkippedSpacingInStartTag: false, previous));

            // Act
            bool continued = reader.SkipDelimiter();

            // Assert
            Assert.True(continued);
            Assert.Equal(0, reader.Position);
            Assert.Equal(0, reader.ReaderState._linePosition);
            Assert.Equal(current, reader.TokenType);
            Assert.False(reader.ReaderState._hasSkippedSpacingInStartTag);
        }

        /// <summary>
        /// D0 outside a start tag: prolog, content and miscellaneous never skip a delimiter
        /// </summary>
        [Theory]
        [InlineData(" <a/>", Context.Prolog, TokenType.Declaration)]
        [InlineData("= text", Context.Content, TokenType.Element)]
        [InlineData(" <!-- c -->", Context.Miscellaneous, TokenType.ElementEnd)]
        public void SkipDelimiter_ForD0OutsideStartTag_ItShould_ContinueSkippingNothing(
            string text,
            Context context,
            TokenType current
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(context, current));

            // Act
            bool continued = reader.SkipDelimiter();

            // Assert
            Assert.True(continued);
            Assert.Equal(0, reader.Position);
        }
        #endregion

        #region S0-S3 (D1 commit)
        /// <summary>
        /// S0-S3: the whole equals non-terminal is committed up to the first non-spacing byte
        /// after <c>=</c>, which is not checked; line tracking follows the spacing and
        /// <c>=</c> adds 1 to the line position. The spacing flag is not set.
        /// </summary>
        [Theory]
        [InlineData("=\"1\">", 1, 0, 1)]                // S0 <a b="1">
        [InlineData("='1'/>", 1, 0, 1)]                // S0 single quote
        [InlineData("=1>", 1, 0, 1)]                   // S0 the byte after = is not checked (#41)
        [InlineData(" =\"1\">", 2, 0, 2)]              // S1 <a b ="1">
        [InlineData("\t \t=\"1\">", 4, 0, 4)]          // S1 mixed spacing
        [InlineData("= \"1\">", 2, 0, 2)]              // S2 <a b= "1">
        [InlineData(" = \"1\">", 3, 0, 3)]             // S2 both sides
        [InlineData("\n=\n \"1\">", 4, 2, 1)]          // S3 line feeds both sides
        [InlineData("\r\n=\r\n\"1\">", 5, 2, 0)]       // S3 carriage return + line feed
        public void SkipDelimiter_ForS0ToS3EqualsRead_ItShould_CommitWithoutSettingTheFlag(
            string text,
            long expectedPosition,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));

            // Act
            bool continued = reader.SkipDelimiter();

            // Assert
            Assert.True(continued);
            Assert.Equal(expectedPosition, reader.Position);
            Assert.Equal(expectedLineNumber, reader.ReaderState._lineNumber);
            Assert.Equal(expectedLinePosition, reader.ReaderState._linePosition);
            Assert.Equal(TokenType.Attribute, reader.TokenType);
            Assert.False(reader.ReaderState._hasSkippedSpacingInStartTag);
        }

        /// <summary>
        /// S0: the line position advances from where the equals non-terminal starts
        /// (<c>&lt;Stem Key="1"&gt;</c> after <c>Key</c>)
        /// </summary>
        [Fact]
        public void SkipDelimiter_ForS0AfterLinePosition_ItShould_AdvanceTheLinePosition()
        {
            // Arrange
            Utf8XmlReader reader = new("=\"1\">"u8, isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element, linePosition: 9));

            // Act
            bool continued = reader.SkipDelimiter();

            // Assert
            Assert.True(continued);
            Assert.Equal(1, reader.Position);
            Assert.Equal(10, reader.ReaderState._linePosition);
        }

        /// <summary>
        /// S0-S3 are decided by peeking, not by reading completed: the same commit happens when
        /// more segments may follow
        /// </summary>
        [Theory]
        [InlineData("=\"", 1)]
        [InlineData(" = \"", 3)]
        public void SkipDelimiter_ForS0ToS3ReadingNotCompleted_ItShould_Commit(
            string text,
            long expectedPosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: false, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));

            // Act
            bool continued = reader.SkipDelimiter();

            // Assert
            Assert.True(continued);
            Assert.Equal(expectedPosition, reader.Position);
        }
        #endregion

        #region S4
        /// <summary>
        /// S4: the first non-spacing byte is not <c>=</c>, so the markup is malformed and nothing
        /// is committed (<c>&lt;a b&gt;</c>, <c>&lt;a b/&gt;</c>, <c>&lt;a b c="1"&gt;</c>,
        /// <c>&lt;a b "1"&gt;</c>)
        /// </summary>
        [Theory]
        [InlineData(">", true, 1, 1)]
        [InlineData("/>", true, 1, 1)]
        [InlineData(" c=\"1\">", true, 1, 2)]
        [InlineData(" \"1\">", true, 1, 2)]
        [InlineData("\n>", true, 2, 1)]
        [InlineData("\r\n\t/>", true, 2, 2)]
        [InlineData(">", false, 1, 1)]
        [InlineData(" /", false, 1, 2)]
        public void SkipDelimiter_ForS4NotEquals_ItShould_ThrowAtTheOffendingByteCommittingNothing(
            string text,
            bool isReadingCompleted,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));

            // Act
            Exception? thrown = SkipDelimiterThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, expectedLineNumber, expectedLinePosition);
            AssertNothingCommitted(ref reader);
        }

        /// <summary>
        /// S4: the reported position continues from where the attribute name ended
        /// (<c>&lt;Stem Key&gt;</c> after <c>Key</c>)
        /// </summary>
        [Fact]
        public void SkipDelimiter_ForS4AfterLinePosition_ItShould_ThrowAfterTheAttributeName()
        {
            // Arrange
            Utf8XmlReader reader = new(">"u8, isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element, linePosition: 9));

            // Act
            Exception? thrown = SkipDelimiterThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 1, 10);
            AssertNothingCommitted(ref reader, linePositionBefore: 9);
        }

        /// <summary>
        /// S4 straddling segments: the offset walks the segments (empty ones included) with line
        /// tracking
        /// </summary>
        [Theory]
        [InlineData(new[] { " ", "\n", ">" }, 2, 1)]
        [InlineData(new[] { " ", "", "c=\"1\">" }, 1, 2)]
        [InlineData(new[] { "\n ", " ", "/>" }, 2, 3)]
        public void SkipDelimiter_ForS4NotEqualsStraddlingSegments_ItShould_ThrowAtTheOffendingByte(
            string[] parts,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));

            // Act
            Exception? thrown = SkipDelimiterThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, expectedLineNumber, expectedLinePosition);
            Assert.Equal(Throwing.Category.Expected.ToString(), ((XmlException)thrown!).Category);
            Assert.Equal(Throwing.Markup.EqualsSignTerminal.ToString(), ((XmlException)thrown!).Markup);
            AssertNothingCommitted(ref reader);
        }
        #endregion

        #region S5, S7, S8 (undecided)
        /// <summary>
        /// S5, S7, S8: the bytes end before a non-spacing byte after <c>=</c> is found and
        /// reading is not completed, so the read halts committing nothing
        /// (<c>&lt;a b |="1"&gt;</c>, <c>&lt;a b=|"1"&gt;</c>, <c>&lt;a b = |"1"&gt;</c>)
        /// </summary>
        [Theory]
        [InlineData(" ")]           // S5
        [InlineData("\n\t ")]       // S5 with a line feed, line tracking untouched
        [InlineData("=")]           // S7
        [InlineData(" =")]          // S7 after spacing
        [InlineData("= ")]          // S8
        [InlineData(" = \n")]       // S8 with a line feed
        public void SkipDelimiter_ForUndecided_ItShould_HaltCommittingNothing(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: false, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));

            // Act
            bool continued = reader.SkipDelimiter();

            // Assert
            Assert.False(continued);
            AssertNothingCommitted(ref reader);
        }
        #endregion

        #region S6, S9 (undecided, reading completed)
        /// <summary>
        /// S6, S9: undecided with reading completed throws (<c>&lt;a b </c>, <c>&lt;a b=</c>,
        /// <c>&lt;a b = </c>); the exception type is open in #32
        /// </summary>
        [Theory]
        [InlineData(" ")]           // S6
        [InlineData("\n")]          // S6
        [InlineData("=")]           // S9 (S7 completed)
        [InlineData(" = ")]         // S9 (S8 completed)
        public void SkipDelimiter_ForUndecidedReadingCompleted_ItShould_Throw(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));

            // Act
            Exception? thrown = SkipDelimiterThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion

        #region M0-M2 (multiple segments, commit)
        /// <summary>
        /// M0-M2: the peek crosses segments (including empty ones) without advancing, then the
        /// commit follows it into the segment holding the first non-spacing byte after <c>=</c>
        /// </summary>
        [Theory]
        [InlineData(new[] { " ", "=", " \"1\">" }, 3, 0, 3)]         // M0
        [InlineData(new[] { "=", "\"1\">" }, 1, 0, 1)]               // M1 = is the last byte of a segment
        [InlineData(new[] { " =", "\"1\">" }, 2, 0, 2)]              // M1 after spacing
        [InlineData(new[] { " ", "", "=", "", "\"1\">" }, 2, 0, 2)]  // M2 empty segments
        [InlineData(new[] { "\n", "=", "\n \"1\">" }, 4, 2, 1)]      // M0 line tracking across segments
        public void SkipDelimiter_ForMultipleSegments_ItShould_CommitIntoTheLastSegment(
            string[] parts,
            long expectedPosition,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));

            // Act
            bool continued = reader.SkipDelimiter();

            // Assert
            Assert.True(continued);
            Assert.Equal(expectedPosition, reader.Position);
            Assert.Equal(expectedLineNumber, reader.ReaderState._lineNumber);
            Assert.Equal(expectedLinePosition, reader.ReaderState._linePosition);
            Assert.False(reader.ReaderState._hasSkippedSpacingInStartTag);
        }

        /// <summary>
        /// S1 and S2 across segments: the spacing before <c>=</c> (S1) or after it (S2) is read
        /// past, alone or together, whichever segment it is in. Skip spacing runs next in the
        /// read cycle and indexes the byte at the reader position, so the reader must not be left
        /// at the end of a segment (e.g. after an <c>=</c> that is a segment's last byte).
        /// </summary>
        [Theory]
        [InlineData(new[] { "  ", "=\"1\">" }, 3, 0, 3)]             // S1 only, spacing in its own segment
        [InlineData(new[] { " ", " ", "=\"1\">" }, 3, 0, 3)]         // S1 only, spacing over two segments
        [InlineData(new[] { "=", "  \"1\">" }, 3, 0, 3)]             // S2 only, = is the last byte of a segment
        [InlineData(new[] { "=", " ", "", "\"1\">" }, 2, 0, 2)]      // S2 only, spacing in its own segment
        [InlineData(new[] { " ", "=", "", " ", "\"1\">" }, 3, 0, 3)] // S1 and S2, each in its own segment
        [InlineData(new[] { "\r\n", "=", "\r\n", "\"1\">" }, 5, 2, 0)] // S1 and S2 with carriage returns
        [InlineData(new[] { "=", "\"1\">" }, 1, 0, 1)]               // neither, = is the last byte of a segment
        public void SkipDelimiter_ForS1S2StraddlingSegments_ItShould_ReadPastTheSpacing(
            string[] parts,
            long expectedPosition,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));

            // Act
            bool continued = reader.SkipDelimiter();
            bool spacing = reader.SkipSpacing();

            // Assert
            Assert.True(continued);
            Assert.True(spacing);
            Assert.Equal(expectedPosition, reader.Position);
            Assert.Equal(expectedLineNumber, reader.ReaderState._lineNumber);
            Assert.Equal(expectedLinePosition, reader.ReaderState._linePosition);
            Assert.False(reader.ReaderState._hasSkippedSpacingInStartTag);
        }

        /// <summary>
        /// S7, S9: <c>=</c> is the final byte of the sequence (the sequence reader is at its end
        /// after <c>=</c>, trailing empty segments included), so the read halts when reading is
        /// not completed
        /// </summary>
        [Theory]
        [InlineData([new[] { "=" }])]
        [InlineData([new[] { " ", "=" }])]
        [InlineData([new[] { "=", "" }])]
        [InlineData([new[] { " =", "", "" }])]
        public void SkipDelimiter_ForEqualsFinalByteOfTheSequence_ItShould_HaltCommittingNothing(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: false, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));
            SequencePosition sequencePosition = reader.SequencePosition;

            // Act
            bool continued = reader.SkipDelimiter();

            // Assert
            Assert.False(continued);
            Assert.Equal(sequencePosition, reader.SequencePosition);
            AssertNothingCommitted(ref reader);
        }

        /// <summary>
        /// S9: <c>=</c> is the final byte of the sequence and reading is completed, so the
        /// element is incomplete, reported at the end of the data
        /// </summary>
        [Theory]
        [InlineData(new[] { "=" }, 1, 2)]
        [InlineData(new[] { "=", "", "" }, 1, 2)]
        [InlineData(new[] { " ", "\n", "=" }, 2, 2)]
        public void SkipDelimiter_ForEqualsFinalByteOfTheSequenceReadingCompleted_ItShould_ThrowAtTheEnd(
            string[] parts,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));

            // Act
            Exception? thrown = SkipDelimiterThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, expectedLineNumber, expectedLinePosition);
            Assert.Equal(Throwing.Category.Incomplete.ToString(), ((XmlException)thrown!).Category);
        }

        /// <summary>
        /// Undecided across segments (S5, S7, S8 straddling): halts with the sequence position
        /// unchanged
        /// </summary>
        [Theory]
        [InlineData([new[] { " ", " " }])]
        [InlineData([new[] { " ", "", "=" }])]
        [InlineData([new[] { "=", " ", "" }])]
        public void SkipDelimiter_ForMultipleSegmentsUndecided_ItShould_HaltCommittingNothing(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: false, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));
            SequencePosition sequencePosition = reader.SequencePosition;

            // Act
            bool continued = reader.SkipDelimiter();

            // Assert
            Assert.False(continued);
            Assert.Equal(sequencePosition, reader.SequencePosition);
            AssertNothingCommitted(ref reader);
        }
        #endregion

        #region M3 (multiple segments, throw)
        /// <summary>
        /// M3: undecided across segments with reading completed throws (as S6, S9), and S4
        /// across segments throws on the first non-spacing byte
        /// </summary>
        [Theory]
        [InlineData([new[] { " ", "\n", "=" }])]      // M3 (S9)
        [InlineData([new[] { " ", "", " " }])]        // M3 (S6)
        [InlineData([new[] { " ", "c=\"1\">" }])]     // S4 straddling
        [InlineData([new[] { " ", "", ">" }])]        // S4 straddling empty segment
        public void SkipDelimiter_ForMultipleSegmentsMalformedOrCompleted_ItShould_Throw(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));

            // Act
            Exception? thrown = SkipDelimiterThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion

        #region R0-R2 (read cycle)
        /// <summary>
        /// R0: the spacing before <c>=</c> doesn't set the spacing flag, so a following
        /// attribute without spacing is still caught by #40's S0
        /// (<c>&lt;a b ="1"c="2"&gt;</c>). Asserted on the reader state until the attribute
        /// value delegate exists.
        /// </summary>
        [Fact]
        public void SkipDelimiter_ForR0SpacingBeforeEquals_ItShould_LeaveTheFlagCleared()
        {
            // Arrange
            Utf8XmlReader reader = new(" =\"1\"c=\"2\">"u8, isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));

            // Act
            bool delimiter = reader.SkipDelimiter();
            bool spacing = reader.SkipSpacing();

            // Assert
            Assert.True(delimiter);
            Assert.True(spacing);  // nothing to skip at '"', continue (new skip spacing convention)
            Assert.Equal(2, reader.Position);
            Assert.False(reader.ReaderState._hasSkippedSpacingInStartTag);
        }

        /// <summary>
        /// R1: through Read(), a dangling attribute name throws in skip delimiter before content
        /// ready S4 or empty element S5.a are reached. Asserted by type only, so this passes
        /// before #32 too - see the exception naming bug.
        /// </summary>
        [Theory]
        [InlineData("<a b>")]
        [InlineData("<a b/>")]
        [InlineData("<a b >")]
        public void Read_ForR1DanglingAttributeName_ItShould_Throw(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));
            reader.Read();
            reader.Read();

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }

        /// <summary>
        /// Through Read(): spacing after the attribute name drains the segment, so skip
        /// delimiter halts committing nothing - the spacing isn't skipped by skip spacing and the
        /// flag stays cleared (<c>&lt;a b |="1"&gt;</c>)
        /// </summary>
        [Fact]
        public void Read_ForSpacingAfterAttributeNameDrainingTheSegment_ItShould_HaltCommittingNothing()
        {
            // Arrange
            Utf8XmlReader reader = new("<a b "u8, isReadingCompleted: false, ContextState(Context.Prolog, TokenType.None));
            reader.Read();
            reader.Read();

            // Act
            bool advancement = reader.Read();

            // Assert
            Assert.False(advancement);
            Assert.Equal(4, reader.Position);
            Assert.Equal(TokenType.Attribute, reader.TokenType);
            Assert.False(reader.ReaderState._hasSkippedSpacingInStartTag);
        }

        /// <summary>
        /// Through Read(): the halted equals non-terminal is committed by a new reader once the
        /// next segment arrives, and the read continues to the attribute value candidate
        /// (<c>&lt;a b |= "1"&gt;</c>). The caller re-supplies the uncommitted spacing from
        /// position 4, so the second reader starts at <c> = "1"&gt;</c>. The attribute value
        /// delegate isn't built, so skip delimiter is called directly.
        /// </summary>
        [Fact]
        public void Read_ForEqualsInTheNextSegment_ItShould_CommitInTheNextReader()
        {
            // Arrange
            Utf8XmlReader first = new("<a b "u8, isReadingCompleted: false, ContextState(Context.Prolog, TokenType.None));
            first.Read();
            first.Read();
            first.Read();
            ReaderState state = first.ReaderState;

            Utf8XmlReader second = new(" = \"1\">"u8, isReadingCompleted: true, state);

            // Act
            bool continued = second.SkipDelimiter();

            // Assert
            Assert.True(continued);
            Assert.Equal(3, second.Position);
            Assert.False(second.ReaderState._hasSkippedSpacingInStartTag);
        }

        // R2 (<a b with reading completed throws element not ended in the drained step, skip
        // delimiter never runs) is already covered by #40's
        // ReadAttribute_ForS5NameAtTheEndOfTheDocument_ItShould_ThrowElementNotEndedOnTheNextRead.
        #endregion
    }
}
