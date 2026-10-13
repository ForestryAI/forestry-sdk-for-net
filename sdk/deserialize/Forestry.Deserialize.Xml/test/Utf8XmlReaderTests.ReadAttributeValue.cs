using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Read attribute value shells, from #44 -------------------------------------------
        //
        // The `ReadAttributeValue_*` tests below are shells written from #44's architecture (a
        // quoted literal read all-or-nothing by peeking; the string excludes `<`, `&` and control
        // bytes other than tab, line feed and carriage return; bytes >= 0x80 pass unchecked; the
        // raw bytes are the value, not normalized), before the code exists - see
        // doc/dev/Velocity.md's Test shell phase.
        //
        // The regions follow #44's Assertions: guard (G0), literal (S0-S12), multiple segments
        // (M0-M6) and the read cycle (R0-R5). Examples start at the starting quote with the
        // current token Attribute, as after skip delimiter (#32).
        //
        // The delegate is reached through ReadValue() (#27's delegation, candidate S7), so no
        // shell names the delegate method and no test seam is needed. Until it is built the
        // AttributeValue arm throws NotImplementedException.
        //
        // Open in #44, so asserted loosely until decided:
        // - G0 markup, S6 and S8 category: XmlException type and position only
        // - S7 unsupported exception type: any exception, position only when it is an XmlException
        // - M3 empty value straddling segments: the text only, not Value vs ValueSequence

        #region helpers
        /// <summary>
        /// Assert the reader after a literal was read: the token types, the value text, the
        /// spacing flag untouched (false) and the positions past the ending quote
        /// </summary>
        private static void AssertAttributeValueRead(
            ref Utf8XmlReader reader,
            string expectedValue,
            long expectedPosition,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            Assert.Equal(TokenType.Value, reader.TokenType);
            Assert.Equal(TokenType.Attribute, reader.ReaderState._previousTokenType);
            Assert.Equal(expectedValue, ValueText(ref reader));
            Assert.False(reader.ReaderState._hasSkippedSpacingInStartTag);

            Assert.Equal(1, reader.Depth);
            Assert.False(reader.ReaderState._elementStack.ContentReady);

            Assert.Equal(expectedPosition, reader.Position);
            Assert.Equal(expectedLineNumber, reader.ReaderState._lineNumber);
            Assert.Equal(expectedLinePosition, reader.ReaderState._linePosition);
        }

        /// <summary>
        /// A reader at the starting quote, after an attribute name
        /// </summary>
        private static Utf8XmlReader AttributeValueReader(string text, bool isReadingCompleted = true) =>
            new(Encoding.UTF8.GetBytes(text), isReadingCompleted, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));

        /// <summary>
        /// A reader at the starting quote straddling segments, after an attribute name
        /// </summary>
        private static Utf8XmlReader AttributeValueReader(string[] parts, bool isReadingCompleted = true) =>
            new(Sequence(parts), isReadingCompleted, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));
        #endregion

        #region G0
        /// <summary>
        /// G0: a quote in a start tag is always the AttributeValue candidate (#27 rule 5), so
        /// the delegate throws when no attribute name comes before it (<c>&lt;a "1"&gt;</c>,
        /// <c>&lt;a b="1" "2"&gt;</c>); markup open in #44
        /// </summary>
        [Theory]
        [InlineData("\"1\">", TokenType.Element, TokenType.None)]
        [InlineData("'1'/>", TokenType.Element, TokenType.None)]
        [InlineData("\"2\">", TokenType.Value, TokenType.Attribute)]
        public void ReadAttributeValue_ForG0NotAfterAttribute_ItShould_ThrowCommittingNothing(
            string text,
            TokenType current,
            TokenType previous
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, StartTagState(current, hasSkippedSpacingInStartTag: true, previous));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 1, 1);
            Assert.Equal(Throwing.Category.Expected.ToString(), ((XmlException)thrown!).Category);
            Assert.Equal(0, reader.Position);
            Assert.Equal(current, reader.TokenType);
        }
        #endregion

        #region S0-S5, S9 (read)
        /// <summary>
        /// S0-S5, S9: the literal is read, the value is the raw bytes between the quotes (not
        /// normalized, UTF-8 unchecked), the opposite quote kind is content, and lines are
        /// tracked over the whole literal
        /// </summary>
        [Theory]
        [InlineData("\"1\"", "1", 3, 0, 3)]                     // S0
        [InlineData("\"1\">", "1", 3, 0, 3)]                    // S0 the ending terminal is not read
        [InlineData("'1'", "1", 3, 0, 3)]                       // S1
        [InlineData("\"\"", "", 2, 0, 2)]                       // S2
        [InlineData("''/>", "", 2, 0, 2)]                       // S2
        [InlineData("\"it's\"", "it's", 6, 0, 6)]               // S3
        [InlineData("'say \"hi\"'", "say \"hi\"", 10, 0, 10)]   // S3
        [InlineData("\"Gran ä\"", "Gran ä", 9, 0, 9)]           // S4 line position counts bytes
        [InlineData("\"a\nb\"", "a\nb", 5, 1, 2)]               // S5 line feed kept, line tracked
        [InlineData("\"a\r\nb\"", "a\r\nb", 6, 1, 2)]           // S5 carriage return + line feed
        [InlineData("\"a\tb\"", "a\tb", 5, 0, 5)]               // S5 tab kept
        [InlineData("\"a\u007Fb\"", "a\u007Fb", 5, 0, 5)]       // S9 0x7F allowed
        [InlineData("\"a\"<", "a", 3, 0, 3)]                    // S10 '<' after the ending quote
        public void ReadAttributeValue_ForS0ToS5Literal_ItShould_ReadTheRawBytes(
            string text,
            string expectedValue,
            long expectedPosition,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = AttributeValueReader(text);

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.False(reader.HasValueSequence);
            AssertAttributeValueRead(ref reader, expectedValue, expectedPosition, expectedLineNumber, expectedLinePosition);
        }

        /// <summary>
        /// S0: the line position continues from where the literal starts
        /// (<c>&lt;Stem Key="1"</c> after <c>=</c>)
        /// </summary>
        [Fact]
        public void ReadAttributeValue_ForS0AfterLinePosition_ItShould_AdvanceTheLinePosition()
        {
            // Arrange
            Utf8XmlReader reader = new("\"1\">"u8, isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element, linePosition: 10));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            AssertAttributeValueRead(ref reader, "1", 3, 0, 13);
        }

        /// <summary>
        /// S0-S5 are decided by the ending quote, not by reading completed
        /// </summary>
        [Theory]
        [InlineData("\"1\"", "1")]
        [InlineData("''", "")]
        public void ReadAttributeValue_ForLiteralReadingNotCompleted_ItShould_Read(
            string text,
            string expectedValue
        ) {
            // Arrange
            Utf8XmlReader reader = AttributeValueReader(text, isReadingCompleted: false);

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.Equal(expectedValue, ValueText(ref reader));
        }
        #endregion

        #region S6, S8, S10 (malformed)
        /// <summary>
        /// S6, S8, S10: <c>&lt;</c> or a control byte before the ending quote is malformed,
        /// reported at the offending byte; nothing is committed. Category open in #44.
        /// </summary>
        [Theory]
        [InlineData("\"a<b\"", true, 1, 3)]          // S6
        [InlineData("\"1'><c/>", true, 1, 5)]        // S6 opposite quote kind never ends the literal
        [InlineData("\"<\"", false, 1, 2)]           // S6 reading not completed
        [InlineData("\"a\u0001b\"", true, 1, 3)]     // S8
        [InlineData("\"\u0000\"", true, 1, 2)]       // S8
        [InlineData("'\u001F'", true, 1, 2)]         // S8
        [InlineData("\"a\n\u0008\"", true, 2, 1)]    // S8 after a line feed
        [InlineData("\"a<&\"", true, 1, 3)]          // S10 '<' first
        public void ReadAttributeValue_ForMalformedString_ItShould_ThrowAtTheOffendingByteCommittingNothing(
            string text,
            bool isReadingCompleted,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = AttributeValueReader(text, isReadingCompleted);

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, expectedLineNumber, expectedLinePosition);
            AssertNothingCommitted(ref reader);
        }
        #endregion

        #region S7, S10 (unsupported)
        /// <summary>
        /// S7, S10: <c>&amp;</c> starts a reference, well-formed but unsupported in the POC,
        /// reported at the <c>&amp;</c>; nothing is committed. Exception type open in #44.
        /// </summary>
        [Theory]
        [InlineData("\"a&amp;\"", 1, 3)]     // S7
        [InlineData("'&#60;'", 1, 2)]        // S7 character reference
        [InlineData("\"a&<\"", 1, 3)]        // S10 '&' first
        public void ReadAttributeValue_ForReference_ItShould_ThrowUnsupportedCommittingNothing(
            string text,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = AttributeValueReader(text);

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.NotNull(thrown);
            Assert.IsNotType<NotImplementedException>(thrown);
            if (thrown is XmlException exception)
            {
                Assert.Equal(expectedLineNumber, exception.LineNumber);
                Assert.Equal(expectedLinePosition, exception.LinePosition);
            }
            AssertNothingCommitted(ref reader);
        }
        #endregion

        #region S11, S12 (undecided)
        /// <summary>
        /// S11: no ending quote yet and reading is not completed, so the read halts committing
        /// nothing
        /// </summary>
        [Theory]
        [InlineData("\"abc")]
        [InlineData("\"")]
        [InlineData("'a\"b")]       // the opposite quote kind never ends the literal
        [InlineData("\"a\nb")]      // line tracking untouched
        public void ReadAttributeValue_ForS11NoEndingQuote_ItShould_HaltCommittingNothing(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = AttributeValueReader(text, isReadingCompleted: false);

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.False(advancement);
            AssertNothingCommitted(ref reader);
        }

        /// <summary>
        /// S12: no ending quote and reading is completed, so the attribute value is incomplete,
        /// reported at the end of the data
        /// </summary>
        [Theory]
        [InlineData("\"abc", 1, 5)]
        [InlineData("\"", 1, 2)]
        [InlineData("'a\"", 1, 4)]
        [InlineData("\"a\nb", 2, 2)]
        public void ReadAttributeValue_ForS12NoEndingQuoteReadingCompleted_ItShould_ThrowIncompleteAtTheEnd(
            string text,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = AttributeValueReader(text);

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, expectedLineNumber, expectedLinePosition);
            Assert.Equal(Throwing.Category.Incomplete.ToString(), ((XmlException)thrown!).Category);
            Assert.Equal(Throwing.Markup.AttributeValue.ToString(), ((XmlException)thrown!).Markup);
            AssertNothingCommitted(ref reader);
        }
        #endregion

        #region M0-M2 (multiple segments, read)
        /// <summary>
        /// M0-M2: the literal straddles segments (empty ones included), so the value is a
        /// sequence; lines are tracked across the segments
        /// </summary>
        [Theory]
        [InlineData(new[] { "\"St", "em\"" }, "Stem", 6, 0, 6)]                  // M0
        [InlineData(new[] { "\"", "Stem\"" }, "Stem", 6, 0, 6)]                  // M1 starting quote alone
        [InlineData(new[] { "\"Stem", "\"" }, "Stem", 6, 0, 6)]                  // M1 ending quote alone
        [InlineData(new[] { "\"St", "", "em\"" }, "Stem", 6, 0, 6)]              // M2 empty segment
        [InlineData(new[] { "'a\n", "b'" }, "a\nb", 5, 1, 2)]                    // M0 line feed in the first segment
        public void ReadAttributeValue_ForMultipleSegments_ItShould_ReadTheValueSequence(
            string[] parts,
            string expectedValue,
            long expectedPosition,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = AttributeValueReader(parts);

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            AssertAttributeValueRead(ref reader, expectedValue, expectedPosition, expectedLineNumber, expectedLinePosition);
        }

        /// <summary>
        /// M0: a UTF-8 character split between segments (<c>ä</c> is <c>C3 A4</c>, bytes 6 and 7
        /// of <c>"Gran ä"</c>): neither half is mistaken for a quote, <c>&lt;</c> or
        /// <c>&amp;</c> (both are &gt;= 0x80), the sequence joins them back and the line
        /// position counts bytes
        /// </summary>
        [Theory]
        [InlineData("\"Gran ä\"", new[] { 7 })]        // split between C3 and A4
        [InlineData("\"Gran ä\"", new[] { 6, 7 })]     // C3 alone in its own segment
        [InlineData("\"Gran ä\"", new[] { 7, 7 })]     // an empty segment between the halves
        public void ReadAttributeValue_ForM0Utf8CharacterSplit_ItShould_ReadTheValueSequence(
            string text,
            int[] byteOffsets
        ) {
            // Arrange
            Utf8XmlReader reader = new(SequenceSplitAt(text, byteOffsets), isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.True(reader.HasValueSequence);
            AssertAttributeValueRead(ref reader, "Gran ä", 9, 0, 9);
        }

        /// <summary>
        /// M3: an empty literal straddling segments is an empty value; Value or an empty
        /// ValueSequence is open in #44
        /// </summary>
        [Theory]
        [InlineData([new[] { "\"", "\"" }])]
        [InlineData([new[] { "'", "", "'" }])]
        public void ReadAttributeValue_ForM3EmptyStraddlingSegments_ItShould_ReadEmpty(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = AttributeValueReader(parts);

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.Equal(TokenType.Value, reader.TokenType);
            Assert.Equal("", ValueText(ref reader));
            Assert.Equal(2, reader.Position);
        }
        #endregion

        #region M4-M6 (multiple segments, halt and throw)
        /// <summary>
        /// M4: no ending quote across segments and reading is not completed, so the read halts
        /// with the sequence position unchanged
        /// </summary>
        [Theory]
        [InlineData([new[] { "\"St", "em" }])]
        [InlineData([new[] { "\"", "" }])]
        [InlineData([new[] { "'a\"", "", "b" }])]
        public void ReadAttributeValue_ForM4NoEndingQuote_ItShould_HaltCommittingNothing(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = AttributeValueReader(parts, isReadingCompleted: false);
            SequencePosition sequencePosition = reader.SequencePosition;

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.False(advancement);
            Assert.Equal(sequencePosition, reader.SequencePosition);
            AssertNothingCommitted(ref reader);
        }

        /// <summary>
        /// M5, M6: the offending byte or the end of the data is reported across segments with
        /// line tracking; nothing is committed
        /// </summary>
        [Theory]
        [InlineData(new[] { "\"a", "\n", "<\"" }, 2, 1)]       // M5 '<'
        [InlineData(new[] { "\"a", "", "&b\"" }, 1, 3)]        // M5 '&'
        [InlineData(new[] { "\"a", "\u0001\"" }, 1, 3)]        // M5 control byte
        [InlineData(new[] { "\"Stem", "" }, 1, 6)]             // M6 incomplete
        [InlineData(new[] { "'a\n", "b" }, 2, 2)]              // M6 incomplete after a line feed
        public void ReadAttributeValue_ForMultipleSegmentsMalformedOrIncomplete_ItShould_ThrowAtThePosition(
            string[] parts,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = AttributeValueReader(parts);

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.NotNull(thrown);
            Assert.IsNotType<NotImplementedException>(thrown);
            if (thrown is XmlException exception)
            {
                Assert.Equal(expectedLineNumber, exception.LineNumber);
                Assert.Equal(expectedLinePosition, exception.LinePosition);
            }
            AssertNothingCommitted(ref reader);
        }
        #endregion

        #region R0-R5 (read cycle)
        /// <summary>
        /// R0, R1: through Read(), the first complete attributes - element, attribute name,
        /// attribute value, then the empty element's end
        /// </summary>
        [Theory]
        [InlineData("<a b=\"1\"/>", new[] { "Element:a", "Attribute:b", "Value:1", "ElementEnd:" })]
        [InlineData("<a b = '1' c=\"2\"/>", new[] { "Element:a", "Attribute:b", "Value:1", "Attribute:c", "Value:2", "ElementEnd:" })]
        [InlineData("<Stem StemKey=\"12\"\n\tSpecies='Gran ä'/>", new[] { "Element:Stem", "Attribute:StemKey", "Value:12", "Attribute:Species", "Value:Gran ä", "ElementEnd:" })]
        public void Read_ForR0R1Attributes_ItShould_ReadNameThenValue(
            string text,
            string[] expectedTokens
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));
            List<string> tokens = [];

            // Act
            while (reader.Read())
            {
                tokens.Add($"{reader.TokenType}:{ValueText(ref reader)}");
            }

            // Assert
            Assert.Equal(expectedTokens, tokens);
        }

        /// <summary>
        /// R2, R5: through Read(), no spacing before the next attribute throws at it (#40's S0,
        /// now reachable), and a value without an attribute name throws at its quote (G0)
        /// </summary>
        [Theory]
        [InlineData("<a b=\"1\"c=\"2\">", 3, 1, 9)]   // R2
        [InlineData("<a \"1\">", 1, 1, 4)]            // R5
        public void Read_ForR2R5Malformed_ItShould_Throw(
            string text,
            int readsBefore,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));
            for (int read = 0; read < readsBefore; read++)
            {
                Assert.True(reader.Read());
            }

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, expectedLineNumber, expectedLinePosition);
        }

        /// <summary>
        /// R3: through Read(), after the value content ready S5 skips <c>&gt;</c>
        /// (<c>&lt;a b="1"&gt;</c>, more to come)
        /// </summary>
        [Fact]
        public void Read_ForR3ValueThenStartTagEnd_ItShould_SkipTheEndingTerminal()
        {
            // Arrange
            Utf8XmlReader reader = new("<a b=\"1\">"u8, isReadingCompleted: false, ContextState(Context.Prolog, TokenType.None));
            reader.Read();
            reader.Read();
            bool value = reader.Read();

            // Act
            bool advancement = reader.Read();

            // Assert
            Assert.True(value);
            Assert.False(advancement);
            Assert.Equal(9, reader.Position);
            Assert.True(reader.ReaderState._elementStack.ContentReady);
        }

        /// <summary>
        /// R4: through Read(), the literal straddles two piped readers (<c>&lt;a b="1|0"/&gt;</c>):
        /// the first halts, the caller re-supplies the bytes from the reader's position and the
        /// second reads the value, then the empty element's end
        /// </summary>
        /// <remarks>
        /// The first reader's skip delimiter commits <c>=</c> before the value halts, so the
        /// second reader starts at the quote with the current token still Attribute - see the
        /// #32 / #44 note on re-running skip delimiter.
        /// </remarks>
        [Fact]
        public void Read_ForR4LiteralAcrossReaders_ItShould_HaltThenReadTheValue()
        {
            // Arrange
            Utf8XmlReader first = new("<a b=\"1"u8, isReadingCompleted: false, ContextState(Context.Prolog, TokenType.None));
            first.Read();
            first.Read();
            bool halted = first.Read();
            long position = first.Position;
            ReaderState state = first.ReaderState;

            Utf8XmlReader second = new(Encoding.UTF8.GetBytes("<a b=\"10\"/>"[(int)position..]), isReadingCompleted: true, state);

            // Act
            bool value = second.Read();
            TokenType valueType = second.TokenType;
            string valueText = ValueText(ref second);
            bool elementEnd = second.Read();

            // Assert
            Assert.False(halted);
            Assert.True(value);
            Assert.Equal(TokenType.Value, valueType);
            Assert.Equal("10", valueText);
            Assert.True(elementEnd);
            Assert.Equal(TokenType.ElementEnd, second.TokenType);
        }
        #endregion
    }
}
