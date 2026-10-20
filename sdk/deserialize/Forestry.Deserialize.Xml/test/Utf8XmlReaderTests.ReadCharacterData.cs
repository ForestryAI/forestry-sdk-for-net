using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Read content character data shells, from #48 -----------------------------------
        //
        // The `SkipSpacingOnlyCharacterData_*`, `EvaluateCharacterData_*`, `ReadCharacterData_*`
        // and `Read_*` tests below are shells written from #48's architecture (a guard skipping
        // spacing-only character data right after the start tag's '>', a character data
        // evaluation stopping at '<' or '&', and the delegate reacting to its markup state),
        // before the code exists - see doc/dev/Velocity.md's Test shell phase.
        //
        // The regions follow #48's Assertions: guard (G0-G8), evaluation (E0-E9), delegate
        // (S0-S7), multiple segments (M0-M7) and the read cycle (R0-R7).
        //
        // - The guard runs inside Read(), so its rows start at the start tag's '>' with the
        //   current token Element (or Value after Attribute) and call Read().
        // - The delegate is reached through ReadValue() (candidate S8), so no shell names the
        //   delegate method. Its rows start at the text with the current token Element.
        // - PLACEHOLDER seam, the only compile error until the evaluation exists:
        //   `internal static MarkupState Utf8XmlReader.EvaluateCharacterDataNonTerminalMarkup(
        //   ReadOnlySpan<byte> characters, out int length, out bool unsupportedCharacter)` -
        //   rename it here when the real name is chosen.
        // - The CharacterData throwing markup doesn't exist yet, so it is compared by name as a
        //   string (as #47 did for EndTag).
        //
        // The open element is "E0" (ElementStackAtDepth), so end tags in the examples are </E0>.

        #region helpers
        private const string CharacterDataMarkup = "CharacterData";

        /// <summary>
        /// A reader at the start tag's <c>&gt;</c> with the current token Element, as after the
        /// start tag name was read
        /// </summary>
        private static Utf8XmlReader AfterStartTagNameReader(string text, bool isReadingCompleted) =>
            new(Encoding.UTF8.GetBytes(text), isReadingCompleted, ContextState(Context.StartTag, TokenType.Element));

        /// <summary>
        /// A reader at the start tag's <c>&gt;</c> straddling segments with the current token Element
        /// </summary>
        private static Utf8XmlReader AfterStartTagNameReader(string[] parts, bool isReadingCompleted) =>
            new(Sequence(parts), isReadingCompleted, ContextState(Context.StartTag, TokenType.Element));

        /// <summary>
        /// A reader at the text in content with the current token Element, as after content
        /// ready skipped the start tag's <c>&gt;</c>
        /// </summary>
        private static Utf8XmlReader CharacterDataReader(string text, bool isReadingCompleted) =>
            new(Encoding.UTF8.GetBytes(text), isReadingCompleted, ContextState(Context.Content, TokenType.Element));

        /// <summary>
        /// A reader at the text in content straddling segments with the current token Element
        /// </summary>
        private static Utf8XmlReader CharacterDataReader(string[] parts, bool isReadingCompleted) =>
            new(Sequence(parts), isReadingCompleted, ContextState(Context.Content, TokenType.Element));

        /// <summary>
        /// Assert the reader after character data was read: Value with the text, previous token
        /// Element and the positions past the text
        /// </summary>
        private static void AssertCharacterDataRead(
            ref Utf8XmlReader reader,
            string expectedText,
            long expectedPosition,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            Assert.Equal(TokenType.Value, reader.TokenType);
            Assert.Equal(TokenType.Element, reader.ReaderState._previousTokenType);
            Assert.Equal(expectedText, ValueText(ref reader));

            Assert.Equal(expectedPosition, reader.Position);
            Assert.Equal(expectedLineNumber, reader.ReaderState._lineNumber);
            Assert.Equal(expectedLinePosition, reader.ReaderState._linePosition);
        }

        /// <summary>
        /// Assert nothing was committed: position, line tracking and token types unchanged
        /// </summary>
        private static void AssertCharacterDataNothingCommitted(ref Utf8XmlReader reader)
        {
            Assert.Equal(0, reader.Position);
            Assert.Equal(0, reader.ReaderState._lineNumber);
            Assert.Equal(0, reader.ReaderState._linePosition);
            Assert.Equal(TokenType.Element, reader.TokenType);
            Assert.Equal(TokenType.None, reader.ReaderState._previousTokenType);
        }

        /// <summary>
        /// The thrown exception is an XML exception with the category and markup (by name) at
        /// the 1-based position
        /// </summary>
        private static void AssertCharacterDataException(
            Exception? thrown,
            string category,
            long lineNumber,
            long linePosition
        ) {
            XmlException exception = Assert.IsType<XmlException>(thrown);
            Assert.Equal(category, exception.Category);
            Assert.Equal(CharacterDataMarkup, exception.Markup);
            Assert.Equal(lineNumber, exception.LineNumber);
            Assert.Equal(linePosition, exception.LinePosition);
        }
        #endregion

        #region G0-G8 (guard)
        /// <summary>
        /// G0: the guard only runs right after the start tag's <c>&gt;</c>. In later cycles
        /// (here the current token is Comment) skip spacing already ignores spacing-only text.
        /// A regression guard - it passes today, and its positions are the same as G8's.
        /// </summary>
        [Fact]
        public void SkipSpacingOnlyCharacterData_ForG0NotAfterStartTag_ItShould_LeaveItToSkipSpacing()
        {
            // Arrange
            Utf8XmlReader reader = new("\n  <b/>"u8, isReadingCompleted: true, ContextState(Context.Content, TokenType.Comment));

            // Act
            bool advancement = reader.Read();

            // Assert
            Assert.True(advancement);
            Assert.Equal(TokenType.Element, reader.TokenType);
            Assert.Equal("b", ValueText(ref reader));
            Assert.Equal(1, reader.ReaderState._lineNumber);
            Assert.Equal(4, reader.ReaderState._linePosition);
        }

        /// <summary>
        /// G1, G3: no spacing, or spacing followed by text, so nothing is skipped and the text
        /// (with its leading spacing) is the Value
        /// </summary>
        [Theory]
        [InlineData(">x</E0>", "x")]                 // G1
        [InlineData(">\n  x</E0>", "\n  x")]         // G3
        [InlineData("> x</E0>", " x")]               // G3
        [InlineData(">\t\"x\"</E0>", "\t\"x\"")]     // G3 a quote after the spacing
        public void SkipSpacingOnlyCharacterData_ForG1G3Text_ItShould_SkipNothing(
            string text,
            string expectedText
        ) {
            // Arrange
            Utf8XmlReader reader = AfterStartTagNameReader(text, isReadingCompleted: true);

            // Act
            bool advancement = reader.Read();

            // Assert
            Assert.True(advancement);
            Assert.Equal(TokenType.Value, reader.TokenType);
            Assert.Equal(expectedText, ValueText(ref reader));
        }

        /// <summary>
        /// G2, G8: spacing followed by <c>&lt;</c> is skipped with line tracking, so the next
        /// token is the child element and no Value. G8's positions are after the child's name
        /// (the skip leaves line 1, position 2; <c>&lt;b</c> adds 2).
        /// </summary>
        [Theory]
        [InlineData(">\n  <b/>", 1, 4)]      // G2, G8
        [InlineData(">\r\n\t<b/>", 1, 3)]    // G2
        public void SkipSpacingOnlyCharacterData_ForG2G8SpacingOnly_ItShould_SkipToTheChild(
            string text,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = AfterStartTagNameReader(text, isReadingCompleted: true);

            // Act
            bool advancement = reader.Read();

            // Assert
            Assert.True(advancement);
            Assert.Equal(TokenType.Element, reader.TokenType);
            Assert.Equal("b", ValueText(ref reader));
            Assert.Equal(expectedLineNumber, reader.ReaderState._lineNumber);
            Assert.Equal(expectedLinePosition, reader.ReaderState._linePosition);
        }

        /// <summary>
        /// G4: the spacing runs to the end of the segment and reading is not completed, so the
        /// guard halts: the <c>&gt;</c> stays committed (content ready), the spacing doesn't
        /// </summary>
        [Theory]
        [InlineData(">  ")]
        [InlineData(">\n")]
        public void SkipSpacingOnlyCharacterData_ForG4SegmentEnds_ItShould_HaltKeepingTheGreaterThan(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = AfterStartTagNameReader(text, isReadingCompleted: false);

            // Act
            bool advancement = reader.Read();

            // Assert
            Assert.False(advancement);
            Assert.Equal(1, reader.Position);
            Assert.Equal(0, reader.ReaderState._lineNumber);
            Assert.True(reader.ReaderState._elementStack.ContentReady);
        }

        /// <summary>
        /// G5: the spacing runs to the end of the data and reading is completed, so Incomplete
        /// is thrown at the end of the data. Every row has a line feed after the '>': content
        /// ready's skip doesn't add to the line position yet (bug #30), so a position on the
        /// first line would be one early for a reason outside #48.
        /// </summary>
        [Theory]
        [InlineData(">\n", 2, 1)]
        [InlineData(">\t\n ", 2, 2)]
        public void SkipSpacingOnlyCharacterData_ForG5DataEnds_ItShould_ThrowIncomplete(
            string text,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = AfterStartTagNameReader(text, isReadingCompleted: true);

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            AssertCharacterDataException(thrown, nameof(Throwing.Category.Incomplete), expectedLineNumber, expectedLinePosition);
        }

        /// <summary>
        /// G6: the start tag ends after an attribute's value (current Value, previous Attribute),
        /// so the guard runs too
        /// </summary>
        [Fact]
        public void SkipSpacingOnlyCharacterData_ForG6AfterAttributeValue_ItShould_SkipToTheChild()
        {
            // Arrange
            Utf8XmlReader reader = new(
                ">\n<c/>"u8,
                isReadingCompleted: true,
                State(ElementStackAtDepth(1, contentReady: false), TokenType.Value, TokenType.Attribute));

            // Act
            bool advancement = reader.Read();

            // Assert
            Assert.True(advancement);
            Assert.Equal(TokenType.Element, reader.TokenType);
            Assert.Equal("c", ValueText(ref reader));
        }

        /// <summary>
        /// G7: <c>&gt;</c> is the last byte, so the drained check halts before the guard reads
        /// past it. A regression guard - it passes today.
        /// </summary>
        [Fact]
        public void SkipSpacingOnlyCharacterData_ForG7GreaterThanLastByte_ItShould_HaltBeforeTheGuard()
        {
            // Arrange
            Utf8XmlReader reader = AfterStartTagNameReader(">", isReadingCompleted: false);

            // Act
            bool advancement = reader.Read();

            // Assert
            Assert.False(advancement);
            Assert.Equal(1, reader.Position);
            Assert.True(reader.ReaderState._elementStack.ContentReady);
        }
        #endregion

        #region E0-E9 (evaluation)
        /// <summary>
        /// E0-E9: the evaluation stops at <c>&lt;</c> (well-formed), <c>&amp;</c> (well-formed and
        /// unsupported) or a control byte other than spacing (malformed); every other byte is
        /// plain text; without a stop it is Unknown. The length is the bytes before the stop.
        /// The state is passed by name: a public test method can't take the internal enum.
        /// </summary>
        [Theory]
        [InlineData("Gary<", "WellFormed", 4, false)]       // E0
        [InlineData("Gary&", "WellFormed", 4, true)]        // E1
        [InlineData("&amp;", "WellFormed", 0, true)]        // E2
        [InlineData("<", "WellFormed", 0, false)]           // E0 at once
        [InlineData("Ga\u0001ry<", "Malformed", 2, false)]  // E3
        [InlineData("\u001F<", "Malformed", 0, false)]      // E3
        [InlineData("\u0000", "Malformed", 0, false)]       // E3
        [InlineData("\t\n\r x<", "WellFormed", 5, false)]   // E4
        [InlineData("Gärd<", "WellFormed", 5, false)]       // E5 'ä' is 2 bytes
        [InlineData("\"'>=<", "WellFormed", 4, false)]      // E6
        [InlineData("\u007F<", "WellFormed", 1, false)]     // E7
        [InlineData("Gary", "Unknown", 4, false)]           // E8
        [InlineData("Ga\n", "Unknown", 3, false)]           // E8
        [InlineData("", "Unknown", 0, false)]               // E9
        public void EvaluateCharacterData_ForE0ToE9_ItShould_StopAtTheFirstDecidingByte(
            string text,
            string expectedState,
            int expectedLength,
            bool expectedUnsupported
        ) {
            // Act
            Utf8XmlReader.MarkupState state = Utf8XmlReader.EvaluateCharacterDataNonTerminalMarkup(
                Encoding.UTF8.GetBytes(text),
                out int length,
                out bool unsupportedCharacter);

            // Assert
            Assert.Equal(expectedState, state.ToString());
            Assert.Equal(expectedLength, length);
            Assert.Equal(expectedUnsupported, unsupportedCharacter);
        }
        #endregion

        #region S0-S7 (delegate)
        /// <summary>
        /// S0, S6, S7: well-formed text before <c>&lt;</c> is the Value (quotes and <c>&gt;</c> are
        /// plain text, line feeds are tracked); the <c>&lt;</c> isn't consumed
        /// </summary>
        [Theory]
        [InlineData("Gary</E0>", "Gary", 4, 0, 4)]             // S0
        [InlineData("x>y</E0>", "x>y", 3, 0, 3)]               // S0 '>' is plain text
        [InlineData("\"Gary\"</E0>", "\"Gary\"", 6, 0, 6)]     // S6 double quote pad
        [InlineData("'Gary'</E0>", "'Gary'", 6, 0, 6)]         // S6 single quote pad
        [InlineData("Ga\nry</E0>", "Ga\nry", 5, 1, 2)]         // S7
        [InlineData("Gary\n</E0>", "Gary\n", 5, 1, 0)]         // S7 trailing spacing is text
        [InlineData("Gärd</E0>", "Gärd", 5, 0, 5)]             // S0 line position counts bytes
        public void ReadCharacterData_ForS0S6S7Text_ItShould_SetTheValue(
            string text,
            string expectedText,
            long expectedPosition,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = CharacterDataReader(text, isReadingCompleted: true);

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.False(reader.HasValueSequence);
            AssertCharacterDataRead(ref reader, expectedText, expectedPosition, expectedLineNumber, expectedLinePosition);
        }

        /// <summary>
        /// S1: an <c>&amp;</c> at the reader position (length 0, unsupported) throws Expected at the
        /// <c>&amp;</c> - temporary until the unsupported exception is designed; nothing committed
        /// </summary>
        [Fact]
        public void ReadCharacterData_ForS1AmpersandFirst_ItShould_ThrowCommittingNothing()
        {
            // Arrange
            Utf8XmlReader reader = CharacterDataReader("&amp;</E0>", isReadingCompleted: true);

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertCharacterDataException(thrown, nameof(Throwing.Category.Expected), 1, 1);
            AssertCharacterDataNothingCommitted(ref reader);
        }

        /// <summary>
        /// S2: the text before an <c>&amp;</c> is the Value; the next read starts at the
        /// <c>&amp;</c> and throws as S1
        /// </summary>
        [Fact]
        public void ReadCharacterData_ForS2TextBeforeAmpersand_ItShould_SetTheValueThenThrow()
        {
            // Arrange
            Utf8XmlReader reader = CharacterDataReader("Gary&amp;</E0>", isReadingCompleted: true);

            // Act
            bool advancement = reader.ReadValue();
            string value = ValueText(ref reader);
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.True(advancement);
            Assert.Equal("Gary", value);
            AssertCharacterDataException(thrown, nameof(Throwing.Category.Expected), 1, 5);
            Assert.Equal(4, reader.Position);
        }

        /// <summary>
        /// S3: a control byte other than spacing throws Expected at that byte, even when reading
        /// is not completed (the first deciding byte wins); nothing committed
        /// </summary>
        [Theory]
        [InlineData("Ga\u0001ry</E0>", true, 1, 3)]
        [InlineData("\u0001</E0>", true, 1, 1)]
        [InlineData("Ga\n\u0001", false, 2, 1)]
        public void ReadCharacterData_ForS3ControlByte_ItShould_ThrowCommittingNothing(
            string text,
            bool isReadingCompleted,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = CharacterDataReader(text, isReadingCompleted);

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertCharacterDataException(thrown, nameof(Throwing.Category.Expected), expectedLineNumber, expectedLinePosition);
            AssertCharacterDataNothingCommitted(ref reader);
        }

        /// <summary>
        /// S4: the text runs to the end of the segment and reading is not completed, so the
        /// delegate halts (simulated rollback)
        /// </summary>
        [Theory]
        [InlineData("Gary")]
        [InlineData("Ga\nry")]
        [InlineData("\"")]
        public void ReadCharacterData_ForS4SegmentEnds_ItShould_HaltCommittingNothing(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = CharacterDataReader(text, isReadingCompleted: false);

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.False(advancement);
            AssertCharacterDataNothingCommitted(ref reader);
        }

        /// <summary>
        /// S5: the text runs to the end of the data and reading is completed, so Incomplete is
        /// thrown at the end of the data; nothing committed
        /// </summary>
        [Theory]
        [InlineData("Gary", 1, 5)]
        [InlineData("Ga\nry", 2, 3)]
        public void ReadCharacterData_ForS5DataEnds_ItShould_ThrowIncompleteCommittingNothing(
            string text,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = CharacterDataReader(text, isReadingCompleted: true);

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertCharacterDataException(thrown, nameof(Throwing.Category.Incomplete), expectedLineNumber, expectedLinePosition);
            AssertCharacterDataNothingCommitted(ref reader);
        }
        #endregion

        #region M0-M4 (multiple segments, delegate)
        /// <summary>
        /// M0, M1: text straddling segments is in <c>ValueSequence</c>; text ending at a segment's
        /// end is a plain <c>Value</c>
        /// </summary>
        [Theory]
        [InlineData(new[] { "Ga", "ry</E0>" }, true)]          // M0
        [InlineData(new[] { "Ga", "", "ry</E0>" }, true)]      // M0 an empty segment between
        [InlineData(new[] { "Gary", "</E0>" }, false)]         // M1
        public void ReadCharacterData_ForM0M1Straddling_ItShould_SetTheValue(
            string[] parts,
            bool expectedHasValueSequence
        ) {
            // Arrange
            Utf8XmlReader reader = CharacterDataReader(parts, isReadingCompleted: true);

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.Equal(expectedHasValueSequence, reader.HasValueSequence);
            AssertCharacterDataRead(ref reader, "Gary", 4, 0, 4);
        }

        /// <summary>
        /// M2: the text runs to the end of the last segment and reading is not completed, so the
        /// delegate halts with the sequence position unchanged
        /// </summary>
        [Theory]
        [InlineData([new[] { "Ga", "ry" }])]
        [InlineData([new[] { "Ga", "" }])]
        public void ReadCharacterData_ForM2SegmentEnds_ItShould_HaltCommittingNothing(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = CharacterDataReader(parts, isReadingCompleted: false);
            SequencePosition sequencePosition = reader.SequencePosition;

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.False(advancement);
            Assert.Equal(sequencePosition, reader.SequencePosition);
            AssertCharacterDataNothingCommitted(ref reader);
        }

        /// <summary>
        /// M3: the text before an <c>&amp;</c> in a later segment is the Value; the next read throws
        /// at the <c>&amp;</c>
        /// </summary>
        [Fact]
        public void ReadCharacterData_ForM3AmpersandStraddling_ItShould_SetTheValueThenThrow()
        {
            // Arrange
            Utf8XmlReader reader = CharacterDataReader(["Ga", "ry&amp;</E0>"], isReadingCompleted: true);

            // Act
            bool advancement = reader.ReadValue();
            bool hasValueSequence = reader.HasValueSequence;
            string value = ValueText(ref reader);
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.True(advancement);
            Assert.True(hasValueSequence);
            Assert.Equal("Gary", value);
            AssertCharacterDataException(thrown, nameof(Throwing.Category.Expected), 1, 5);
        }

        /// <summary>
        /// M4: a control byte in a later segment throws at the same offset as in one segment
        /// </summary>
        [Fact]
        public void ReadCharacterData_ForM4ControlByteStraddling_ItShould_ThrowCommittingNothing()
        {
            // Arrange
            Utf8XmlReader reader = CharacterDataReader(["Ga", "\u0001</E0>"], isReadingCompleted: true);

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertCharacterDataException(thrown, nameof(Throwing.Category.Expected), 1, 3);
            AssertCharacterDataNothingCommitted(ref reader);
        }
        #endregion

        #region M5-M7 (multiple segments, guard)
        /// <summary>
        /// M5: spacing-only text straddling segments is skipped, the next token is the child
        /// </summary>
        [Theory]
        [InlineData([new[] { ">\n ", " <b/>" }])]
        [InlineData([new[] { ">", "\n", "<b/>" }])]
        [InlineData([new[] { "> ", "", "<b/>" }])]
        public void SkipSpacingOnlyCharacterData_ForM5Straddling_ItShould_SkipToTheChild(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = AfterStartTagNameReader(parts, isReadingCompleted: true);

            // Act
            bool advancement = reader.Read();

            // Assert
            Assert.True(advancement);
            Assert.Equal(TokenType.Element, reader.TokenType);
            Assert.Equal("b", ValueText(ref reader));
        }

        /// <summary>
        /// M6: spacing straddling segments followed by text is not skipped, it starts the Value
        /// </summary>
        [Fact]
        public void SkipSpacingOnlyCharacterData_ForM6SpacingThenText_ItShould_SkipNothing()
        {
            // Arrange
            Utf8XmlReader reader = AfterStartTagNameReader([">\n ", "x</E0>"], isReadingCompleted: true);

            // Act
            bool advancement = reader.Read();

            // Assert
            Assert.True(advancement);
            Assert.Equal(TokenType.Value, reader.TokenType);
            Assert.True(reader.HasValueSequence);
            Assert.Equal("\n x", ValueText(ref reader));
        }

        /// <summary>
        /// M7: the spacing runs to the end of the last segment and reading is not completed, so
        /// the guard halts with only the <c>&gt;</c> committed
        /// </summary>
        [Theory]
        [InlineData([new[] { ">\n ", " " }])]
        [InlineData([new[] { ">", "\n" }])]
        public void SkipSpacingOnlyCharacterData_ForM7SegmentEnds_ItShould_HaltKeepingTheGreaterThan(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = AfterStartTagNameReader(parts, isReadingCompleted: false);

            // Act
            bool advancement = reader.Read();

            // Assert
            Assert.False(advancement);
            Assert.Equal(1, reader.Position);
            Assert.Equal(0, reader.ReaderState._lineNumber);
        }
        #endregion

        #region R0-R7 (read cycle)
        /// <summary>
        /// R0-R6: through Read(), indentation never produces a Value; text keeps its spacing
        /// </summary>
        [Theory]
        [InlineData("<a> </a>", new[] { "Element:a", "ElementEnd:a" })]                                                      // R0 (#47's R2)
        [InlineData("<a>\n</a>", new[] { "Element:a", "ElementEnd:a" })]                                                     // R0 (#47's R2)
        [InlineData("<a>\n   <b/>\n</a>", new[] { "Element:a", "Element:b", "ElementEnd:", "ElementEnd:a" })]                 // R1
        [InlineData("<a>\n   A<b/>\n</a>", new[] { "Element:a", "Value:\n   A", "Element:b", "ElementEnd:", "ElementEnd:a" })] // R2
        [InlineData("<a>Gary\n<b/>\n</a>", new[] { "Element:a", "Value:Gary\n", "Element:b", "ElementEnd:", "ElementEnd:a" })]   // R3
        [InlineData("<a b=\"1\">x</a>", new[] { "Element:a", "Attribute:b", "Value:1", "Value:x", "ElementEnd:a" })]           // R4
        [InlineData("<a b=\"1\">\n <c/></a>", new[] { "Element:a", "Attribute:b", "Value:1", "Element:c", "ElementEnd:", "ElementEnd:a" })] // R4
        [InlineData("<a>x<b/>y</a>", new[] { "Element:a", "Value:x", "Element:b", "ElementEnd:", "Value:y", "ElementEnd:a" })]  // R5
        [InlineData("<a>x>y</a>", new[] { "Element:a", "Value:x>y", "ElementEnd:a" })]                                       // R6
        [InlineData("<Stem>\n\t<Key>1</Key>\n</Stem>", new[] { "Element:Stem", "Element:Key", "Value:1", "ElementEnd:Key", "ElementEnd:Stem" })]
        public void Read_ForR0ToR6CharacterData_ItShould_IgnoreIndentation(
            string text,
            string[] expectedTokens
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));

            // Act
            List<string> tokens = ReadTokens(ref reader);

            // Assert
            Assert.Equal(expectedTokens, tokens);
        }

        /// <summary>
        /// R7: through Read(), a comment between texts splits them into two Values
        /// </summary>
        [Fact]
        public void Read_ForR7CommentBetweenText_ItShould_ReadTwoValues()
        {
            // Arrange
            Utf8XmlReader reader = new("<a>x<!--c-->y</a>"u8, isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));
            List<TokenType> tokenTypes = [];
            List<string> values = [];

            // Act
            while (reader.Read())
            {
                tokenTypes.Add(reader.TokenType);
                if (reader.TokenType == TokenType.Value)
                {
                    values.Add(ValueText(ref reader));
                }
            }

            // Assert
            Assert.Equal(new[] { TokenType.Element, TokenType.Value, TokenType.Comment, TokenType.Value, TokenType.ElementEnd }, tokenTypes);
            Assert.Equal(new[] { "x", "y" }, values);
        }
        #endregion
    }
}
