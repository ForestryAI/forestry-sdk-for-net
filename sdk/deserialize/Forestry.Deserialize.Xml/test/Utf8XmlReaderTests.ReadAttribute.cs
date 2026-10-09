using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Read attribute shells, from #40 -------------------------------------------------
        //
        // The `ReadAttribute_*` tests below are shells written from #40's architecture (a flag
        // recording spacing skipped in the start tag, cleared by the attribute delegate and by
        // closing the start tag; content ready S4 and the empty element step throwing on a
        // dangling attribute name; the attribute delegate reusing #38's read name), before the
        // code exists - see doc/dev/Velocity.md's Test shell phase.
        //
        // #40's table has four parts and so do the regions here: skip spacing (S1.a, S1.b),
        // content ready (S3, S5 clearing the flag; S4 is in Utf8XmlReaderTests.ContentReady.cs),
        // the empty element step (S5.a, S5.b) and the delegate (S0-S5). The #32 row has no
        // shells: #32 isn't built.
        //
        // The delegate is reached through ReadValue() (#27's delegation), so no shell names the
        // delegate method. Until #32 is built the `=` after an attribute name reaches the
        // attribute candidate, so no shell reads past a name.
        //
        // Test seam - PLACEHOLDER, not decided by the architecture, to be confirmed or renamed
        // during Understanding. Won't compile until it exists:
        // - ReaderState field `_hasSkippedSpacingInStartTag` and constructor parameter
        //   `hasSkippedSpacingInStartTag`, carried by the reader like `_documentType`
        //
        // Exceptions are only asserted by type (XmlException).

        #region helpers
        /// <summary>
        /// Inside a start tag (depth 1, content not ready) with the spacing flag
        /// </summary>
        private static ReaderState StartTagState(
            TokenType current,
            bool hasSkippedSpacingInStartTag,
            TokenType previous = TokenType.None,
            int depth = 1,
            long linePosition = 0
        ) => new(
            lineNumber: 0,
            linePosition: linePosition,
            documentType: false,
            hasSkippedSpacingInStartTag: hasSkippedSpacingInStartTag,
            currentTokenType: current,
            previousTokenType: previous,
            elementStack: ElementStackAtDepth(depth, contentReady: false),
            readerOptions: default
        );

        /// <summary>
        /// Content ready catching the exception, a ref struct can't be captured by Assert.Throws
        /// </summary>
        private static Exception? ContentReadyThrown(ref Utf8XmlReader reader)
        {
            try
            {
                reader.ContentReady();
            }
            catch (Exception exception)
            {
                return exception;
            }

            return null;
        }

        /// <summary>
        /// Assert the reader after an attribute name was read (S3-S5): the token types, the
        /// value, the flag cleared, the element stack unchanged and the positions the name's
        /// length further
        /// </summary>
        private static void AssertAttributeRead(
            ref Utf8XmlReader reader,
            string expectedName,
            TokenType previous,
            long linePositionBefore = 0
        ) {
            Assert.Equal(TokenType.Attribute, reader.TokenType);
            Assert.Equal(previous, reader.ReaderState._previousTokenType);
            Assert.Equal(expectedName, ValueText(ref reader));
            Assert.False(reader.ReaderState._hasSkippedSpacingInStartTag);

            Assert.Equal(1, reader.Depth);
            Assert.False(reader.ReaderState._elementStack.ContentReady);

            Assert.Equal(expectedName.Length, reader.Position);
            Assert.Equal(linePositionBefore + expectedName.Length, reader.ReaderState._linePosition);
        }
        #endregion

        #region Skip spacing S1.a, S1.b
        /// <summary>
        /// Skip spacing S1.a: spacing skipped in the start tag sets the flag
        /// </summary>
        [Theory]
        [InlineData(" b", TokenType.Element, TokenType.None)]
        [InlineData("\n\t b", TokenType.Element, TokenType.None)]
        [InlineData(" c", TokenType.Value, TokenType.Attribute)]
        public void ReadAttribute_ForSkipSpacingS1aInStartTag_ItShould_SetTheFlag(
            string text,
            TokenType current,
            TokenType previous
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, StartTagState(current, hasSkippedSpacingInStartTag: false, previous));

            // Act
            bool halted = reader.SkipSpacing();

            // Assert
            Assert.False(halted);
            Assert.True(reader.ReaderState._hasSkippedSpacingInStartTag);
        }

        /// <summary>
        /// Skip spacing S1.a: the flag is set before returning, also when the skip drained the
        /// segment(s) and halted the read (<c>&lt;a |b</c>)
        /// </summary>
        [Theory]
        [InlineData(" ")]
        [InlineData("\n  ")]
        public void ReadAttribute_ForSkipSpacingS1aDrained_ItShould_SetTheFlagReturningTrue(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: false, StartTagState(TokenType.Element, hasSkippedSpacingInStartTag: false));

            // Act
            bool halted = reader.SkipSpacing();

            // Assert
            Assert.True(halted);
            Assert.True(reader.ReaderState._hasSkippedSpacingInStartTag);
        }

        /// <summary>
        /// Skip spacing S1.a straddling segments
        /// </summary>
        [Theory]
        [InlineData([new[] { " ", " b" }])]
        [InlineData([new[] { " ", "", "b" }])]
        public void ReadAttribute_ForSkipSpacingS1aStraddlingSegments_ItShould_SetTheFlag(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, StartTagState(TokenType.Element, hasSkippedSpacingInStartTag: false));

            // Act
            reader.SkipSpacing();

            // Assert
            Assert.True(reader.ReaderState._hasSkippedSpacingInStartTag);
        }

        /// <summary>
        /// Skip spacing S1.b: spacing skipped in the prolog or miscellaneous leaves the flag
        /// unchanged
        /// </summary>
        [Theory]
        [InlineData("\n<a", Context.Prolog, TokenType.Declaration)]
        [InlineData("  <a", Context.Prolog, TokenType.None)]
        [InlineData("\n<!-- c -->", Context.Miscellaneous, TokenType.ElementEnd)]
        public void ReadAttribute_ForSkipSpacingS1bNotInStartTag_ItShould_LeaveTheFlagUnchanged(
            string text,
            Context context,
            TokenType current
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(context, current));

            // Act
            reader.SkipSpacing();

            // Assert
            Assert.False(reader.ReaderState._hasSkippedSpacingInStartTag);
        }
        #endregion

        #region Content ready S3, S5
        /// <summary>
        /// Content ready S3 and S5: closing the start tag with <c>&gt;</c> clears the flag
        /// (<c>&lt;a &gt;</c>, <c>&lt;a b="1" &gt;</c>)
        /// </summary>
        [Theory]
        [InlineData(TokenType.Element, TokenType.None)]
        [InlineData(TokenType.Value, TokenType.Attribute)]
        public void ReadAttribute_ForContentReadyClosingTheStartTag_ItShould_ClearTheFlag(
            TokenType current,
            TokenType previous
        ) {
            // Arrange
            Utf8XmlReader reader = new(">"u8, isReadingCompleted: true, StartTagState(current, hasSkippedSpacingInStartTag: true, previous));

            // Act
            reader.ContentReady();

            // Assert
            Assert.Equal(1, reader.Position);
            Assert.True(reader.ReaderState._elementStack.ContentReady);
            Assert.False(reader.ReaderState._hasSkippedSpacingInStartTag);
        }
        #endregion

        #region Empty element S5.a, S5.b
        /// <summary>
        /// Empty element S5.a: <c>/&gt;</c> after an attribute name is a dangling attribute,
        /// the value is missing (<c>&lt;a b/&gt;</c>)
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ReadAttribute_ForEmptyElementS5aAfterAttributeName_ItShould_Throw(
            bool hasSkippedSpacingInStartTag
        ) {
            // Arrange
            Utf8XmlReader reader = new("/>"u8, isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag));

            // Act
            Exception? thrown = ReadEmptyElementEndThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }

        /// <summary>
        /// Empty element S5.a straddling segments
        /// </summary>
        [Theory]
        [InlineData([new[] { "/", ">" }])]
        [InlineData([new[] { "/", "", ">" }])]
        public void ReadAttribute_ForEmptyElementS5aAfterAttributeNameStraddlingSegments_ItShould_Throw(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false));

            // Act
            Exception? thrown = ReadEmptyElementEndThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }

        /// <summary>
        /// Empty element S5.a is asserted after <c>/&gt;</c> has been peeked, so a <c>/</c> as
        /// the last byte after an attribute name is still #35's S2 (undecided, false)
        /// </summary>
        [Fact]
        public void ReadAttribute_ForEmptyElementSlashLastAfterAttributeName_ItShould_ReturnFalseWithoutAdvancement()
        {
            // Arrange
            Utf8XmlReader reader = new("/"u8, isReadingCompleted: false, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false));

            // Act
            bool? outcome = reader.TryReadEmptyElementEndingTerminal();

            // Assert
            Assert.False(outcome);
            Assert.Equal(0, reader.Position);
            Assert.Equal(TokenType.Attribute, reader.TokenType);
        }

        /// <summary>
        /// Empty element S5.b: <c>/&gt;</c> closing the start tag clears the flag
        /// (<c>&lt;a /&gt;</c>, <c>&lt;a b="1" /&gt;</c>)
        /// </summary>
        [Theory]
        [InlineData(TokenType.Element, TokenType.None)]
        [InlineData(TokenType.Value, TokenType.Attribute)]
        public void ReadAttribute_ForEmptyElementS5bClosingTheStartTag_ItShould_ClearTheFlag(
            TokenType current,
            TokenType previous
        ) {
            // Arrange
            Utf8XmlReader reader = new("/>"u8, isReadingCompleted: true, StartTagState(current, hasSkippedSpacingInStartTag: true, previous, depth: 2));

            // Act
            bool? outcome = reader.TryReadEmptyElementEndingTerminal();

            // Assert
            Assert.True(outcome);
            Assert.Equal(TokenType.ElementEnd, reader.TokenType);
            Assert.False(reader.ReaderState._hasSkippedSpacingInStartTag);
        }
        #endregion

        #region S0
        /// <summary>
        /// S0: no spacing was skipped before the attribute, so the markup is malformed
        /// (<c>&lt;a b="1"c="2"&gt;</c>, <c>&lt;a b=</c>)
        /// </summary>
        [Theory]
        [InlineData("c=\"2\">", TokenType.Value, TokenType.Attribute)]
        [InlineData("=\"1\">", TokenType.Attribute, TokenType.Element)]
        public void ReadAttribute_ForS0NoSpacing_ItShould_Throw(
            string text,
            TokenType current,
            TokenType previous
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, StartTagState(current, hasSkippedSpacingInStartTag: false, previous));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }

        /// <summary>
        /// S0 is asserted before reading the name: an undecided name (S1) still throws when no
        /// spacing was skipped
        /// </summary>
        [Fact]
        public void ReadAttribute_ForS0NoSpacingBeforeAnUndecidedName_ItShould_ThrowBeforeReadingTheName()
        {
            // Arrange
            Utf8XmlReader reader = new("c"u8, isReadingCompleted: false, StartTagState(TokenType.Value, hasSkippedSpacingInStartTag: false, TokenType.Attribute));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion

        #region S1
        /// <summary>
        /// S1: reading the name returns false (more to come), so nothing is committed and the
        /// flag stays true
        /// </summary>
        [Theory]
        [InlineData("b", TokenType.Element)]
        [InlineData("StemKey", TokenType.Value)]
        public void ReadAttribute_ForS1NameNotRead_ItShould_ReturnFalseKeepingTheFlag(
            string text,
            TokenType current
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: false, StartTagState(current, hasSkippedSpacingInStartTag: true));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.False(advancement);
            Assert.Equal(0, reader.Position);
            Assert.Equal(0, reader.ReaderState._linePosition);
            Assert.Equal(current, reader.TokenType);
            Assert.True(reader.ReaderState._hasSkippedSpacingInStartTag);
        }

        /// <summary>
        /// S1 straddling segments
        /// </summary>
        [Theory]
        [InlineData([new[] { "St", "em" }])]
        [InlineData([new[] { "", "b" }])]
        [InlineData([new[] { "St", "", "em" }])]
        public void ReadAttribute_ForS1NameNotReadStraddlingSegments_ItShould_ReturnFalseKeepingTheFlag(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: false, StartTagState(TokenType.Element, hasSkippedSpacingInStartTag: true));
            SequencePosition sequencePosition = reader.SequencePosition;

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.False(advancement);
            Assert.Equal(sequencePosition, reader.SequencePosition);
            Assert.Equal(TokenType.Element, reader.TokenType);
            Assert.True(reader.ReaderState._hasSkippedSpacingInStartTag);
        }
        #endregion

        #region S2
        /// <summary>
        /// S2: reading the name throws, propagated - #38's S6 for a first character that doesn't
        /// start a name (the empty scratch pad never checked it), #38's S5 for an unsupported
        /// character, and <c>=</c> after spacing until #32 is built
        /// </summary>
        [Theory]
        [InlineData("1=\"x\">", TokenType.Element)]
        [InlineData("-a=\"x\">", TokenType.Element)]
        [InlineData("é=\"x\">", TokenType.Element)]
        [InlineData("bé=\"x\">", TokenType.Element)]
        [InlineData("=\"1\">", TokenType.Attribute)]
        public void ReadAttribute_ForS2NameThrows_ItShould_Propagate(
            string text,
            TokenType current
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, StartTagState(current, hasSkippedSpacingInStartTag: true));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }

        /// <summary>
        /// S2 straddling segments: the unsupported character is in a following segment
        /// </summary>
        [Fact]
        public void ReadAttribute_ForS2NameThrowsStraddlingSegments_ItShould_Propagate()
        {
            // Arrange
            Utf8XmlReader reader = new(Sequence("Stem", "é=\"x\">"), isReadingCompleted: true, StartTagState(TokenType.Element, hasSkippedSpacingInStartTag: true));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion

        #region S3
        /// <summary>
        /// S3: the name is read in <c>Value</c>, the flag cleared and the token types set, after
        /// the element name or after another attribute's value
        /// </summary>
        [Theory]
        [InlineData("b=\"1\">", TokenType.Element, TokenType.None, "b")]
        [InlineData("StemKey='1'/>", TokenType.Element, TokenType.None, "StemKey")]
        [InlineData("c=\"2\">", TokenType.Value, TokenType.Attribute, "c")]
        [InlineData("b>", TokenType.Element, TokenType.None, "b")]
        [InlineData("b\n=\"1\">", TokenType.Element, TokenType.None, "b")]
        public void ReadAttribute_ForS3NameInValue_ItShould_SetAttributeAndClearTheFlag(
            string text,
            TokenType current,
            TokenType previous,
            string expectedName
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, StartTagState(current, hasSkippedSpacingInStartTag: true, previous));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.False(reader.HasValueSequence);
            AssertAttributeRead(ref reader, expectedName, current);
        }

        /// <summary>
        /// S3: the line position advances from where the attribute name starts
        /// </summary>
        [Fact]
        public void ReadAttribute_ForS3AfterLinePosition_ItShould_AdvanceTheLinePosition()
        {
            // Arrange - e.g. "<Stem " already read up to the attribute name
            Utf8XmlReader reader = new("StemKey=\"1\">"u8, isReadingCompleted: true, StartTagState(TokenType.Element, hasSkippedSpacingInStartTag: true, linePosition: 6));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            AssertAttributeRead(ref reader, "StemKey", TokenType.Element, linePositionBefore: 6);
        }
        #endregion

        #region S4
        /// <summary>
        /// S4: the name straddles segments so it is in <c>ValueSequence</c>, nothing is copied
        /// (nothing is pushed)
        /// </summary>
        [Theory]
        [InlineData(new[] { "St", "emKey=" }, "StemKey")]
        [InlineData(new[] { "Stem", "", "Key=" }, "StemKey")]
        [InlineData(new[] { "StemKeyStemKeyStemKeyStemKey", "StemKeyStemK=" }, "StemKeyStemKeyStemKeyStemKeyStemKeyStemK")]
        public void ReadAttribute_ForS4NameInValueSequence_ItShould_SetAttributeAndClearTheFlag(
            string[] parts,
            string expectedName
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, StartTagState(TokenType.Element, hasSkippedSpacingInStartTag: true));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.True(reader.HasValueSequence);
            AssertAttributeRead(ref reader, expectedName, TokenType.Element);
        }
        #endregion

        #region S5
        /// <summary>
        /// S5: the name ends the document (#38's S8), so the attribute is read as in S3
        /// </summary>
        [Fact]
        public void ReadAttribute_ForS5NameAtTheEndOfTheDocument_ItShould_SetAttributeAndClearTheFlag()
        {
            // Arrange
            Utf8XmlReader reader = new("b"u8, isReadingCompleted: true, StartTagState(TokenType.Element, hasSkippedSpacingInStartTag: true));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            AssertAttributeRead(ref reader, "b", TokenType.Element);
        }

        /// <summary>
        /// S5: the next read throws because the element is not ended
        /// </summary>
        [Fact]
        public void ReadAttribute_ForS5NameAtTheEndOfTheDocument_ItShould_ThrowElementNotEndedOnTheNextRead()
        {
            // Arrange
            Utf8XmlReader reader = new("b"u8, isReadingCompleted: true, StartTagState(TokenType.Element, hasSkippedSpacingInStartTag: true));
            reader.ReadValue();

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion

        #region Read() wiring
        /// <summary>
        /// Through Read(): the start tag then the attribute name, the spacing between them sets
        /// the flag in the same read cycle that the delegate clears it
        /// </summary>
        [Theory]
        [InlineData("<a b", "a", "b", 4)]
        [InlineData("<Stem\n\tKey", "Stem", "Key", 10)]
        public void Read_ForAttributeAfterStartTag_ItShould_ReadElementThenAttribute(
            string text,
            string expectedElement,
            string expectedAttribute,
            long expectedPosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));

            // Act
            bool element = reader.Read();
            string elementName = ValueText(ref reader);
            bool attribute = reader.Read();

            // Assert
            Assert.True(element);
            Assert.Equal(expectedElement, elementName);
            Assert.True(attribute);
            Assert.Equal(TokenType.Attribute, reader.TokenType);
            Assert.Equal(TokenType.Element, reader.ReaderState._previousTokenType);
            Assert.Equal(expectedAttribute, ValueText(ref reader));
            Assert.Equal(expectedPosition, reader.Position);
            Assert.False(reader.ReaderState._hasSkippedSpacingInStartTag);
        }

        /// <summary>
        /// Through Read(): the flag survives a new reader instance - the spacing drains the
        /// first segment (<c>&lt;a |b</c>), the reader is reconstructed from its reader state
        /// and the attribute name is read from the next segment
        /// </summary>
        [Fact]
        public void Read_ForSpacingDrainingTheSegmentBeforeAttribute_ItShould_KeepTheFlagAcrossReaders()
        {
            // Arrange
            Utf8XmlReader first = new("<a "u8, isReadingCompleted: false, ContextState(Context.Prolog, TokenType.None));
            first.Read();
            bool halted = first.Read();
            ReaderState state = first.ReaderState;

            Utf8XmlReader second = new("b"u8, isReadingCompleted: true, state);

            // Act
            bool attribute = second.Read();

            // Assert
            Assert.False(halted);
            Assert.True(state._hasSkippedSpacingInStartTag);
            Assert.True(attribute);
            Assert.Equal(TokenType.Attribute, second.TokenType);
            Assert.Equal("b", ValueText(ref second));
        }
        #endregion
    }
}
