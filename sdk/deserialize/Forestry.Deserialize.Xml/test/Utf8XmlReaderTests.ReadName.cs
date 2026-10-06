using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Read name shells, from #38 ------------------------------------------------------
        //
        // The `EvaluateName_*` and `ReadName_*` tests below are shells written from #38's
        // architecture (phase 1 S0-S3 evaluating a byte span, phase 2 S4-S9 blending the markup
        // state with the reader state), before the change exists - see doc/dev/Velocity.md's
        // Test shell phase.
        //
        // Phase 1 is a pure scan of the available bytes: it never knows whether reading is
        // completed. Phase 2 runs once on the final markup state, after phase 1 has continued
        // over every following segment while the state is Unknown.
        //
        // Test seam - PLACEHOLDER, not decided by the architecture, to be confirmed or renamed
        // during Understanding. Won't compile until it exists:
        // - `MarkupState` (#39) made internal, still nested in Utf8XmlReader
        // - phase 1 as `internal static MarkupState Utf8XmlReader.EvaluateNameNonTerminalMarkup(ReadOnlySpan<byte>
        //   characters, bool isFirstCharacter, out int nameLength, out bool unsupportedCharacter)` -
        //   isFirstCharacter is true only for the span holding the name's first character,
        //   nameLength is the count of name characters in this span
        // - phase 2 as `internal bool ReadName()` starting at the reader's current position
        //
        // Exceptions are only asserted by type (XmlException): the architecture doesn't decide
        // messages, so S5 (unsupported) and S6 (malformed) are told apart only by phase 1's
        // flag. Token types are set by the callers (#37, attribute name, end tag), so phase 2
        // asserts the current token is unchanged. Whether a name that fits in one segment of a
        // byte sequence is a Value or a ValueSequence isn't decided, so only a name that
        // straddles segments asserts HasValueSequence. The name count isn't asserted on
        // Malformed.

        #region helpers
        /// <summary>
        /// Read name catching the exception, a ref struct can't be captured by Assert.Throws
        /// </summary>
        private static Exception? ReadNameThrown(ref Utf8XmlReader reader)
        {
            try
            {
                reader.ReadName();
            }
            catch (Exception exception)
            {
                return exception;
            }

            return null;
        }
        #endregion

        #region S0
        /// <summary>
        /// S0: a non-ASCII byte is malformed and flags the unsupported character, checked before
        /// S1 (first character) and S2 (a non-ASCII byte never ends a name)
        /// </summary>
        [Theory]
        [InlineData("é", true)]
        [InlineData("aé", true)]
        [InlineData("aé>", true)]
        [InlineData("é", false)]
        [InlineData("Keyé>", false)]
        public void EvaluateName_ForS0NonAscii_ItShould_BeMalformedAndUnsupported(
            string text,
            bool isFirstCharacter
        ) {
            // Act
            Utf8XmlReader.MarkupState state = Utf8XmlReader.EvaluateNameNonTerminalMarkup(Encoding.UTF8.GetBytes(text), isFirstCharacter, out _, out bool unsupportedCharacter);

            // Assert
            Assert.Equal(Utf8XmlReader.MarkupState.Malformed, state);
            Assert.True(unsupportedCharacter);
        }
        #endregion

        #region S1
        /// <summary>
        /// S1: the name's first byte is not a name starting character
        /// </summary>
        [Theory]
        [InlineData("1a")]
        [InlineData("-a")]
        [InlineData(".a")]
        [InlineData("=")]
        [InlineData(">")]
        [InlineData("/")]
        [InlineData(" a")]
        [InlineData("\"a\"")]
        public void EvaluateName_ForS1NotNameStartingCharacter_ItShould_BeMalformed(
            string text
        ) {
            // Act
            Utf8XmlReader.MarkupState state = Utf8XmlReader.EvaluateNameNonTerminalMarkup(Encoding.UTF8.GetBytes(text), isFirstCharacter: true, out _, out bool unsupportedCharacter);

            // Assert
            Assert.Equal(Utf8XmlReader.MarkupState.Malformed, state);
            Assert.False(unsupportedCharacter);
        }

        /// <summary>
        /// S1 only applies to the name's first character: a following span starting with a
        /// name character that can't start a name continues the name
        /// </summary>
        [Theory]
        [InlineData("1a", 2)]
        [InlineData("-a", 2)]
        [InlineData(".a", 2)]
        public void EvaluateName_ForS1NotFirstCharacter_ItShould_ContinueTheName(
            string text,
            int expectedNameLength
        ) {
            // Act
            Utf8XmlReader.MarkupState state = Utf8XmlReader.EvaluateNameNonTerminalMarkup(Encoding.UTF8.GetBytes(text), isFirstCharacter: false, out int nameLength, out bool unsupportedCharacter);

            // Assert
            Assert.Equal(Utf8XmlReader.MarkupState.Unknown, state);
            Assert.Equal(expectedNameLength, nameLength);
            Assert.False(unsupportedCharacter);
        }
        #endregion

        #region S2
        /// <summary>
        /// S2: a following byte is not a name character, so the name ends before it
        /// </summary>
        [Theory]
        [InlineData("a>", 1)]
        [InlineData("a b", 1)]
        [InlineData("a/", 1)]
        [InlineData("a=", 1)]
        [InlineData("a\n", 1)]
        [InlineData("StemKey>", 7)]
        [InlineData("_a:b-c.d1>", 9)]
        [InlineData(":a>", 2)]
        public void EvaluateName_ForS2NotNameCharacter_ItShould_BeWellFormed(
            string text,
            int expectedNameLength
        ) {
            // Act
            Utf8XmlReader.MarkupState state = Utf8XmlReader.EvaluateNameNonTerminalMarkup(Encoding.UTF8.GetBytes(text), isFirstCharacter: true, out int nameLength, out bool unsupportedCharacter);

            // Assert
            Assert.Equal(Utf8XmlReader.MarkupState.WellFormed, state);
            Assert.Equal(expectedNameLength, nameLength);
            Assert.False(unsupportedCharacter);
        }

        /// <summary>
        /// S2 in a following span: the name ended exactly at the previous segment boundary
        /// (<c>Stem|&gt;</c>) or continues into this span (<c>Stem|Key&gt;</c>)
        /// </summary>
        [Theory]
        [InlineData(">", 0)]
        [InlineData("=", 0)]
        [InlineData(" ", 0)]
        [InlineData("Key>", 3)]
        public void EvaluateName_ForS2NotFirstCharacter_ItShould_BeWellFormed(
            string text,
            int expectedNameLength
        ) {
            // Act
            Utf8XmlReader.MarkupState state = Utf8XmlReader.EvaluateNameNonTerminalMarkup(Encoding.UTF8.GetBytes(text), isFirstCharacter: false, out int nameLength, out bool unsupportedCharacter);

            // Assert
            Assert.Equal(Utf8XmlReader.MarkupState.WellFormed, state);
            Assert.Equal(expectedNameLength, nameLength);
            Assert.False(unsupportedCharacter);
        }
        #endregion

        #region S3
        /// <summary>
        /// S3: every byte is a name character so the span is exhausted without a verdict,
        /// including an empty span
        /// </summary>
        [Theory]
        [InlineData("a", true, 1)]
        [InlineData("StemKey", true, 7)]
        [InlineData("Key", false, 3)]
        [InlineData("", true, 0)]
        [InlineData("", false, 0)]
        public void EvaluateName_ForS3SpanExhausted_ItShould_BeUnknown(
            string text,
            bool isFirstCharacter,
            int expectedNameLength
        ) {
            // Act
            Utf8XmlReader.MarkupState state = Utf8XmlReader.EvaluateNameNonTerminalMarkup(Encoding.UTF8.GetBytes(text), isFirstCharacter, out int nameLength, out bool unsupportedCharacter);

            // Assert
            Assert.Equal(Utf8XmlReader.MarkupState.Unknown, state);
            Assert.Equal(expectedNameLength, nameLength);
            Assert.False(unsupportedCharacter);
        }
        #endregion

        #region S4
        /// <summary>
        /// S4: well-formed, so the reader advances past the name only, commits the value and
        /// the line position, and leaves the token type to the caller
        /// </summary>
        [Theory]
        [InlineData("a>", Context.StartTag, TokenType.Element, "a")]
        [InlineData("StemKey b=\"1\">", Context.StartTag, TokenType.Element, "StemKey")]
        [InlineData("a/>", Context.StartTag, TokenType.Element, "a")]
        [InlineData("b=\"1\"", Context.StartTag, TokenType.Element, "b")]
        [InlineData("b =\"1\"", Context.StartTag, TokenType.Value, "b")]
        [InlineData("a>", Context.Content, TokenType.Value, "a")]
        public void ReadName_ForS4WellFormed_ItShould_AdvancePastTheName(
            string text,
            Context context,
            TokenType current,
            string expectedName
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(context, current));

            // Act
            bool advancement = reader.ReadName();

            // Assert
            Assert.True(advancement);
            Assert.Equal(expectedName, ValueText(ref reader));
            Assert.False(reader.HasValueSequence);
            Assert.Equal(expectedName.Length, reader.Position);
            Assert.Equal(0, reader.ReaderState._lineNumber);
            Assert.Equal(expectedName.Length, reader.ReaderState._linePosition);
            Assert.Equal(current, reader.TokenType);
        }

        /// <summary>
        /// S4: the line position advances from where the name starts
        /// </summary>
        [Fact]
        public void ReadName_ForS4WellFormedAfterLinePosition_ItShould_AdvanceTheLinePosition()
        {
            // Arrange - e.g. "\n  <a " already read up to the attribute name
            ReaderState state = OpaqueState(Context.StartTag, TokenType.Element, lineNumber: 1, linePosition: 5);
            Utf8XmlReader reader = new("StemKey=\"1\""u8, isReadingCompleted: true, state);

            // Act
            bool advancement = reader.ReadName();

            // Assert
            Assert.True(advancement);
            Assert.Equal(1, reader.ReaderState._lineNumber);
            Assert.Equal(12, reader.ReaderState._linePosition);
        }

        /// <summary>
        /// S4 straddling segments: phase 1 continues over the following segments while the
        /// state is Unknown, so the name is a value sequence
        /// </summary>
        [Theory]
        [InlineData(new[] { "Stem", "Key>" }, "StemKey")]
        [InlineData(new[] { "Stem", "", "Key>" }, "StemKey")]
        [InlineData(new[] { "S", "te", "mKey b=\"1\"" }, "StemKey")]
        [InlineData(new[] { "", "St", "emKey>" }, "StemKey")]
        public void ReadName_ForS4WellFormedStraddlingSegments_ItShould_SetAValueSequence(
            string[] parts,
            string expectedName
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, ContextState(Context.StartTag, TokenType.Element));

            // Act
            bool advancement = reader.ReadName();

            // Assert
            Assert.True(advancement);
            Assert.Equal(expectedName, ValueText(ref reader));
            Assert.True(reader.HasValueSequence);
            Assert.Equal(expectedName.Length, reader.Position);
            Assert.Equal(expectedName.Length, reader.ReaderState._linePosition);
        }

        /// <summary>
        /// S4 with the name in one segment and the character ending it in the next
        /// (<c>Stem|&gt;</c>): Value or ValueSequence isn't decided, so only the text is asserted
        /// </summary>
        [Theory]
        [InlineData(new[] { "Stem", ">" }, "Stem")]
        [InlineData(new[] { "Stem", "", " b=\"1\"" }, "Stem")]
        public void ReadName_ForS4WellFormedEndingInNextSegment_ItShould_AdvancePastTheName(
            string[] parts,
            string expectedName
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, ContextState(Context.StartTag, TokenType.Element));

            // Act
            bool advancement = reader.ReadName();

            // Assert
            Assert.True(advancement);
            Assert.Equal(expectedName, ValueText(ref reader));
            Assert.Equal(expectedName.Length, reader.Position);
        }
        #endregion

        #region S5
        /// <summary>
        /// S5: malformed and unsupported, a non-ASCII character in the name throws
        /// </summary>
        [Theory]
        [InlineData("é>")]
        [InlineData("aé>")]
        [InlineData("Stemé")]
        public void ReadName_ForS5Unsupported_ItShould_Throw(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: false, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadNameThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }

        /// <summary>
        /// S5 straddling segments: the non-ASCII character is in a following segment
        /// </summary>
        [Theory]
        [InlineData([new[] { "Stem", "é>" }])]
        [InlineData([new[] { "Stem", "", "Keyé>" }])]
        [InlineData([new[] { "", "é>" }])]
        public void ReadName_ForS5UnsupportedStraddlingSegments_ItShould_Throw(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: false, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadNameThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion

        #region S6
        /// <summary>
        /// S6: malformed and not unsupported, a first character that can't start a name throws,
        /// e.g. an attribute name missing before <c>=</c> (<c>&lt;a ="1"&gt;</c>)
        /// </summary>
        [Theory]
        [InlineData("=\"1\">", false)]
        [InlineData("1a>", false)]
        [InlineData("-a>", true)]
        [InlineData(">", true)]
        public void ReadName_ForS6Malformed_ItShould_Throw(
            string text,
            bool isReadingCompleted
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadNameThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }

        /// <summary>
        /// S6 after empty segments: the name's first byte is in a later segment, so the first
        /// character check follows the first byte, not the first segment
        /// </summary>
        [Theory]
        [InlineData([new[] { "", "=\"1\">" }])]
        [InlineData([new[] { "", "", "1a>" }])]
        public void ReadName_ForS6MalformedAfterEmptySegments_ItShould_Throw(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadNameThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion

        #region S7
        /// <summary>
        /// S7: unknown and reading not completed, so nothing is advanced (simulated rollback)
        /// and the read waits for more bytes
        /// </summary>
        [Theory]
        [InlineData("Stem")]
        [InlineData("a")]
        [InlineData("")]
        public void ReadName_ForS7UnknownWhenReadingNotCompleted_ItShould_ReturnFalseWithoutAdvancement(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: false, ContextState(Context.StartTag, TokenType.Element));

            // Act
            bool advancement = reader.ReadName();

            // Assert
            Assert.False(advancement);
            Assert.Equal(0, reader.Position);
            Assert.Equal(0, reader.ReaderState._linePosition);
            Assert.Equal(TokenType.Element, reader.TokenType);
        }

        /// <summary>
        /// S7 straddling segments: every segment is name characters, so phase 2 runs once
        /// after the last one, not on the first segment's Unknown
        /// </summary>
        [Theory]
        [InlineData([new[] { "Stem", "Key" }])]
        [InlineData([new[] { "Stem", "", "Key" }])]
        [InlineData([new[] { "", "" }])]
        public void ReadName_ForS7UnknownStraddlingSegments_ItShould_ReturnFalseWithoutAdvancement(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: false, ContextState(Context.StartTag, TokenType.Element));
            SequencePosition sequencePosition = reader.SequencePosition;

            // Act
            bool advancement = reader.ReadName();

            // Assert
            Assert.False(advancement);
            Assert.Equal(0, reader.Position);
            Assert.Equal(sequencePosition, reader.SequencePosition);
            Assert.Equal(0, reader.ReaderState._linePosition);
        }
        #endregion

        #region S8
        /// <summary>
        /// S8: unknown, reading completed and at least one name character, so the name is
        /// committed as in S4
        /// </summary>
        [Theory]
        [InlineData("Stem", "Stem")]
        [InlineData("a", "a")]
        public void ReadName_ForS8UnknownWhenReadingCompleted_ItShould_CommitTheName(
            string text,
            string expectedName
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(Context.StartTag, TokenType.Element));

            // Act
            bool advancement = reader.ReadName();

            // Assert
            Assert.True(advancement);
            Assert.Equal(expectedName, ValueText(ref reader));
            Assert.Equal(expectedName.Length, reader.Position);
            Assert.Equal(expectedName.Length, reader.ReaderState._linePosition);
        }

        /// <summary>
        /// S8 straddling segments
        /// </summary>
        [Theory]
        [InlineData(new[] { "Stem", "Key" }, "StemKey")]
        [InlineData(new[] { "Stem", "", "Key" }, "StemKey")]
        public void ReadName_ForS8UnknownWhenReadingCompletedStraddlingSegments_ItShould_CommitTheName(
            string[] parts,
            string expectedName
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, ContextState(Context.StartTag, TokenType.Element));

            // Act
            bool advancement = reader.ReadName();

            // Assert
            Assert.True(advancement);
            Assert.Equal(expectedName, ValueText(ref reader));
            Assert.True(reader.HasValueSequence);
            Assert.Equal(expectedName.Length, reader.Position);
        }

        /// <summary>
        /// S8: the next read cycle throws because the element is not ended
        /// (<c>&lt;Stem</c> at the end of the document)
        /// </summary>
        [Fact]
        public void ReadName_ForS8UnknownWhenReadingCompleted_ItShould_ThrowElementNotEndedOnTheNextRead()
        {
            // Arrange
            Utf8XmlReader reader = new("Stem"u8, isReadingCompleted: true, ContextState(Context.StartTag, TokenType.Element));
            reader.ReadName();

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion

        #region S9
        /// <summary>
        /// S9: unknown, reading completed and no name characters, e.g. an attribute name at the
        /// end of the document
        /// </summary>
        [Fact]
        public void ReadName_ForS9NoNameWhenReadingCompleted_ItShould_Throw()
        {
            // Arrange
            Utf8XmlReader reader = new(""u8, isReadingCompleted: true, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadNameThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }

        /// <summary>
        /// S9 straddling segments: only empty segments are left
        /// </summary>
        [Fact]
        public void ReadName_ForS9NoNameWhenReadingCompletedStraddlingSegments_ItShould_Throw()
        {
            // Arrange
            Utf8XmlReader reader = new(Sequence("", ""), isReadingCompleted: true, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadNameThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion
    }
}
