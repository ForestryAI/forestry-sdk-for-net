using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Read end tag shells, from #47 ---------------------------------------------------
        //
        // The `ReadEndTag_*` tests below are shells written from #47's architecture (evaluate the
        // name 2 bytes past `</`, peek the ending terminal after only optional spacing, try peek
        // the name on the element stack - then, and only then, pop, advance, set the value and
        // the token types), before the delegate exists - see doc/dev/Velocity.md's Test shell
        // phase.
        //
        // The regions follow #47's Assertions: end tag (S0-S17), element stack (E0-E2), multiple
        // segments (M0-M10) and the read cycle (R0-R7). Examples start at `</` in content with
        // the current token Element.
        //
        // The delegate is reached through ReadValue() (#27's delegation, candidate S5), so no
        // shell names the delegate method and no test seam is needed. Until it is built the
        // EndTag arm throws NotImplementedException.
        //
        // The EndTag throwing markup doesn't exist yet (#47's warning), so the markup is compared
        // by name as a string and these shells compile today. ElementStack.TryPeek has no shells
        // here: it is only observed through the delegate (a mismatch commits nothing).

        #region helpers
        /// <summary>
        /// Reader state in content with the named elements open (outermost first), the current
        /// token Element and content ready, as after content ready skipped the last start tag's
        /// <c>&gt;</c>
        /// </summary>
        private static ReaderState EndTagState(params string[] openElements)
        {
            ElementStack stack = default;
            foreach (string name in openElements)
            {
                stack.Push(Encoding.UTF8.GetBytes(name));
            }

            // TryPop is the only way to set content ready (see ElementStackAtDepth)
            stack.Push("Child"u8);
            stack.TryPop("Child"u8);

            return State(stack, TokenType.Element);
        }

        /// <summary>
        /// A reader at <c>&lt;/</c> with the named elements open
        /// </summary>
        private static Utf8XmlReader EndTagReader(string text, bool isReadingCompleted, params string[] openElements) =>
            new(Encoding.UTF8.GetBytes(text), isReadingCompleted, EndTagState(openElements));

        /// <summary>
        /// A reader at <c>&lt;/</c> straddling segments with the named elements open
        /// </summary>
        private static Utf8XmlReader EndTagReader(string[] parts, bool isReadingCompleted, params string[] openElements) =>
            new(Sequence(parts), isReadingCompleted, EndTagState(openElements));

        /// <summary>
        /// Assert the reader after an end tag was read: ElementEnd with the full name as the
        /// value, previous token Element, popped one level with content ready, and the positions
        /// past <c>&gt;</c>
        /// </summary>
        private static void AssertEndTagRead(
            ref Utf8XmlReader reader,
            string expectedName,
            int depthBefore,
            long expectedPosition,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            Assert.Equal(TokenType.ElementEnd, reader.TokenType);
            Assert.Equal(TokenType.Element, reader.ReaderState._previousTokenType);
            Assert.Equal(expectedName, ValueText(ref reader));

            Assert.Equal(depthBefore - 1, reader.Depth);
            Assert.True(reader.ReaderState._elementStack.ContentReady);

            Assert.Equal(expectedPosition, reader.Position);
            Assert.Equal(expectedLineNumber, reader.ReaderState._lineNumber);
            Assert.Equal(expectedLinePosition, reader.ReaderState._linePosition);
        }

        /// <summary>
        /// Assert nothing was committed: position, line tracking, token types and depth unchanged
        /// </summary>
        private static void AssertEndTagNothingCommitted(ref Utf8XmlReader reader, int depthBefore)
        {
            Assert.Equal(0, reader.Position);
            Assert.Equal(0, reader.ReaderState._lineNumber);
            Assert.Equal(0, reader.ReaderState._linePosition);
            Assert.Equal(TokenType.Element, reader.TokenType);
            Assert.Equal(TokenType.None, reader.ReaderState._previousTokenType);
            Assert.Equal(depthBefore, reader.Depth);
        }

        /// <summary>
        /// The thrown exception is an XML exception with the category and markup (by name, the
        /// EndTag markup doesn't exist yet) at the 1-based position
        /// </summary>
        private static void AssertEndTagException(
            Exception? thrown,
            string category,
            string markup,
            long lineNumber,
            long linePosition
        ) {
            XmlException exception = Assert.IsType<XmlException>(thrown);
            Assert.Equal(category, exception.Category);
            Assert.Equal(markup, exception.Markup);
            Assert.Equal(lineNumber, exception.LineNumber);
            Assert.Equal(linePosition, exception.LinePosition);
        }

        /// <summary>
        /// Read() until it returns false, collecting each token as "TokenType:value"
        /// </summary>
        private static List<string> ReadTokens(ref Utf8XmlReader reader)
        {
            List<string> tokens = [];
            while (reader.Read())
            {
                tokens.Add($"{reader.TokenType}:{ValueText(ref reader)}");
            }

            return tokens;
        }

        private const string Name32 = "StemKeyStemKeyStemKeyStemKeyStem";   // 32 bytes
        private const string Name40 = "StemKeyStemKeyStemKeyStemKeyStemKeyStemK";
        #endregion

        #region S0-S5 (read)
        /// <summary>
        /// S0-S3: the name is read in <c>Value</c>, optional spacing (line feeds tracked) and the
        /// ending terminal are consumed, the element is popped
        /// </summary>
        [Theory]
        [InlineData("</a>", "a", 4, 0, 4)]                         // S0
        [InlineData("</a>text", "a", 4, 0, 4)]                     // S0 nothing after '>' is read
        [InlineData("</a >", "a", 5, 0, 5)]                        // S1
        [InlineData("</a\t\t>", "a", 6, 0, 6)]                     // S1
        [InlineData("</a\n>", "a", 5, 1, 1)]                       // S2
        [InlineData("</a\r\n>", "a", 6, 1, 1)]                     // S2
        [InlineData("</Stem-Key.1>", "Stem-Key.1", 13, 0, 13)]     // S3
        public void ReadEndTag_ForS0ToS3EndTag_ItShould_PopAndSetElementEnd(
            string text,
            string expectedName,
            long expectedPosition,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = EndTagReader(text, isReadingCompleted: true, expectedName);

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.False(reader.HasValueSequence);
            AssertEndTagRead(ref reader, expectedName, depthBefore: 1, expectedPosition, expectedLineNumber, expectedLinePosition);
        }

        /// <summary>
        /// S4, S5: a name over 32 bytes is compared on its first 32 bytes only (rule 16), so a
        /// difference after them is a known leniency, not a throw. The value is the full name
        /// from the document, not the stack's 32 bytes.
        /// </summary>
        [Theory]
        [InlineData(Name32 + "a", Name32 + "a")]     // S4 same 33-byte name
        [InlineData(Name32 + "b", Name32 + "c")]     // S5 first 32 bytes match, the rest differs
        [InlineData(Name40, Name40)]                 // S4
        public void ReadEndTag_ForS4S5LongName_ItShould_ReadTheFullName(
            string openName,
            string endName
        ) {
            // Arrange
            Utf8XmlReader reader = EndTagReader($"</{endName}>", isReadingCompleted: true, openName);

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            AssertEndTagRead(ref reader, endName, depthBefore: 1, endName.Length + 3, 0, endName.Length + 3);
        }
        #endregion

        #region S6-S8 (name throws)
        /// <summary>
        /// S6, S7: an empty name or a byte that can't start a name throws at the start of the
        /// name (offset 2); nothing committed
        /// </summary>
        [Theory]
        [InlineData("</>")]      // S6
        [InlineData("</1a>")]    // S7
        [InlineData("</ a>")]    // S7
        [InlineData("</-a>")]    // S7
        public void ReadEndTag_ForS6S7MalformedName_ItShould_ThrowCommittingNothing(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = EndTagReader(text, isReadingCompleted: true, "a");

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertEndTagException(thrown, nameof(Throwing.Category.Name), nameof(Throwing.Markup.Name), 1, 3);
            AssertEndTagNothingCommitted(ref reader, depthBefore: 1);
        }

        /// <summary>
        /// S8: a non-ASCII name byte throws unsupported (rule 14, the name flag) at that byte;
        /// nothing committed
        /// </summary>
        [Theory]
        [InlineData("</ä>", 3)]
        [InlineData("</aä>", 4)]
        public void ReadEndTag_ForS8UnsupportedNameByte_ItShould_ThrowCommittingNothing(
            string text,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = EndTagReader(text, isReadingCompleted: true, "a");

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertXmlExceptionAt(thrown, 1, expectedLinePosition);
            AssertEndTagNothingCommitted(ref reader, depthBefore: 1);
        }
        #endregion

        #region S9, S13 (halt)
        /// <summary>
        /// S9, S13: the segment ends inside the name or with only spacing after it, and reading
        /// is not completed, so the delegate halts (simulated rollback) - line feeds in the
        /// spacing aren't tracked either
        /// </summary>
        [Theory]
        [InlineData("</")]       // S9 the name hasn't started
        [InlineData("</a")]      // S9
        [InlineData("</Stem")]   // S9
        [InlineData("</a ")]     // S13
        [InlineData("</a\n")]    // S13
        public void ReadEndTag_ForS9S13SegmentEnds_ItShould_HaltCommittingNothing(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = EndTagReader(text, isReadingCompleted: false, "a");

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.False(advancement);
            AssertEndTagNothingCommitted(ref reader, depthBefore: 1);
        }
        #endregion

        #region S10, S14 (incomplete)
        /// <summary>
        /// S10, S14: the data ends inside the name or with only spacing after it, and reading is
        /// completed, so Incomplete is thrown at the end of the data; nothing committed
        /// </summary>
        [Theory]
        [InlineData("</a", 1, 4)]       // S10
        [InlineData("</", 1, 3)]        // S10
        [InlineData("</a ", 1, 5)]      // S14
        [InlineData("</a\n", 2, 1)]     // S14 the end of the data is after the line feed
        public void ReadEndTag_ForS10S14DataEnds_ItShould_ThrowIncompleteCommittingNothing(
            string text,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = EndTagReader(text, isReadingCompleted: true, "a");

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertEndTagException(thrown, nameof(Throwing.Category.Incomplete), "EndTag", expectedLineNumber, expectedLinePosition);
            AssertEndTagNothingCommitted(ref reader, depthBefore: 1);
        }
        #endregion

        #region S11, S12, S16, S17 (wrong byte)
        /// <summary>
        /// S11, S12: a byte after the name, or after the spacing, that isn't the ending terminal
        /// throws at that byte; nothing committed
        /// </summary>
        [Theory]
        [InlineData("</a b>", 5)]      // S11
        [InlineData("</a/>", 4)]       // S11
        [InlineData("</a=>", 4)]       // S11
        [InlineData("</a  b>", 6)]     // S12
        public void ReadEndTag_ForS11S12WrongByte_ItShould_ThrowExpectedCommittingNothing(
            string text,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = EndTagReader(text, isReadingCompleted: true, "a");

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertEndTagException(thrown, nameof(Throwing.Category.Expected), nameof(Throwing.Markup.EndingTerminal), 1, expectedLinePosition);
            AssertEndTagNothingCommitted(ref reader, depthBefore: 1);
        }

        /// <summary>
        /// S16: the first deciding byte wins - malformed markup throws even when reading is not
        /// completed, a halt would only make the caller read again
        /// </summary>
        [Theory]
        [InlineData("</a b", nameof(Throwing.Category.Expected), nameof(Throwing.Markup.EndingTerminal), 5)]
        [InlineData("</1", nameof(Throwing.Category.Name), nameof(Throwing.Markup.Name), 3)]
        public void ReadEndTag_ForS16FirstDecidingByte_ItShould_ThrowWhenReadingNotCompleted(
            string text,
            string category,
            string markup,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = EndTagReader(text, isReadingCompleted: false, "a");

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertEndTagException(thrown, category, markup, 1, expectedLinePosition);
            AssertEndTagNothingCommitted(ref reader, depthBefore: 1);
        }

        /// <summary>
        /// S17: the end tag is evaluated before the element stack is checked, so a wrong byte
        /// throws Expected even though the name doesn't match either
        /// </summary>
        [Fact]
        public void ReadEndTag_ForS17WrongByteAndMismatch_ItShould_ThrowExpectedFirst()
        {
            // Arrange
            Utf8XmlReader reader = EndTagReader("</b c>", isReadingCompleted: true, "a");

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertEndTagException(thrown, nameof(Throwing.Category.Expected), nameof(Throwing.Markup.EndingTerminal), 1, 5);
            AssertEndTagNothingCommitted(ref reader, depthBefore: 1);
        }
        #endregion

        #region S15, E0-E2 (element stack)
        /// <summary>
        /// S15, E1: the name doesn't match the top of the element stack, so Name is thrown at the
        /// start of the name (offset 2); nothing committed, the element is still open
        /// </summary>
        [Theory]
        [InlineData("</b>", new[] { "a" })]          // S15
        [InlineData("</a>", new[] { "ab" })]         // S15 a prefix of the open name
        [InlineData("</ab>", new[] { "a" })]         // S15 the open name is a prefix
        [InlineData("</a>", new[] { "a", "b" })]     // E1 only the top is compared
        public void ReadEndTag_ForS15E1NameMismatch_ItShould_ThrowCommittingNothing(
            string text,
            string[] openElements
        ) {
            // Arrange
            Utf8XmlReader reader = EndTagReader(text, isReadingCompleted: true, openElements);

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertEndTagException(thrown, nameof(Throwing.Category.Name), "EndTag", 1, 3);
            AssertEndTagNothingCommitted(ref reader, depthBefore: openElements.Length);
            Assert.True(reader.ReaderState._elementStack.TryPop(Encoding.UTF8.GetBytes(openElements[^1])));
        }

        /// <summary>
        /// E0: a nested end tag pops one level, the parent is the top afterwards
        /// </summary>
        [Fact]
        public void ReadEndTag_ForE0NestedEndTag_ItShould_PopOneLevel()
        {
            // Arrange
            Utf8XmlReader reader = EndTagReader("</b>", isReadingCompleted: true, "a", "b");

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            AssertEndTagRead(ref reader, "b", depthBefore: 2, 4, 0, 4);
            Assert.True(reader.ReaderState._elementStack.TryPop("a"u8));
        }

        /// <summary>
        /// E2: the root's end tag closes the root, what follows is miscellaneous
        /// </summary>
        [Fact]
        public void ReadEndTag_ForE2RootEndTag_ItShould_CloseTheRoot()
        {
            // Arrange
            Utf8XmlReader reader = EndTagReader("</a>", isReadingCompleted: true, "a");

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            AssertEndTagRead(ref reader, "a", depthBefore: 1, 4, 0, 4);
            Assert.Equal(0, reader.Depth);
            Assert.True(reader.ReaderState._elementStack.RootElement);
        }
        #endregion

        #region M0-M6 (multiple segments, read)
        /// <summary>
        /// M0-M5: the starting terminal, the name, the spacing or the ending terminal straddle
        /// segments; line feeds are tracked across segments
        /// </summary>
        [Theory]
        [InlineData(new[] { "<", "/a>" }, "a", 4, 0, 4)]                 // M0
        [InlineData(new[] { "</", "a>" }, "a", 4, 0, 4)]                 // M1
        [InlineData(new[] { "</a", ">" }, "a", 4, 0, 4)]                 // M1
        [InlineData(new[] { "</St", "em>" }, "Stem", 7, 0, 7)]           // M2
        [InlineData(new[] { "</St", "", "em>" }, "Stem", 7, 0, 7)]       // M3
        [InlineData(new[] { "</a ", ">" }, "a", 5, 0, 5)]                // M4
        [InlineData(new[] { "</a", " ", ">" }, "a", 5, 0, 5)]            // M4
        [InlineData(new[] { "</a", "\n", ">" }, "a", 5, 1, 1)]           // M5
        public void ReadEndTag_ForM0ToM5Straddling_ItShould_PopAndSetElementEnd(
            string[] parts,
            string expectedName,
            long expectedPosition,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = EndTagReader(parts, isReadingCompleted: true, expectedName);

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            AssertEndTagRead(ref reader, expectedName, depthBefore: 1, expectedPosition, expectedLineNumber, expectedLinePosition);
        }

        /// <summary>
        /// M2: a name straddling segments is in <c>ValueSequence</c> (copied to a contiguous
        /// span for try peeking)
        /// </summary>
        [Fact]
        public void ReadEndTag_ForM2NameStraddling_ItShould_ReadTheValueSequence()
        {
            // Arrange
            Utf8XmlReader reader = EndTagReader(["</St", "em>"], isReadingCompleted: true, "Stem");

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.True(reader.HasValueSequence);
            Assert.Equal("Stem", ValueText(ref reader));
        }

        /// <summary>
        /// M6: a name over 32 bytes straddling segments is copied only up to the element stack's
        /// packed name length for try peeking - copying it all would overflow the span. The value
        /// is still the full name.
        /// </summary>
        [Theory]
        [InlineData([new[] { "</StemKeyStemKeyStemKey", "StemKeyStemKeyStemK>" }])]    // 21 | 19 name bytes
        [InlineData([new[] { "</StemKeyStemKeyStemKeyStemKeyStemKey", "StemK>" }])]    // 35 | 5 name bytes
        public void ReadEndTag_ForM6LongNameStraddling_ItShould_ReadTheFullName(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = EndTagReader(parts, isReadingCompleted: true, Name40);

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.True(reader.HasValueSequence);
            AssertEndTagRead(ref reader, Name40, depthBefore: 1, Name40.Length + 3, 0, Name40.Length + 3);
        }
        #endregion

        #region M7-M10 (multiple segments, halt and throw)
        /// <summary>
        /// M7, M8: the name or the spacing runs to the end of the last segment and reading is
        /// not completed, so the delegate halts with the sequence position unchanged
        /// </summary>
        [Theory]
        [InlineData([new[] { "</St", "em" }])]      // M7
        [InlineData([new[] { "<", "/" }])]          // M7 the name hasn't started
        [InlineData([new[] { "</a ", "" }])]        // M8
        [InlineData([new[] { "</a", " ", "\n" }])]  // M8
        public void ReadEndTag_ForM7M8SegmentEnds_ItShould_HaltCommittingNothing(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = EndTagReader(parts, isReadingCompleted: false, "Stem");
            SequencePosition sequencePosition = reader.SequencePosition;

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.False(advancement);
            Assert.Equal(sequencePosition, reader.SequencePosition);
            AssertEndTagNothingCommitted(ref reader, depthBefore: 1);
        }

        /// <summary>
        /// M9, M10: a wrong byte or a name mismatch across segments throws at the same offset as
        /// in one segment; nothing committed
        /// </summary>
        [Theory]
        [InlineData(new[] { "</St", "em b>" }, "Stem", nameof(Throwing.Category.Expected), nameof(Throwing.Markup.EndingTerminal), 1, 8)]   // M9
        [InlineData(new[] { "</St", "em", "\n b>" }, "Stem", nameof(Throwing.Category.Expected), nameof(Throwing.Markup.EndingTerminal), 2, 2)]   // M9 after a line feed
        [InlineData(new[] { "</St", "ep>" }, "Stem", nameof(Throwing.Category.Name), "EndTag", 1, 3)]   // M10
        public void ReadEndTag_ForM9M10Straddling_ItShould_ThrowCommittingNothing(
            string[] parts,
            string openName,
            string category,
            string markup,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = EndTagReader(parts, isReadingCompleted: true, openName);

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertEndTagException(thrown, category, markup, expectedLineNumber, expectedLinePosition);
            AssertEndTagNothingCommitted(ref reader, depthBefore: 1);
        }
        #endregion

        #region R0-R7 (read cycle)
        /// <summary>
        /// R0-R4: through Read(), the first complete non-empty elements - a start tag's
        /// ElementEnd carries the name, an empty element's is empty (R3)
        /// </summary>
        [Theory]
        [InlineData("<a></a>", new[] { "Element:a", "ElementEnd:a" })]                                              // R0
        [InlineData("<a><b></b></a>", new[] { "Element:a", "Element:b", "ElementEnd:b", "ElementEnd:a" })]           // R1
        [InlineData("<a> </a>", new[] { "Element:a", "ElementEnd:a" })]                                             // R2
        [InlineData("<a>\n</a>", new[] { "Element:a", "ElementEnd:a" })]                                            // R2
        [InlineData("<a><b/></a>", new[] { "Element:a", "Element:b", "ElementEnd:", "ElementEnd:a" })]              // R3
        [InlineData("<a b=\"1\"></a>", new[] { "Element:a", "Attribute:b", "Value:1", "ElementEnd:a" })]            // R4
        [InlineData("<Stem>\n\t<Key></Key >\n</Stem>", new[] { "Element:Stem", "Element:Key", "ElementEnd:Key", "ElementEnd:Stem" })]
        public void Read_ForR0ToR4EndTags_ItShould_ReadTheElements(
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
        /// R5: through Read(), the end tag straddles two piped readers (<c>&lt;a&gt;&lt;/a|&gt;</c>):
        /// the first halts, the caller re-supplies the bytes from the reader's position and the
        /// second reads ElementEnd
        /// </summary>
        [Fact]
        public void Read_ForR5EndTagAcrossReaders_ItShould_HaltThenReadElementEnd()
        {
            // Arrange
            Utf8XmlReader first = new("<a></a"u8, isReadingCompleted: false, ContextState(Context.Prolog, TokenType.None));
            bool element = first.Read();
            bool halted = first.Read();
            long position = first.Position;
            ReaderState state = first.ReaderState;

            Utf8XmlReader second = new(Encoding.UTF8.GetBytes("<a></a>"[(int)position..]), isReadingCompleted: true, state);

            // Act
            bool elementEnd = second.Read();

            // Assert
            Assert.True(element);
            Assert.False(halted);
            Assert.True(elementEnd);
            Assert.Equal(TokenType.ElementEnd, second.TokenType);
            Assert.Equal("a", ValueText(ref second));
            Assert.Equal(0, second.Depth);
        }

        /// <summary>
        /// R6: through Read(), a second root after the root's end tag throws (the existing
        /// candidate None path)
        /// </summary>
        [Fact]
        public void Read_ForR6SecondRoot_ItShould_Throw()
        {
            // Arrange
            Utf8XmlReader reader = new("<a></a><b/>"u8, isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));
            Assert.True(reader.Read());
            Assert.True(reader.Read());

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }

        /// <summary>
        /// R7: through Read(), the document ends after the root's end tag - Read() returns false
        /// without throwing
        /// </summary>
        [Fact]
        public void Read_ForR7DocumentComplete_ItShould_ReturnFalseWithoutThrowing()
        {
            // Arrange
            Utf8XmlReader reader = new("<a></a>"u8, isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));
            Assert.True(reader.Read());
            Assert.True(reader.Read());

            // Act - ref struct, so try/catch rather than Assert.Throws
            Exception? thrown = null;
            bool advancement = false;
            try
            {
                advancement = reader.Read();
            }
            catch (Exception exception)
            {
                thrown = exception;
            }

            // Assert
            Assert.Null(thrown);
            Assert.False(advancement);
        }
        #endregion
    }
}
