using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Read attribute guard shells, from bug #41 ---------------------------------------
        //
        // The tests below are shells written from #41's architecture (a guard at the start of
        // the attribute delegate, before #40's S0 spacing check: when the current token is
        // Attribute and the equals flag is set (#45), the equals non-terminal was skipped but no
        // literal follows, so the attribute value is missing), before the code exists - see
        // doc/dev/Velocity.md's Test shell phase.
        //
        // The guard reads no bytes: the reader already sits on the offending byte, so the
        // exception's position is the reader position. It reuses Expected / AttributeValue, the
        // same pair content ready S4 (`<a b=>`) and empty element S5.a (`<a b=/>`) throw.
        //
        // The delegate is reached through ReadValue() (candidate S6, an empty scratch pad in a
        // start tag), so no test seam is needed.

        #region helpers
        /// <summary>
        /// Inside a start tag after an attribute name whose equals non-terminal was skipped
        /// (#45), the reader at the byte after it
        /// </summary>
        private static ReaderState AfterEqualsState(bool hasSkippedSpacingInStartTag = false, long linePosition = 0) =>
            StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag, TokenType.Element, linePosition: linePosition, hasSkippedAttributeDelimiter: true);

        /// <summary>
        /// The thrown exception is attribute value missing (Expected, AttributeValue) at the
        /// 1-based line number and line position
        /// </summary>
        private static void AssertAttributeValueMissingAt(Exception? thrown, long lineNumber, long linePosition)
        {
            AssertXmlExceptionAt(thrown, lineNumber, linePosition);
            Assert.Equal(Throwing.Category.Expected.ToString(), ((XmlException)thrown!).Category);
            Assert.Equal(Throwing.Markup.AttributeValue.ToString(), ((XmlException)thrown!).Markup);
        }
        #endregion

        #region G0-G2
        /// <summary>
        /// G0-G2: whatever byte follows the equals non-terminal without being a quote reaches the
        /// attribute delegate (an empty scratch pad), which throws attribute value missing at it
        /// committing nothing (<c>&lt;a b=1/&gt;</c>, <c>&lt;a b=c="2"&gt;</c>)
        /// </summary>
        [Theory]
        [InlineData("1/>")]          // G0 a name character
        [InlineData("1>")]           // G0
        [InlineData("c=\"2\">")]     // G1 another attribute, its name is not read
        [InlineData("StemKey='1'>")] // G1
        [InlineData("==\">")]        // G2 a byte that starts no terminal
        [InlineData("-1>")]          // G2
        public void ReadAttribute_ForGuardAfterEquals_ItShould_ThrowAttributeValueMissingCommittingNothing(
            string text
        ) {
            // Arrange - e.g. "<a b=" already read up to the byte after '='
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, AfterEqualsState(linePosition: 5));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertAttributeValueMissingAt(thrown, 1, 6);
            Assert.Equal(0, reader.Position);
            Assert.Equal(5, reader.ReaderState._linePosition);
            Assert.Equal(TokenType.Attribute, reader.TokenType);
            Assert.False(reader.HasValueSequence);
            Assert.Equal(0, reader.Value.Length);
        }

        /// <summary>
        /// G0-G2 are decided by the reader state alone, so the guard throws whether or not
        /// reading is completed and across segments - it never halts
        /// </summary>
        [Theory]
        [InlineData([new[] { "1", ">" }])]
        [InlineData([new[] { "c", "=\"2\">" }])]
        [InlineData([new[] { "St", "emKey" }])]   // the name would be undecided, the guard comes first
        public void ReadAttribute_ForGuardAfterEqualsNotCompletedStraddlingSegments_ItShould_Throw(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: false, AfterEqualsState());

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertAttributeValueMissingAt(thrown, 1, 1);
            Assert.Equal(0, reader.Position);
        }
        #endregion

        #region G3
        /// <summary>
        /// G3: the guard comes before #40's S0, so attribute value missing is thrown whether or
        /// not spacing was skipped - never spacing missing
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReadAttribute_ForG3GuardBeforeSpacingCheck_ItShould_ThrowAttributeValueMissing(
            bool hasSkippedSpacingInStartTag
        ) {
            // Arrange
            Utf8XmlReader reader = new("1/>"u8, isReadingCompleted: true, AfterEqualsState(hasSkippedSpacingInStartTag));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertAttributeValueMissingAt(thrown, 1, 1);
            Assert.NotEqual(Throwing.Markup.Spacing.ToString(), ((XmlException)thrown!).Markup);
        }
        #endregion

        #region G4 (guard not active)
        /// <summary>
        /// G4: after a value the current token is not Attribute, so the guard is not active and
        /// the next attribute is read (#40's S3), whatever the equals flag
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReadAttribute_ForG4AfterValue_ItShould_ReadTheAttribute(
            bool hasSkippedAttributeDelimiter
        ) {
            // Arrange
            Utf8XmlReader reader = new("c=\"2\">"u8, isReadingCompleted: true, StartTagState(TokenType.Value, hasSkippedSpacingInStartTag: true, TokenType.Attribute, hasSkippedAttributeDelimiter: hasSkippedAttributeDelimiter));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.Equal(TokenType.Attribute, reader.TokenType);
            Assert.Equal("c", ValueText(ref reader));
        }

        /// <summary>
        /// The guard needs the equals flag: with the current token Attribute but the flag not set
        /// the delegate behaves as before #41 (#40's S0 when no spacing, else #38's malformed
        /// first character)
        /// </summary>
        [Theory]
        [InlineData(false, "Spacing")]
        [InlineData(true, "Name")]
        public void ReadAttribute_ForAttributeTokenWithoutEqualsFlag_ItShould_NotThrowAttributeValueMissing(
            bool hasSkippedSpacingInStartTag,
            string expectedMarkup
        ) {
            // Arrange
            Utf8XmlReader reader = new("1=\"x\">"u8, isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag, TokenType.Element, hasSkippedAttributeDelimiter: false));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            XmlException exception = Assert.IsType<XmlException>(thrown);
            Assert.Equal(expectedMarkup, exception.Markup);
        }
        #endregion

        #region R0-R2 (read cycle)
        /// <summary>
        /// R0, R1: through Read(), the element and the attribute name are read, then the byte
        /// after the equals non-terminal throws attribute value missing at it
        /// </summary>
        [Theory]
        [InlineData("<a b=1/>", 6)]       // R0
        [InlineData("<a b= 1>", 7)]       // R1 spacing after '=' belongs to the equals non-terminal
        [InlineData("<a b=c=\"2\">", 6)]  // G1 through Read()
        public void Read_ForR0R1NoLiteralAfterEquals_ItShould_ThrowAttributeValueMissing(
            string text,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));
            Assert.True(reader.Read());  // Element a
            Assert.True(reader.Read());  // Attribute b

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            AssertAttributeValueMissingAt(thrown, 1, expectedLinePosition);
        }

        /// <summary>
        /// R2: the first reader halts in skip delimiter (<c>=</c> is its last byte, #32's S7),
        /// so nothing is committed and the equals flag stays false; the next reader skips
        /// <c>=</c> itself, sets the flag, and the guard throws at <c>1</c>
        /// (<c>&lt;a b=|1&gt;</c>)
        /// </summary>
        [Fact]
        public void Read_ForR2EqualsLastByteAcrossReaders_ItShould_ThrowInTheNextReader()
        {
            // Arrange
            const string text = "<a b=1>";
            Utf8XmlReader first = new(Encoding.UTF8.GetBytes(text[..5]), isReadingCompleted: false, ContextState(Context.Prolog, TokenType.None));
            first.Read();  // Element a
            first.Read();  // Attribute b
            bool halted = first.Read();
            long position = first.Position;
            ReaderState state = first.ReaderState;

            Utf8XmlReader second = new(Encoding.UTF8.GetBytes(text[(int)position..]), isReadingCompleted: true, state);

            // Act
            Exception? thrown = ReadThrown(ref second);

            // Assert
            Assert.False(halted);
            Assert.Equal(4, position);
            Assert.False(state._hasSkippedAttributeDelimiter);
            AssertAttributeValueMissingAt(thrown, 1, 6);
        }
        #endregion
    }
}
