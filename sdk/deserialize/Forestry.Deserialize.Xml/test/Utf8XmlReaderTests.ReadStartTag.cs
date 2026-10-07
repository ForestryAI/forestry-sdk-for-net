using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Read start tag shells, from #37 -------------------------------------------------
        //
        // The `ReadStartTag_*` tests below are shells written from #37's architecture (skip
        // `<`, read the name with #38, roll back on false, push and set the token types on
        // true; state table S0-S5), before the delegate exists - see doc/dev/Velocity.md's Test
        // shell phase.
        //
        // The delegate is reached through ReadValue() (#27's delegation), so no shell names the
        // delegate method and there is no placeholder seam: the shells compile today and fail
        // on ReadValue's NotImplementedException until #37 is coded. A start tag candidate needs
        // the prolog (root element) or content (child element) context.
        //
        // Exceptions are only asserted by type (XmlException). #38's S1 and S9 can't occur here
        // (the candidate already found a name starting character after `<`), so S1 only covers
        // #38's S0 (unsupported). Whether a name that fits in one segment of a byte sequence is
        // a Value or a ValueSequence isn't decided, so only a name that straddles segments
        // asserts HasValueSequence. The name landing on the element stack is asserted by
        // popping it from a copy of the stack.

        #region helpers
        /// <summary>
        /// Assert the reader after a start tag was read (S2-S5): the token types, the value, the
        /// element stack one level deeper with content not ready and the name on top, the root
        /// element set, and the positions one more than the name's length
        /// </summary>
        private static void AssertStartTagRead(
            ref Utf8XmlReader reader,
            string expectedName,
            TokenType previous,
            int depthBefore,
            long linePositionBefore = 0
        ) {
            Assert.Equal(TokenType.Element, reader.TokenType);
            Assert.Equal(previous, reader.ReaderState._previousTokenType);
            Assert.Equal(expectedName, ValueText(ref reader));

            ElementStack stack = reader.ReaderState._elementStack;
            Assert.Equal(depthBefore + 1, stack.Depth);
            Assert.False(stack.ContentReady);
            Assert.True(stack.RootElement);
            Assert.True(stack.TryPop(Encoding.UTF8.GetBytes(expectedName)));

            Assert.Equal(1 + expectedName.Length, reader.Position);
            Assert.Equal(linePositionBefore + 1 + expectedName.Length, reader.ReaderState._linePosition);
        }
        #endregion

        #region S0
        /// <summary>
        /// S0: reading the name returns false (more to come), so the skipped <c>&lt;</c> is
        /// rolled back and nothing changes
        /// </summary>
        [Theory]
        [InlineData("<Stem", Context.Prolog, TokenType.None)]
        [InlineData("<a", Context.Prolog, TokenType.Declaration)]
        [InlineData("<StemKey", Context.Content, TokenType.Element)]
        public void ReadStartTag_ForS0NameNotRead_ItShould_RollBackReturningFalse(
            string text,
            Context context,
            TokenType current
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: false, ContextState(context, current));
            int depth = reader.Depth;

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.False(advancement);
            Assert.Equal(0, reader.Position);
            Assert.Equal(0, reader.ReaderState._linePosition);
            Assert.Equal(current, reader.TokenType);
            Assert.Equal(depth, reader.Depth);
        }

        /// <summary>
        /// S0 straddling segments, including <c>&lt;</c> as the last byte of its segment where
        /// the skip must not move into the next segment
        /// </summary>
        [Theory]
        [InlineData([new[] { "<", "Stem" }])]
        [InlineData([new[] { "<St", "em" }])]
        [InlineData([new[] { "<", "", "Stem" }])]
        public void ReadStartTag_ForS0NameNotReadStraddlingSegments_ItShould_RollBackReturningFalse(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: false, ContextState(Context.Prolog, TokenType.None));
            SequencePosition sequencePosition = reader.SequencePosition;

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.False(advancement);
            Assert.Equal(0, reader.Position);
            Assert.Equal(sequencePosition, reader.SequencePosition);
            Assert.Equal(0, reader.ReaderState._linePosition);
            Assert.Equal(TokenType.None, reader.TokenType);
            Assert.Equal(0, reader.Depth);
        }
        #endregion

        #region S1
        /// <summary>
        /// S1: reading the name throws (#38's S0 unsupported), propagated without a rollback
        /// </summary>
        [Theory]
        [InlineData("<é>")]
        [InlineData("<aé>")]
        public void ReadStartTag_ForS1NameThrows_ItShould_Propagate(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }

        /// <summary>
        /// S1 straddling segments: the unsupported character is in a following segment
        /// </summary>
        [Fact]
        public void ReadStartTag_ForS1NameThrowsStraddlingSegments_ItShould_Propagate()
        {
            // Arrange
            Utf8XmlReader reader = new(Sequence("<Stem", "é>"), isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion

        #region S2
        /// <summary>
        /// S2: the name is read in <c>Value</c>, pushed, and the token types set - the root
        /// element in the prolog, a child element in content
        /// </summary>
        [Theory]
        [InlineData("<a>", Context.Prolog, TokenType.None, "a")]
        [InlineData("<a/>", Context.Prolog, TokenType.Declaration, "a")]
        [InlineData("<StemKey b=\"1\">", Context.Prolog, TokenType.Comment, "StemKey")]
        [InlineData("<a\n>", Context.Prolog, TokenType.None, "a")]
        [InlineData("<b>", Context.Content, TokenType.Element, "b")]
        [InlineData("<b/>", Context.Content, TokenType.Value, "b")]
        public void ReadStartTag_ForS2NameInValue_ItShould_PushTheNameAndSetElement(
            string text,
            Context context,
            TokenType current,
            string expectedName
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(context, current));
            int depth = reader.Depth;

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.False(reader.HasValueSequence);
            AssertStartTagRead(ref reader, expectedName, current, depth);
        }

        /// <summary>
        /// S2: the line position advances from where the start tag starts
        /// </summary>
        [Fact]
        public void ReadStartTag_ForS2AfterLinePosition_ItShould_AdvanceTheLinePosition()
        {
            // Arrange - e.g. "<a>\n   " already read up to the child start tag
            ReaderState state = OpaqueState(Context.Content, TokenType.Element, lineNumber: 1, linePosition: 3);
            Utf8XmlReader reader = new("<StemKey>"u8, isReadingCompleted: true, state);

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.Equal(1, reader.ReaderState._lineNumber);
            AssertStartTagRead(ref reader, "StemKey", TokenType.Element, depthBefore: 1, linePositionBefore: 3);
        }
        #endregion

        #region S3
        /// <summary>
        /// S3: the name straddles segments so it is in <c>ValueSequence</c>, copied to a
        /// contiguous span before being pushed
        /// </summary>
        [Theory]
        [InlineData(new[] { "<Stem", "Key>" }, "StemKey")]
        [InlineData(new[] { "<", "St", "emKey>" }, "StemKey")]
        [InlineData(new[] { "<Stem", "", "Key b=\"1\">" }, "StemKey")]
        public void ReadStartTag_ForS3NameInValueSequence_ItShould_PushTheNameAndSetElement(
            string[] parts,
            string expectedName
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.True(reader.HasValueSequence);
            AssertStartTagRead(ref reader, expectedName, TokenType.None, depthBefore: 0);
        }
        #endregion

        #region S4
        /// <summary>
        /// S4: a name longer than 32 bytes straddling segments is copied only up to the element
        /// stack's packed name length - copying it all would overflow the span. The value is
        /// still the whole name.
        /// </summary>
        [Theory]
        [InlineData(new[] { "<StemKeyStemKeyStemKey", "StemKeyStemKeyStemK>" }, "StemKeyStemKeyStemKeyStemKeyStemKeyStemK")]
        [InlineData(new[] { "<StemKeyStemKeyStemKeyStemKeyStemKey", "StemK>" }, "StemKeyStemKeyStemKeyStemKeyStemKeyStemK")]
        public void ReadStartTag_ForS4LongNameInValueSequence_ItShould_PushTheNameCapped(
            string[] parts,
            string expectedName
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.True(reader.HasValueSequence);
            AssertStartTagRead(ref reader, expectedName, TokenType.None, depthBefore: 0);
        }
        #endregion

        #region S5
        /// <summary>
        /// S5: the name ends the document (#38's S8), so the start tag is read as in S2
        /// </summary>
        [Fact]
        public void ReadStartTag_ForS5NameAtTheEndOfTheDocument_ItShould_PushTheNameAndSetElement()
        {
            // Arrange
            Utf8XmlReader reader = new("<Stem"u8, isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            AssertStartTagRead(ref reader, "Stem", TokenType.None, depthBefore: 0);
        }

        /// <summary>
        /// S5: the next read throws because the element is not ended
        /// </summary>
        [Fact]
        public void ReadStartTag_ForS5NameAtTheEndOfTheDocument_ItShould_ThrowElementNotEndedOnTheNextRead()
        {
            // Arrange
            Utf8XmlReader reader = new("<Stem"u8, isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));
            reader.ReadValue();

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion
    }
}
