using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Read empty element end shells, from #35 -----------------------------------------
        //
        // The `ReadEmptyElementEnd_*` tests below are shells written from #35's architecture (a
        // read step after content ready reacting to the ending terminal `/>` of an empty element;
        // state table S0-S6), before the step exists - see doc/dev/Velocity.md's Test shell
        // phase.
        //
        // The step has three outcomes for the read cycle: continue to read value delegation,
        // return true (ElementEnd) or return false (undecided). Most shells call the step
        // directly, because through Read() a `continue` (S0, S1) reaches read value delegates
        // that aren't built yet (attribute, character data). The `Read_*` shells at the end cover
        // the step's wiring into Read().
        //
        // Test seam - PLACEHOLDER, not decided by the architecture, to be confirmed or renamed
        // during Understanding. Won't compile until it exists:
        // - the step as `internal bool? ReadEmptyElementEnd()`: null = continue to read value
        //   delegation, true = ElementEnd read, false = undecided (simulated rollback)
        //
        // Exceptions are only asserted by type (XmlException). The popped name is discarded, so
        // the value is asserted empty.

        #region helpers
        /// <summary>
        /// Read empty element end catching the exception, a ref struct can't be captured by
        /// Assert.Throws
        /// </summary>
        private static Exception? ReadEmptyElementEndThrown(ref Utf8XmlReader reader)
        {
            try
            {
                reader.TryReadEmptyElementEndingTerminal();
            }
            catch (Exception exception)
            {
                return exception;
            }

            return null;
        }

        /// <summary>
        /// Inside a start tag nested one level below the root element
        /// </summary>
        private static ReaderState NestedStartTagState(TokenType current, long linePosition = 0) => new(
            lineNumber: 0,
            linePosition: linePosition,
            currentTokenType: current,
            previousTokenType: TokenType.None,
            elementStack: ElementStackAtDepth(2, contentReady: false),
            readerOptions: default,
            documentType: false
        );
        #endregion

        #region S0
        /// <summary>
        /// S0: not inside a start tag (content ready or the element stack is empty), so the step
        /// continues to read value delegation without advancing
        /// </summary>
        [Theory]
        [InlineData("/a", Context.Content, TokenType.Element)]
        [InlineData("/>", Context.Content, TokenType.Value)]
        [InlineData("/>", Context.Prolog, TokenType.None)]
        [InlineData("/>", Context.Miscellaneous, TokenType.ElementEnd)]
        public void ReadEmptyElementEnd_ForS0NotInStartTag_ItShould_Continue(
            string text,
            Context context,
            TokenType current
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(context, current));
            int depth = reader.Depth;

            // Act
            bool? outcome = reader.TryReadEmptyElementEndingTerminal();

            // Assert
            Assert.Null(outcome);
            Assert.Equal(0, reader.Position);
            Assert.Equal(current, reader.TokenType);
            Assert.Equal(depth, reader.Depth);
        }

        /// <summary>
        /// S0: content ready skipped <c>&gt;</c> as the last byte of the segment, so the step
        /// must check the state before reading a byte - otherwise it reads past the segment
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReadEmptyElementEnd_ForS0AfterContentReadyDrainsTheSegment_ItShould_ContinueWithoutReadingAByte(
            bool isReadingCompleted
        ) {
            // Arrange
            Utf8XmlReader reader = new(">"u8, isReadingCompleted, ContextState(Context.StartTag, TokenType.Element));
            reader.ContentReady();

            // Act
            bool? outcome = reader.TryReadEmptyElementEndingTerminal();

            // Assert
            Assert.Null(outcome);
            Assert.Equal(1, reader.Position);
        }
        #endregion

        #region S1
        /// <summary>
        /// S1: inside a start tag the current byte is not <c>/</c>, so the step continues; a
        /// <c>/&gt;</c> after an attribute is read in a later read cycle
        /// </summary>
        [Theory]
        [InlineData("b=\"1\"/>", TokenType.Element)]
        [InlineData("c='d'/>", TokenType.Value)]
        [InlineData("\"1\"/>", TokenType.Attribute)]
        public void ReadEmptyElementEnd_ForS1NotSlash_ItShould_Continue(
            string text,
            TokenType current
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(Context.StartTag, current));

            // Act
            bool? outcome = reader.TryReadEmptyElementEndingTerminal();

            // Assert
            Assert.Null(outcome);
            Assert.Equal(0, reader.Position);
            Assert.Equal(current, reader.TokenType);
            Assert.Equal(1, reader.Depth);
        }
        #endregion

        #region S2
        /// <summary>
        /// S2: <c>/</c> is the last available byte and reading is not completed, so nothing is
        /// advanced and the read cycle returns false
        /// </summary>
        [Fact]
        public void ReadEmptyElementEnd_ForS2SlashLastWhenReadingNotCompleted_ItShould_ReturnFalseWithoutAdvancement()
        {
            // Arrange
            Utf8XmlReader reader = new("/"u8, isReadingCompleted: false, ContextState(Context.StartTag, TokenType.Element));

            // Act
            bool? outcome = reader.TryReadEmptyElementEndingTerminal();

            // Assert
            Assert.False(outcome);
            Assert.Equal(0, reader.Position);
            Assert.Equal(0, reader.ReaderState._linePosition);
            Assert.Equal(TokenType.Element, reader.TokenType);
            Assert.Equal(1, reader.Depth);
        }

        /// <summary>
        /// S2 straddling segments: only empty segments follow the <c>/</c>
        /// </summary>
        [Theory]
        [InlineData([new[] { "/", "" }])]
        [InlineData([new[] { "", "/" }])]
        public void ReadEmptyElementEnd_ForS2SlashLastWhenReadingNotCompletedStraddlingSegments_ItShould_ReturnFalseWithoutAdvancement(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: false, ContextState(Context.StartTag, TokenType.Element));
            SequencePosition sequencePosition = reader.SequencePosition;

            // Act
            bool? outcome = reader.TryReadEmptyElementEndingTerminal();

            // Assert
            Assert.False(outcome);
            Assert.Equal(sequencePosition, reader.SequencePosition);
            Assert.Equal(1, reader.Depth);
        }
        #endregion

        #region S3
        /// <summary>
        /// S3: <c>/</c> is the last available byte and reading is completed, so the ending
        /// terminal is missing (<c>&lt;a/</c> at the end of the document)
        /// </summary>
        [Fact]
        public void ReadEmptyElementEnd_ForS3SlashLastWhenReadingCompleted_ItShould_Throw()
        {
            // Arrange
            Utf8XmlReader reader = new("/"u8, isReadingCompleted: true, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadEmptyElementEndThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }

        /// <summary>
        /// S3 straddling segments
        /// </summary>
        [Fact]
        public void ReadEmptyElementEnd_ForS3SlashLastWhenReadingCompletedStraddlingSegments_ItShould_Throw()
        {
            // Arrange
            Utf8XmlReader reader = new(Sequence("/", ""), isReadingCompleted: true, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadEmptyElementEndThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion

        #region S4
        /// <summary>
        /// S4: <c>/</c> followed by anything other than <c>&gt;</c> is malformed, including
        /// spacing - the grammar doesn't allow spacing inside <c>/&gt;</c>
        /// </summary>
        [Theory]
        [InlineData("/a", true)]
        [InlineData("/ >", true)]
        [InlineData("//", false)]
        [InlineData("/\n>", false)]
        public void ReadEmptyElementEnd_ForS4SlashNotFollowedByGreaterThan_ItShould_Throw(
            string text,
            bool isReadingCompleted
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadEmptyElementEndThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }

        /// <summary>
        /// S4 straddling segments
        /// </summary>
        [Theory]
        [InlineData([new[] { "/", "a>" }])]
        [InlineData([new[] { "/", "", " >" }])]
        public void ReadEmptyElementEnd_ForS4SlashNotFollowedByGreaterThanStraddlingSegments_ItShould_Throw(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: false, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadEmptyElementEndThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion

        #region S5
        /// <summary>
        /// S5: <c>/&gt;</c> pops the element stack (name discarded), sets the token types and
        /// advances by 2, leaving the parent content ready
        /// </summary>
        [Theory]
        [InlineData("/>", TokenType.Element)]
        [InlineData("/><c/>", TokenType.Value)]
        [InlineData("/>text", TokenType.Element)]
        public void ReadEmptyElementEnd_ForS5EmptyElementEnd_ItShould_PopAndSetElementEnd(
            string text,
            TokenType current
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, NestedStartTagState(current));

            // Act
            bool? outcome = reader.TryReadEmptyElementEndingTerminal();

            // Assert
            Assert.True(outcome);
            Assert.Equal(TokenType.ElementEnd, reader.TokenType);
            Assert.Equal(current, reader.ReaderState._previousTokenType);
            Assert.True(reader.Value.IsEmpty);
            Assert.False(reader.HasValueSequence);
            Assert.Equal(1, reader.Depth);
            Assert.True(reader.ReaderState._elementStack.ContentReady);
            Assert.Equal(2, reader.Position);
            Assert.Equal(2, reader.ReaderState._linePosition);
        }

        /// <summary>
        /// S5: the line position advances from where <c>/&gt;</c> starts
        /// </summary>
        [Fact]
        public void ReadEmptyElementEnd_ForS5AfterLinePosition_ItShould_AdvanceTheLinePosition()
        {
            // Arrange - e.g. "<a><b c='d'" already read up to the ending terminal
            Utf8XmlReader reader = new("/>"u8, isReadingCompleted: true, NestedStartTagState(TokenType.Value, linePosition: 11));

            // Act
            bool? outcome = reader.TryReadEmptyElementEndingTerminal();

            // Assert
            Assert.True(outcome);
            Assert.Equal(13, reader.ReaderState._linePosition);
        }

        /// <summary>
        /// S5 straddling segments: <c>/&gt;</c> split across segments, including an empty one
        /// </summary>
        [Theory]
        [InlineData([new[] { "/", ">" }])]
        [InlineData([new[] { "/", "", ">" }])]
        [InlineData([new[] { "/", "><c/>" }])]
        public void ReadEmptyElementEnd_ForS5EmptyElementEndStraddlingSegments_ItShould_PopAndSetElementEnd(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, NestedStartTagState(TokenType.Element));

            // Act
            bool? outcome = reader.TryReadEmptyElementEndingTerminal();

            // Assert
            Assert.True(outcome);
            Assert.Equal(TokenType.ElementEnd, reader.TokenType);
            Assert.Equal(1, reader.Depth);
            Assert.Equal(2, reader.Position);
            Assert.Equal(2, reader.ReaderState._linePosition);
        }
        #endregion

        #region S6
        /// <summary>
        /// S6: popping the root element empties the element stack, so the reader is in the
        /// miscellaneous phase
        /// </summary>
        [Fact]
        public void ReadEmptyElementEnd_ForS6RootElementEnd_ItShould_EnterTheMiscellaneousPhase()
        {
            // Arrange
            Utf8XmlReader reader = new("/>"u8, isReadingCompleted: true, ContextState(Context.StartTag, TokenType.Element));

            // Act
            bool? outcome = reader.TryReadEmptyElementEndingTerminal();

            // Assert
            Assert.True(outcome);
            Assert.Equal(TokenType.ElementEnd, reader.TokenType);
            Assert.Equal(0, reader.Depth);
            Assert.True(reader.RootElement);
        }
        #endregion

        #region Read() wiring
        /// <summary>
        /// S2 through Read(): an undecided step ends the read cycle returning false, it does not
        /// continue to read value delegation (where <c>/</c> would be taken for an attribute)
        /// </summary>
        [Fact]
        public void Read_ForS2EmptyElementEndUndecided_ItShould_ReturnFalseWithoutReadValueDelegation()
        {
            // Arrange
            Utf8XmlReader reader = new("/"u8, isReadingCompleted: false, ContextState(Context.StartTag, TokenType.Element));

            // Act
            bool advancement = reader.Read();

            // Assert
            Assert.False(advancement);
            Assert.Equal(0, reader.Position);
            Assert.Equal(TokenType.Element, reader.TokenType);
        }

        /// <summary>
        /// S5 through Read(): spacing before <c>/&gt;</c> is skipped first, then the step
        /// returns the ElementEnd token
        /// </summary>
        [Theory]
        [InlineData("/>", 2)]
        [InlineData(" />", 3)]
        [InlineData("\n\t/>", 4)]
        public void Read_ForS5EmptyElementEnd_ItShould_ReturnElementEnd(
            string text,
            long expectedPosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, NestedStartTagState(TokenType.Element));

            // Act
            bool advancement = reader.Read();

            // Assert
            Assert.True(advancement);
            Assert.Equal(TokenType.ElementEnd, reader.TokenType);
            Assert.Equal(1, reader.Depth);
            Assert.Equal(expectedPosition, reader.Position);
        }

        /// <summary>
        /// S6 through Read(): a whole document of one empty root element reads as Element then
        /// ElementEnd, then completes without throwing
        /// </summary>
        [Theory]
        [InlineData("<root/>")]
        [InlineData("<root />")]
        [InlineData("<root/>\n")]
        public void Read_ForS6EmptyRootElement_ItShould_ReadTheWholeDocument(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));

            // Act
            bool element = reader.Read();
            TokenType first = reader.TokenType;
            bool elementEnd = reader.Read();
            TokenType second = reader.TokenType;
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            Assert.True(element);
            Assert.Equal(TokenType.Element, first);
            Assert.True(elementEnd);
            Assert.Equal(TokenType.ElementEnd, second);
            Assert.Null(thrown);
            Assert.Equal(0, reader.Depth);
            Assert.True(reader.RootElement);
        }

        /// <summary>
        /// S5 through Read(): an empty child element inside the root element
        /// (<c>&lt;root&gt;&lt;a/&gt;</c> - the root's end tag isn't built yet)
        /// </summary>
        [Fact]
        public void Read_ForS5EmptyChildElement_ItShould_ReadElementThenElementEnd()
        {
            // Arrange
            Utf8XmlReader reader = new("<root><a/>"u8, isReadingCompleted: false, ContextState(Context.Prolog, TokenType.None));

            // Act
            reader.Read();
            bool child = reader.Read();
            string childName = ValueText(ref reader);
            bool childEnd = reader.Read();

            // Assert
            Assert.True(child);
            Assert.Equal("a", childName);
            Assert.True(childEnd);
            Assert.Equal(TokenType.ElementEnd, reader.TokenType);
            Assert.Equal(1, reader.Depth);
            Assert.True(reader.ReaderState._elementStack.ContentReady);
        }
        #endregion
    }
}
