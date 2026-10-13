using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Skipped attribute delimiter shells, from bug #45 --------------------------------
        //
        // The tests below are shells written from #45's architecture (a flag recording that
        // skip delimiter committed the equals non-terminal, so a reader reconstructed after the
        // attribute value halts doesn't expect `=` again; set by skip equals, checked first by
        // skip delimiter, cleared by the attribute value delegate), before the code exists - see
        // doc/dev/Velocity.md's Test shell phase.
        //
        // The regions follow #45's Assertions: flag (F0-F2), skip delimiter (D0-D4), attribute
        // value delegate (V0, V1) and the read cycle (R6-R9). R4 is #44's
        // Read_ForR4LiteralAcrossReaders_ItShould_HaltThenReadTheValue, which this bug makes pass.
        //
        // Test seam - PLACEHOLDER, not decided by the architecture, to be confirmed or renamed
        // during Understanding. Won't compile until it exists:
        // - ReaderState field `_hasSkippedAttributeDelimiter` and internal constructor parameter
        //   `hasSkippedAttributeDelimiter`, carried by the reader like `_hasSkippedSpacingInStartTag`
        //   (READER.md rule 22). The four test helpers building a ReaderState by named arguments
        //   pass it too; StartTagState takes it as an optional parameter (default false).

        #region F0-F2 (flag)
        /// <summary>
        /// F0: the flag defaults to false
        /// </summary>
        [Fact]
        public void SkippedAttributeDelimiter_ForF0NewReaderState_ItShould_BeFalse()
        {
            // Arrange
            ReaderState state = new();
            Utf8XmlReader reader = new("<a/>"u8, isReadingCompleted: true, state);

            // Assert
            Assert.False(state._hasSkippedAttributeDelimiter);
            Assert.False(reader.ReaderState._hasSkippedAttributeDelimiter);
        }

        /// <summary>
        /// F1, F2: both reader constructors copy the flag from the reader state, and the reader
        /// state property hands it back (rule 22)
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SkippedAttributeDelimiter_ForF1F2ReconstructedReader_ItShould_KeepTheFlag(
            bool isMultipleSegments
        ) {
            // Arrange
            ReaderState state = StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element, hasSkippedAttributeDelimiter: true);

            // Act
            Utf8XmlReader reader = isMultipleSegments
                ? new(Sequence("\"1", "0\"/>"), isReadingCompleted: true, state)
                : new("\"10\"/>"u8, isReadingCompleted: true, state);

            // Assert
            Assert.True(reader.ReaderState._hasSkippedAttributeDelimiter);
        }
        #endregion

        #region D0-D4 (skip delimiter)
        /// <summary>
        /// D0: committing the equals non-terminal sets the flag (#32's S0-S3)
        /// </summary>
        [Theory]
        [InlineData("=\"1\">", 1)]
        [InlineData(" = '1'/>", 3)]
        public void SkippedAttributeDelimiter_ForD0EqualsCommitted_ItShould_SetTheFlag(
            string text,
            long expectedPosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));

            // Act
            bool continued = reader.SkipDelimiter();

            // Assert
            Assert.True(continued);
            Assert.Equal(expectedPosition, reader.Position);
            Assert.True(reader.ReaderState._hasSkippedAttributeDelimiter);
            Assert.False(reader.ReaderState._hasSkippedSpacingInStartTag);
        }

        /// <summary>
        /// D0 across segments: the flag is set when the commit crosses segments
        /// </summary>
        [Fact]
        public void SkippedAttributeDelimiter_ForD0EqualsCommittedStraddlingSegments_ItShould_SetTheFlag()
        {
            // Arrange
            Utf8XmlReader reader = new(Sequence(" ", "=", "\"1\">"), isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));

            // Act
            bool continued = reader.SkipDelimiter();

            // Assert
            Assert.True(continued);
            Assert.Equal(2, reader.Position);
            Assert.True(reader.ReaderState._hasSkippedAttributeDelimiter);
        }

        /// <summary>
        /// D1: the flag is set, so skip delimiter breaks fast - the reader is at the literal of a
        /// reconstructed reader (<c>&lt;a b="1|0"/&gt;</c>)
        /// </summary>
        [Theory]
        [InlineData("\"10\"/>")]
        [InlineData("'10'>")]
        [InlineData(">")]           // <a b=> - content ready decides, not skip delimiter
        public void SkippedAttributeDelimiter_ForD1FlagSet_ItShould_ContinueSkippingNothing(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element, hasSkippedAttributeDelimiter: true));

            // Act
            bool continued = reader.SkipDelimiter();

            // Assert
            Assert.True(continued);
            Assert.Equal(0, reader.Position);
            Assert.Equal(0, reader.ReaderState._linePosition);
            Assert.True(reader.ReaderState._hasSkippedAttributeDelimiter);
        }

        /// <summary>
        /// D2: the flag is not set, so a quote right after an attribute name is still malformed
        /// (<c>&lt;a b "1"&gt;</c>, #32's S4)
        /// </summary>
        [Theory]
        [InlineData("\"1\">", 1, 1)]
        [InlineData(" '1'>", 1, 2)]
        public void SkippedAttributeDelimiter_ForD2FlagNotSet_ItShould_ThrowExpectedEquals(
            string text,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));

            // Act
            Exception? thrown = SkipDelimiterThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, expectedLineNumber, expectedLinePosition);
            Assert.Equal(Throwing.Markup.EqualsSignTerminal.ToString(), ((XmlException)thrown!).Markup);
            Assert.False(reader.ReaderState._hasSkippedAttributeDelimiter);
        }

        /// <summary>
        /// D3: skip delimiter halts committing nothing, so the flag stays false (#32's S5, S7, S8)
        /// </summary>
        [Theory]
        [InlineData(" ")]
        [InlineData("=")]
        [InlineData("= ")]
        public void SkippedAttributeDelimiter_ForD3Undecided_ItShould_LeaveTheFlagFalse(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: false, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element));

            // Act
            bool continued = reader.SkipDelimiter();

            // Assert
            Assert.False(continued);
            Assert.Equal(0, reader.Position);
            Assert.False(reader.ReaderState._hasSkippedAttributeDelimiter);
        }

        /// <summary>
        /// D4: the current token is not Attribute, so skip delimiter continues and leaves the
        /// flag untouched (#32's D0)
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SkippedAttributeDelimiter_ForD4NotAfterAttribute_ItShould_LeaveTheFlagUntouched(
            bool hasSkippedAttributeDelimiter
        ) {
            // Arrange
            Utf8XmlReader reader = new(" c=\"2\">"u8, isReadingCompleted: true, StartTagState(TokenType.Value, hasSkippedSpacingInStartTag: false, TokenType.Attribute, hasSkippedAttributeDelimiter: hasSkippedAttributeDelimiter));

            // Act
            bool continued = reader.SkipDelimiter();

            // Assert
            Assert.True(continued);
            Assert.Equal(0, reader.Position);
            Assert.Equal(hasSkippedAttributeDelimiter, reader.ReaderState._hasSkippedAttributeDelimiter);
        }
        #endregion

        #region V0, V1 (attribute value delegate)
        /// <summary>
        /// V0: reading the attribute value clears the flag, so the next attribute's equals
        /// non-terminal is checked again
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SkippedAttributeDelimiter_ForV0ValueRead_ItShould_ClearTheFlag(
            bool isMultipleSegments
        ) {
            // Arrange
            ReaderState state = StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element, hasSkippedAttributeDelimiter: true);
            Utf8XmlReader reader = isMultipleSegments
                ? new(Sequence("\"1", "0\"/>"), isReadingCompleted: true, state)
                : new("\"10\"/>"u8, isReadingCompleted: true, state);

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.Equal(TokenType.Value, reader.TokenType);
            Assert.Equal("10", ValueText(ref reader));
            Assert.False(reader.ReaderState._hasSkippedAttributeDelimiter);
        }

        /// <summary>
        /// V1: the attribute value halts committing nothing, so the flag stays set for the next
        /// reader
        /// </summary>
        [Theory]
        [InlineData("\"1")]
        [InlineData("'")]
        public void SkippedAttributeDelimiter_ForV1ValueHalts_ItShould_KeepTheFlag(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: false, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false, TokenType.Element, hasSkippedAttributeDelimiter: true));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.False(advancement);
            Assert.Equal(0, reader.Position);
            Assert.Equal(TokenType.Attribute, reader.TokenType);
            Assert.True(reader.ReaderState._hasSkippedAttributeDelimiter);
        }
        #endregion

        #region R6-R9 (read cycle)
        /// <summary>
        /// R6: the flag was cleared by the first attribute's value, so the second attribute's
        /// missing equals non-terminal throws at its quote (<c>&lt;a b="1" c "2"&gt;</c>)
        /// </summary>
        [Fact]
        public void Read_ForR6SecondAttributeWithoutEquals_ItShould_ThrowExpectedEquals()
        {
            // Arrange
            Utf8XmlReader reader = new("<a b=\"1\" c \"2\">"u8, isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));
            for (int read = 0; read < 4; read++)
            {
                Assert.True(reader.Read());  // Element a, Attribute b, Value 1, Attribute c
            }

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 1, 12);
            Assert.Equal(Throwing.Markup.EqualsSignTerminal.ToString(), ((XmlException)thrown!).Markup);
        }

        /// <summary>
        /// R7, R8: after the equals non-terminal is committed, the start tag's ending terminal
        /// (content ready S4) or the empty element's (S5.a) throws attribute value missing at
        /// the ending terminal - #40's checks, reachable again
        /// </summary>
        [Theory]
        [InlineData("<a b=>")]      // R7
        [InlineData("<a b=/>")]     // R8
        [InlineData("<a b= >")]     // R7 spacing after = belongs to the equals non-terminal
        public void Read_ForR7R8EqualsWithoutLiteral_ItShould_ThrowAttributeValueMissing(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));
            reader.Read();  // Element a
            reader.Read();  // Attribute b

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 1, text.Length - (text.EndsWith("/>") ? 1 : 0));
            Assert.Equal(Throwing.Category.Expected.ToString(), ((XmlException)thrown!).Category);
            Assert.Equal(Throwing.Markup.AttributeValue.ToString(), ((XmlException)thrown!).Markup);
        }

        /// <summary>
        /// R9: skip delimiter halts (<c>&lt;a b= |"1"/&gt;</c>), nothing is committed and the
        /// flag stays false, so the next reader skips the equals non-terminal itself and reads
        /// the value
        /// </summary>
        [Fact]
        public void Read_ForR9EqualsHaltsAcrossReaders_ItShould_SkipItInTheNextReader()
        {
            // Arrange
            const string text = "<a b= \"1\"/>";
            Utf8XmlReader first = new(Encoding.UTF8.GetBytes(text[..6]), isReadingCompleted: false, ContextState(Context.Prolog, TokenType.None));
            first.Read();  // Element a
            first.Read();  // Attribute b
            bool halted = first.Read();
            long position = first.Position;
            ReaderState state = first.ReaderState;

            Utf8XmlReader second = new(Encoding.UTF8.GetBytes(text[(int)position..]), isReadingCompleted: true, state);

            // Act
            bool value = second.Read();
            TokenType valueType = second.TokenType;
            string valueText = ValueText(ref second);
            bool elementEnd = second.Read();

            // Assert
            Assert.False(halted);
            Assert.Equal(4, position);
            Assert.False(state._hasSkippedAttributeDelimiter);
            Assert.True(value);
            Assert.Equal(TokenType.Value, valueType);
            Assert.Equal("1", valueText);
            Assert.True(elementEnd);
            Assert.Equal(TokenType.ElementEnd, second.TokenType);
        }
        #endregion
    }
}
