using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- SkipSpacing() shells, from #34 --------------------------------------------------
        //
        // The `SkipSpacing_*` tests below are shells written from #34's architecture (context
        // break fast S0-S2, spacing-only character data S3-S6), before the change exists - see
        // doc/dev/Velocity.md's Test shell phase.
        //
        // SkipSpacing() returns true when it halts the read (drained or undecided) and false
        // otherwise. Spacing is space, tab, carriage return and line feed. Content is depth != 0
        // and content ready, as in #27's context table. Line tracking is asserted on the reader
        // state (0-based); only line feeds start a new line.
        //
        // The pre-condition (at least one character available at the current position) is taken
        // as given, like #17's.

        #region helpers
        /// <summary>
        /// Skip spacing catching the exception, a ref struct can't be captured by Assert.Throws
        /// </summary>
        private static Exception? SkipSpacingThrown(ref Utf8XmlReader reader)
        {
            try
            {
                reader.SkipSpacing();
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
        /// S0: the character at the reader's position is not spacing, so skipping breaks fast
        /// </summary>
        [Theory]
        [InlineData("a", Context.Prolog, TokenType.None)]
        [InlineData("<a/>", Context.Prolog, TokenType.None)]
        [InlineData("text", Context.Content, TokenType.Element)]
        [InlineData("<b/>", Context.Content, TokenType.Element)]
        public void SkipSpacing_ForS0NotSpacing_ItShould_BreakFastReturningFalse(
            string text,
            Context context,
            TokenType current
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(context, current));

            // Act
            bool halted = reader.SkipSpacing();

            // Assert
            Assert.False(halted);
            Assert.Equal(0, reader.Position);
            Assert.Equal(0, reader.ReaderState._lineNumber);
            Assert.Equal(0, reader.ReaderState._linePosition);
        }
        #endregion

        #region S1
        /// <summary>
        /// S1: outside content (prolog, start tag, miscellaneous) all spacing is skipped with
        /// line tracking, including spacing that starts with a tab, carriage return or line feed
        /// </summary>
        [Theory]
        [InlineData("  <a/>", Context.Prolog, TokenType.None, 2, 0, 2)]
        [InlineData("\t<a/>", Context.Prolog, TokenType.None, 1, 0, 1)]
        [InlineData("\n<a/>", Context.Prolog, TokenType.Declaration, 1, 1, 0)]
        [InlineData("\r\n<a/>", Context.Prolog, TokenType.Declaration, 2, 1, 0)]
        [InlineData("\n \t<a/>", Context.Prolog, TokenType.Comment, 3, 1, 2)]
        [InlineData(" b=\"1\"", Context.StartTag, TokenType.Element, 1, 0, 1)]
        [InlineData("\n  b=\"1\"", Context.StartTag, TokenType.Value, 3, 1, 2)]
        [InlineData("\n<!-- c -->", Context.Miscellaneous, TokenType.ElementEnd, 1, 1, 0)]
        public void SkipSpacing_ForS1NotInContent_ItShould_SkipAllSpacingWithLineTracking(
            string text,
            Context context,
            TokenType current,
            long expectedPosition,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(context, current));

            // Act
            bool halted = reader.SkipSpacing();

            // Assert
            Assert.False(halted);
            Assert.Equal(expectedPosition, reader.Position);
            Assert.Equal(expectedLineNumber, reader.ReaderState._lineNumber);
            Assert.Equal(expectedLinePosition, reader.ReaderState._linePosition);
        }

        /// <summary>
        /// S1: spacing split across segments is skipped as one, with line tracking carried
        /// across the segments
        /// </summary>
        [Theory]
        [InlineData(new[] { "\n ", " <a/>" }, 3, 1, 2)]
        [InlineData(new[] { "\t", "", "\n<a/>" }, 2, 1, 0)]
        public void SkipSpacing_ForS1NotInContentStraddlingSegments_ItShould_SkipAllSpacingWithLineTracking(
            string[] parts,
            long expectedPosition,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));

            // Act
            bool halted = reader.SkipSpacing();

            // Assert
            Assert.False(halted);
            Assert.Equal(expectedPosition, reader.Position);
            Assert.Equal(expectedLineNumber, reader.ReaderState._lineNumber);
            Assert.Equal(expectedLinePosition, reader.ReaderState._linePosition);
        }

        /// <summary>
        /// S1: content ready can still be true at depth 0 after the root element closes, so
        /// content must also require depth != 0 - otherwise spacing in the miscellaneous
        /// non-terminal would be taken for character data
        /// </summary>
        [Fact]
        public void SkipSpacing_ForS1ContentReadyAtDepthZero_ItShould_SkipAllSpacing()
        {
            // Arrange - depth 0 with content ready true (a child pushed and popped)
            ReaderState state = State(ElementStackAtDepth(0, contentReady: true), TokenType.ElementEnd);
            Utf8XmlReader reader = new("\n<!-- c -->"u8, isReadingCompleted: true, state);

            // Act
            bool halted = reader.SkipSpacing();

            // Assert
            Assert.False(halted);
            Assert.Equal(1, reader.Position);
            Assert.Equal(1, reader.ReaderState._lineNumber);
        }

        /// <summary>
        /// S1: skipping drains the segment(s), halting the read
        /// </summary>
        [Theory]
        [InlineData("  ", Context.Prolog, TokenType.None, false)]
        [InlineData("\n ", Context.Miscellaneous, TokenType.ElementEnd, true)]
        public void SkipSpacing_ForS1Drained_ItShould_ReturnTrue(
            string text,
            Context context,
            TokenType current,
            bool isReadingCompleted
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted, ContextState(context, current));

            // Act
            bool halted = reader.SkipSpacing();

            // Assert
            Assert.True(halted);
            Assert.Equal(text.Length, reader.Position);
        }
        #endregion

        #region S3
        /// <summary>
        /// S3: in content (S2), spacing followed by <c>&lt;</c> is character data with only
        /// spacing, which is ignored by default: skipped with line tracking
        /// </summary>
        [Theory]
        [InlineData("\n  <b/>", 3, 1, 2)]
        [InlineData("  <!-- c -->", 2, 0, 2)]
        [InlineData("\r\n\t</a>", 3, 1, 1)]
        public void SkipSpacing_ForS3OnlySpacingInContent_ItShould_SkipItWithLineTracking(
            string text,
            long expectedPosition,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(Context.Content, TokenType.Element));

            // Act
            bool halted = reader.SkipSpacing();

            // Assert
            Assert.False(halted);
            Assert.Equal(expectedPosition, reader.Position);
            Assert.Equal(expectedLineNumber, reader.ReaderState._lineNumber);
            Assert.Equal(expectedLinePosition, reader.ReaderState._linePosition);
        }

        /// <summary>
        /// S3 straddling segments: the spacing and the following <c>&lt;</c> are in different
        /// segments
        /// </summary>
        [Theory]
        [InlineData(new[] { "\n ", "  <b/>" }, 4, 1, 3)]
        [InlineData(new[] { "\n", "", "<b/>" }, 1, 1, 0)]
        public void SkipSpacing_ForS3OnlySpacingInContentStraddlingSegments_ItShould_SkipItWithLineTracking(
            string[] parts,
            long expectedPosition,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, ContextState(Context.Content, TokenType.Element));

            // Act
            bool halted = reader.SkipSpacing();

            // Assert
            Assert.False(halted);
            Assert.Equal(expectedPosition, reader.Position);
            Assert.Equal(expectedLineNumber, reader.ReaderState._lineNumber);
            Assert.Equal(expectedLinePosition, reader.ReaderState._linePosition);
        }
        #endregion

        #region S4
        /// <summary>
        /// S4: in content, spacing followed by anything other than <c>&lt;</c> belongs to
        /// character data, so nothing is skipped (simulated rollback)
        /// </summary>
        [Theory]
        [InlineData("\n  text</a>")]
        [InlineData("  &amp;")]
        [InlineData(" >")]
        public void SkipSpacing_ForS4SpacingBeforeCharacterData_ItShould_SkipNothing(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, ContextState(Context.Content, TokenType.Element));

            // Act
            bool halted = reader.SkipSpacing();

            // Assert
            Assert.False(halted);
            Assert.Equal(0, reader.Position);
            Assert.Equal(0, reader.ReaderState._lineNumber);
            Assert.Equal(0, reader.ReaderState._linePosition);
        }

        /// <summary>
        /// S4 straddling segments: the character data starts in a following segment
        /// </summary>
        [Theory]
        [InlineData([new[] { "\n ", " text</a>" }])]
        [InlineData([new[] { "\n", "", "text</a>" }])]
        public void SkipSpacing_ForS4SpacingBeforeCharacterDataStraddlingSegments_ItShould_SkipNothing(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, ContextState(Context.Content, TokenType.Element));
            SequencePosition sequencePosition = reader.SequencePosition;

            // Act
            bool halted = reader.SkipSpacing();

            // Assert
            Assert.False(halted);
            Assert.Equal(0, reader.Position);
            Assert.Equal(sequencePosition, reader.SequencePosition);
            Assert.Equal(0, reader.ReaderState._lineNumber);
        }
        #endregion

        #region S5
        /// <summary>
        /// S5: in content, only spacing is available and reading is not completed, so whether it
        /// is only spacing can't be decided yet: nothing is skipped and the read halts
        /// </summary>
        [Fact]
        public void SkipSpacing_ForS5UndecidedInContent_ItShould_SkipNothingReturningTrue()
        {
            // Arrange
            Utf8XmlReader reader = new("\n   "u8, isReadingCompleted: false, ContextState(Context.Content, TokenType.Element));

            // Act
            bool halted = reader.SkipSpacing();

            // Assert
            Assert.True(halted);
            Assert.Equal(0, reader.Position);
            Assert.Equal(0, reader.ReaderState._lineNumber);
            Assert.Equal(0, reader.ReaderState._linePosition);
        }

        /// <summary>
        /// S5 straddling segments
        /// </summary>
        [Theory]
        [InlineData([new[] { "\n ", "  " }])]
        [InlineData([new[] { "\n", "", " " }])]
        public void SkipSpacing_ForS5UndecidedInContentStraddlingSegments_ItShould_SkipNothingReturningTrue(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: false, ContextState(Context.Content, TokenType.Element));
            SequencePosition sequencePosition = reader.SequencePosition;

            // Act
            bool halted = reader.SkipSpacing();

            // Assert
            Assert.True(halted);
            Assert.Equal(0, reader.Position);
            Assert.Equal(sequencePosition, reader.SequencePosition);
            Assert.Equal(0, reader.ReaderState._lineNumber);
        }
        #endregion

        #region S6
        /// <summary>
        /// S6: in content, only spacing is left and reading is completed, so all spacing is
        /// skipped and the segment is drained
        /// </summary>
        /// <remarks>
        /// Draining with reading completed runs the existing state assertion, and depth != 0
        /// means the element was never ended: the missing end tag is reported here as the
        /// element-not-ended XML exception rather than by returning true
        /// </remarks>
        [Fact]
        public void SkipSpacing_ForS6OnlySpacingLeftInContentWhenReadingIsCompleted_ItShould_ThrowElementNotEnded()
        {
            // Arrange
            Utf8XmlReader reader = new("\n   "u8, isReadingCompleted: true, ContextState(Context.Content, TokenType.Element));

            // Act
            Exception? thrown = SkipSpacingThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion
    }
}
