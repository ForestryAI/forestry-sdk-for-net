using System.Buffers;
using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- PeekStartingTerminal() shells, from #17 -----------------------------------------
        //
        // The `PeekStartingTerminal_*` tests below are shells written from #17's architecture
        // text alone (starting terminals table, state table S0-S4, requirements), before
        // PeekStartingTerminal()'s body exists - see doc/dev/Velocity.md's Test shell phase.
        //
        // Every row is covered twice: once from a single byte span and once straddling a byte
        // sequence of multiple segments (#17 requirement: straddling). The pre-condition (at least
        // one character available at the current position) is taken as given, not tested. The
        // scratch pad overflow is a Debug Assert that no row can reach (by 9 characters S1 or S2
        // has been asserted), so it has no shell.
        //
        // The scratch pad is only asserted on rows returning true. On S3 (false) the scratch pad
        // is transient and the caller expands the bytes, so its contents are not part of the
        // contract.
        //
        // Bug #25 shells: `/>` is an ending terminal and no longer an allowed starting terminal,
        // the quotes `"` and `'` are added as the starting terminals of an attribute value, and
        // the new S5 leaves the scratch pad empty when the first character is not the first
        // character of any allowed starting terminal. Every scratch pad therefore either begins
        // with the first character of an allowed starting terminal or is empty. S2 now only
        // applies to an overshoot after S0.
        //
        // Bug #36 shells: the declaration's starting terminal is `<?xml` (5 characters, the
        // following spacing belongs to the version non-terminal). S1 splits into S1.a (an
        // allowed starting terminal other than `<?xml`: break returning true) and S1.b (`<?xml`:
        // continue, so the next character is appended and the following pass breaks with S2,
        // or with S3/S4 when no character is available). Rows that reached `<?xml` moved from
        // S0 and S1 to S1.b; their expected scratch pads are unchanged.

        #region helpers
        private sealed class Segment : ReadOnlySequenceSegment<byte>
        {
            public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;

            public Segment Append(ReadOnlyMemory<byte> memory)
            {
                Segment next = new(memory) { RunningIndex = RunningIndex + Memory.Length };
                Next = next;
                return next;
            }
        }

        /// <summary>
        /// Byte sequence with one segment per part, empty parts become empty segments
        /// </summary>
        private static ReadOnlySequence<byte> Sequence(params string[] parts)
        {
            Segment first = new(Encoding.UTF8.GetBytes(parts[0]));
            Segment last = first;
            for (int i = 1; i < parts.Length; i++)
            {
                last = last.Append(Encoding.UTF8.GetBytes(parts[i]));
            }

            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }

        private static Utf8XmlReader SpanReader(string text, bool isReadingCompleted) =>
            new(Encoding.UTF8.GetBytes(text), isReadingCompleted, new ReaderState());

        private static Utf8XmlReader SequenceReader(string[] parts, bool isReadingCompleted) =>
            new(Sequence(parts), isReadingCompleted, new ReaderState());

        /// <summary>
        /// Characters recorded in the scratch pad i.e. only up to the character count
        /// </summary>
        private static string ScratchPad(ref Utf8XmlReader reader)
        {
            ReadOnlySpan<byte> scratchPad = reader._startingTerminals;
            return Encoding.UTF8.GetString(scratchPad[..reader._startingTerminalCharacterCount]);
        }
        #endregion

        #region S0
        /// <summary>
        /// Peeking continues when a longer allowed starting terminal starts with the 
        /// scratch pad, e.g. <c>&lt;</c> could still become <c>&lt;/</c> and <c>&lt;?</c> could still become <c>&lt;?xml</c>
        /// </summary>
        /// <remarks>When reader construction is from a byte span.  Peeking past an allowed 
        /// starting terminal: <c>&lt;a</c> and <c>&lt;?p</c> overshoot <c>&lt;</c> and <c>&lt;?</c> by one character, 
        /// see S1.b for <c>&lt;?xml</c>.</remarks>
        [Theory]
        [InlineData("<a>", "<a")]
        [InlineData("<?pi?>", "<?p")]
        public void PeekStartingTerminal_ForS0PrefixOfALongerTerminal_ItShould_PeekTheNextCharacter(
            string text, 
            string expected
        ) {
            // Arrange
            Utf8XmlReader reader = SpanReader(text, isReadingCompleted: true);

            // Act
            bool peeked = reader.PeekStartingTerminal();

            // Assert
            Assert.True(peeked);
            Assert.Equal(expected, ScratchPad(ref reader));
        }

        /// <summary>
        /// Peeking continues when a longer allowed starting terminal starts with the 
        /// scratch pad, e.g. <c>&lt;</c> could still become <c>&lt;/</c> and <c>&lt;?</c> could still become <c>&lt;?xml</c>
        /// </summary>
        /// <remarks>When reader construction is from a byte sequence</remarks>
        [Theory]
        [InlineData(new[] { "<", "a>" }, "<a")]
        [InlineData(new[] { "<?", "pi?>" }, "<?p")]
        public void PeekStartingTerminal_ForS0PrefixOfALongerTerminalStraddlingSegments_ItShould_PeekTheNextCharacter(
            string[] parts, string expected)
        {
            // Arrange
            Utf8XmlReader reader = SequenceReader(parts, isReadingCompleted: true);

            // Act
            bool peeked = reader.PeekStartingTerminal();

            // Assert
            Assert.True(peeked);
            Assert.Equal(expected, ScratchPad(ref reader));
        }
        #endregion

        #region S1.a
        /// <summary>
        /// S1.a: peeking breaks fast returning true when the contents of the scratch pad matches 
        /// an allowed starting terminal other than the declaration's <c>&lt;?xml</c> (#36)
        /// </summary>
        /// <remarks>When reader construction is from a byte span</remarks>
        [Theory]
        [InlineData("<!DOCTYPE root>", "<!DOCTYPE")]
        [InlineData("<!-- comment -->", "<!--")]
        [InlineData("</root>", "</")]
        [InlineData("\"1\"", "\"")]
        [InlineData("'1'", "'")]
        public void PeekStartingTerminal_ForS1AllowedStartingTerminal_ItShould_BreakReturningTrue(
            string text, string expected)
        {
            // Arrange
            Utf8XmlReader reader = SpanReader(text, isReadingCompleted: true);

            // Act
            bool peeked = reader.PeekStartingTerminal();

            // Assert
            Assert.True(peeked);
            Assert.Equal(expected, ScratchPad(ref reader));
        }

        /// <summary>
        /// S1.a: peeking breaks fast returning true when the contents of the scratch pad matches 
        /// an allowed starting terminal other than the declaration's <c>&lt;?xml</c> (#36)
        /// </summary>
        /// <remarks>When reader construction is from a byte sequence</remarks>
        [Theory]
        [InlineData(new[] { "<!DOC", "TYPE root>" }, "<!DOCTYPE")]
        [InlineData(new[] { "<", "!", "-", "- comment -->" }, "<!--")]
        [InlineData(new[] { "<", "", "/root>" }, "</")]
        [InlineData(new[] { "", "\"1\"" }, "\"")]
        [InlineData(new[] { "'", "1'" }, "'")]
        public void PeekStartingTerminal_ForS1AllowedStartingTerminalStraddlingSegments_ItShould_BreakReturningTrue(
            string[] parts, string expected)
        {
            // Arrange
            Utf8XmlReader reader = SequenceReader(parts, isReadingCompleted: true);

            // Act
            bool peeked = reader.PeekStartingTerminal();

            // Assert
            Assert.True(peeked);
            Assert.Equal(expected, ScratchPad(ref reader));
        }
        #endregion

        #region S1.b
        /// <summary>
        /// S1.b: the scratch pad equals the declaration's starting terminal <c>&lt;?xml</c>, so
        /// peeking continues and appends the next character, which the following pass breaks on
        /// with S2 (#36).  The extra character is all a candidate needs to tell a declaration
        /// (spacing) from a processing instruction (anything else).
        /// </summary>
        /// <remarks>When reader construction is from a byte span</remarks>
        [Theory]
        [InlineData("<?xml version=\"1.0\"?>", "<?xml ")]
        [InlineData("<?xml\tversion=\"1.0\"?>", "<?xml\t")]
        [InlineData("<?xml\rversion=\"1.0\"?>", "<?xml\r")]
        [InlineData("<?xml\nversion=\"1.0\"?>", "<?xml\n")]
        [InlineData("<?xml-stylesheet?>", "<?xml-")]
        [InlineData("<?xml?>", "<?xml?")]
        [InlineData("<?xmlx?>", "<?xmlx")]
        public void PeekStartingTerminal_ForS1bDeclarationStartingTerminal_ItShould_PeekTheNextCharacter(
            string text, string expected)
        {
            // Arrange
            Utf8XmlReader reader = SpanReader(text, isReadingCompleted: true);

            // Act
            bool peeked = reader.PeekStartingTerminal();

            // Assert
            Assert.True(peeked);
            Assert.Equal(expected, ScratchPad(ref reader));
        }

        /// <summary>
        /// S1.b: the next character after <c>&lt;?xml</c> may be in a following segment (#36)
        /// </summary>
        /// <remarks>When reader construction is from a byte sequence</remarks>
        [Theory]
        [InlineData(new[] { "<?xml", " version=\"1.0\"?>" }, "<?xml ")]
        [InlineData(new[] { "<?xml", "", "\nversion=\"1.0\"?>" }, "<?xml\n")]
        [InlineData(new[] { "<?x", "ml-", "stylesheet?>" }, "<?xml-")]
        [InlineData(new[] { "<?xm", "l", "?>" }, "<?xml?")]
        public void PeekStartingTerminal_ForS1bDeclarationStartingTerminalStraddlingSegments_ItShould_PeekTheNextCharacter(
            string[] parts, string expected)
        {
            // Arrange
            Utf8XmlReader reader = SequenceReader(parts, isReadingCompleted: true);

            // Act
            bool peeked = reader.PeekStartingTerminal();

            // Assert
            Assert.True(peeked);
            Assert.Equal(expected, ScratchPad(ref reader));
        }
        #endregion

        #region S2
        /// <summary>
        /// Peeking returns true when the contents of the scratch pad does 
        /// not match any starting terminal quietly ignoring potential malformed documents,
        /// only reachable after S0 i.e. an overshoot (#25)
        /// </summary>
        /// <remarks>When reader construction is from a byte span</remarks>
        [Theory]
        [InlineData("<a/>", "<a")]
        [InlineData("<![CDATA[x]]>", "<![")]
        [InlineData("<!Dx", "<!Dx")]
        [InlineData("<!-x", "<!-x")]
        public void PeekStartingTerminal_ForS2NoAllowedStartingTerminalStartsWithIt_ItShould_BreakReturningTrue(
            string text, string expected)
        {
            // Arrange
            Utf8XmlReader reader = SpanReader(text, isReadingCompleted: true);

            // Act
            bool peeked = reader.PeekStartingTerminal();

            // Assert
            Assert.True(peeked);
            Assert.Equal(expected, ScratchPad(ref reader));
        }

        /// <summary>
        /// Peeking returns true when the contents of the scratch pad does 
        /// not match any starting terminal quietly ignoring potential malformed documents,
        /// only reachable after S0 i.e. an overshoot (#25)
        /// </summary>
        /// <remarks>When reader construction is from a byte sequence</remarks>
        [Theory]
        [InlineData(new[] { "<", "a/>" }, "<a")]
        [InlineData(new[] { "<!", "[CDATA[x]]>" }, "<![")]
        [InlineData(new[] { "<!D", "x" }, "<!Dx")]
        [InlineData(new[] { "<!", "", "-x" }, "<!-x")]
        public void PeekStartingTerminal_ForS2NoAllowedStartingTerminalStartsWithItStraddlingSegments_ItShould_BreakReturningTrue(
            string[] parts, string expected)
        {
            // Arrange
            Utf8XmlReader reader = SequenceReader(parts, isReadingCompleted: true);

            // Act
            bool peeked = reader.PeekStartingTerminal();

            // Assert
            Assert.True(peeked);
            Assert.Equal(expected, ScratchPad(ref reader));
        }
        #endregion

        #region S3
        /// <summary>
        /// When the next character is not available to peek and reading has not 
        /// completed then return false leaving the responsibility to the caller 
        /// to expand the byte span
        /// </summary>
        [Theory]
        [InlineData("<")]
        [InlineData("<!")]
        [InlineData("<!DOCTYP")]
        [InlineData("<?xml")]
        [InlineData("<!-")]
        public void PeekStartingTerminal_ForS3NextCharacterNotAvailableAndReadingNotCompleted_ItShould_BreakReturningFalse(
            string text)
        {
            // Arrange
            Utf8XmlReader reader = SpanReader(text, isReadingCompleted: false);

            // Act
            bool peeked = reader.PeekStartingTerminal();

            // Assert
            Assert.False(peeked);
        }

        /// <summary>
        /// When the next character is not available to peek and reading has not 
        /// completed then return false leaving the responsibility to the caller 
        /// to expand the byte sequence
        /// </summary>
        [Theory]
        [InlineData([new[] { "<", "!" }])]
        [InlineData([new[] { "<!DOC", "TYP" }])]
        [InlineData([new[] { "<?", "x", "ml" }])]
        public void PeekStartingTerminal_ForS3NextCharacterNotAvailableAndReadingNotCompletedStraddlingSegments_ItShould_BreakReturningFalse(
            string[] parts)
        {
            // Arrange
            Utf8XmlReader reader = SequenceReader(parts, isReadingCompleted: false);

            // Act
            bool peeked = reader.PeekStartingTerminal();

            // Assert
            Assert.False(peeked);
        }
        #endregion

        #region S4
        /// <summary>
        /// When the next character is not available to peek and reading has 
        /// completed break fast returning true quietly ignoring malformed documents
        /// </summary>
        /// <remarks>When reader construction is from a byte span</remarks>
        [Theory]
        [InlineData("<")]
        [InlineData("<!")]
        [InlineData("<!DOCTYP")]
        [InlineData("<?xml")]
        [InlineData("<!-")]
        public void PeekStartingTerminal_ForS4NextCharacterNotAvailableAndReadingCompleted_ItShould_BreakReturningTrue(
            string text)
        {
            // Arrange
            Utf8XmlReader reader = SpanReader(text, isReadingCompleted: true);

            // Act
            bool peeked = reader.PeekStartingTerminal();

            // Assert - partial starting terminal handed forward, malformed left to following steps
            Assert.True(peeked);
            Assert.Equal(text, ScratchPad(ref reader));
        }

        /// <summary>
        /// When the next character is not available to peek and reading has 
        /// completed break fast returning true quietly ignoring malformed documents
        /// </summary>
        /// <remarks>When reader construction is from a byte sequence</remarks>
        [Theory]
        [InlineData(new[] { "<", "!" }, "<!")]
        [InlineData(new[] { "<!DOC", "TYP" }, "<!DOCTYP")]
        [InlineData(new[] { "<?", "x", "ml" }, "<?xml")]
        public void PeekStartingTerminal_ForS4NextCharacterNotAvailableAndReadingCompletedStraddlingSegments_ItShould_BreakReturningTrue(
            string[] parts, string expected)
        {
            // Arrange
            Utf8XmlReader reader = SequenceReader(parts, isReadingCompleted: true);

            // Act
            bool peeked = reader.PeekStartingTerminal();

            // Assert - partial starting terminal handed forward, malformed left to following steps
            Assert.True(peeked);
            Assert.Equal(expected, ScratchPad(ref reader));
        }
        #endregion

        #region S5
        /// <summary>
        /// Peeking breaks fast returning true with an empty scratch pad when the first character
        /// is not the first character of any allowed starting terminal (#25), e.g. the ending
        /// terminal <c>/&gt;</c>
        /// </summary>
        /// <remarks>When reader construction is from a byte span</remarks>
        [Theory]
        [InlineData("a")]
        [InlineData("=\"1\"")]
        [InlineData(">")]
        [InlineData("/")]
        [InlineData("/a")]
        [InlineData("/>")]
        [InlineData("&amp;")]
        public void PeekStartingTerminal_ForS5FirstCharacterStartsNoAllowedStartingTerminal_ItShould_BreakReturningTrueWithAnEmptyScratchPad(
            string text)
        {
            // Arrange
            Utf8XmlReader reader = SpanReader(text, isReadingCompleted: true);

            // Act
            bool peeked = reader.PeekStartingTerminal();

            // Assert
            Assert.True(peeked);
            Assert.Equal("", ScratchPad(ref reader));
        }

        /// <summary>
        /// Peeking breaks fast returning true with an empty scratch pad when the first character
        /// is not the first character of any allowed starting terminal (#25)
        /// </summary>
        /// <remarks>When reader construction is from a byte sequence</remarks>
        [Theory]
        [InlineData([new[] { "", "a" }])]
        [InlineData([new[] { "/", ">" }])]
        [InlineData([new[] { "", "", "/>" }])]
        public void PeekStartingTerminal_ForS5FirstCharacterStartsNoAllowedStartingTerminalStraddlingSegments_ItShould_BreakReturningTrueWithAnEmptyScratchPad(
            string[] parts)
        {
            // Arrange
            Utf8XmlReader reader = SequenceReader(parts, isReadingCompleted: true);

            // Act
            bool peeked = reader.PeekStartingTerminal();

            // Assert
            Assert.True(peeked);
            Assert.Equal("", ScratchPad(ref reader));
        }

        /// <summary>
        /// S5 does not halt: nothing is recorded so there is nothing to expand, even when the
        /// first character is the last one available and reading has not completed (#25)
        /// </summary>
        [Theory]
        [InlineData("/")]
        [InlineData("a")]
        public void PeekStartingTerminal_ForS5WhenReadingIsNotCompleted_ItShould_StillBreakReturningTrueWithAnEmptyScratchPad(
            string text)
        {
            // Arrange
            Utf8XmlReader reader = SpanReader(text, isReadingCompleted: false);

            // Act
            bool peeked = reader.PeekStartingTerminal();

            // Assert
            Assert.True(peeked);
            Assert.Equal("", ScratchPad(ref reader));
        }
        #endregion

        #region requirements
        /// <summary>
        /// Requirement peek only: Position and Sequence Position are unchanged for every row,
        /// including straddled peeks that walk into following segments
        /// </summary>
        [Theory]
        [InlineData(new[] { "<!DOC", "TYPE root>" }, true)]   // S1
        [InlineData(new[] { "<!", "", "-x" }, true)]          // S2
        [InlineData(new[] { "<!DOC", "TYP" }, false)]         // S3
        [InlineData(new[] { "<!DOC", "TYP" }, true)]          // S4
        [InlineData(new[] { "", "/>" }, true)]                // S5
        public void PeekStartingTerminal_ForAnyRowStraddlingSegments_ItShould_LeavePositionAndSequencePositionUnchanged(
            string[] parts, bool isReadingCompleted)
        {
            // Arrange
            Utf8XmlReader reader = SequenceReader(parts, isReadingCompleted);
            long position = reader.Position;
            SequencePosition sequencePosition = reader.SequencePosition;

            // Act
            reader.PeekStartingTerminal();

            // Assert
            Assert.Equal(position, reader.Position);
            Assert.Equal(sequencePosition, reader.SequencePosition);
        }

        /// <summary>
        /// Requirement peek only: Position is unchanged when peeking a byte span
        /// </summary>
        [Theory]
        [InlineData("<!DOCTYPE root>", true)]   // S1
        [InlineData("<a>", true)]               // S2
        [InlineData("<!DOCTYP", false)]         // S3
        [InlineData("<!DOCTYP", true)]          // S4
        [InlineData("/>", true)]                // S5
        public void PeekStartingTerminal_ForAnyRow_ItShould_LeavePositionUnchanged(
            string text, bool isReadingCompleted)
        {
            // Arrange
            Utf8XmlReader reader = SpanReader(text, isReadingCompleted);

            // Act
            reader.PeekStartingTerminal();

            // Assert
            Assert.Equal(0, reader.Position);
        }

        /// <summary>
        /// Requirement transient: the scratch pad starts with only the first character on every
        /// peek, so peeking twice without advancing records the same starting terminal rather
        /// than appending to the previous one
        /// </summary>
        [Fact]
        public void PeekStartingTerminal_ForASecondPeekOnTheSameReader_ItShould_RewriteTheScratchPad()
        {
            // Arrange
            Utf8XmlReader reader = SpanReader("<!-- comment -->", isReadingCompleted: true);
            reader.PeekStartingTerminal();

            // Act
            bool peeked = reader.PeekStartingTerminal();

            // Assert
            Assert.True(peeked);
            Assert.Equal("<!--", ScratchPad(ref reader));
        }
        #endregion
    }
}
