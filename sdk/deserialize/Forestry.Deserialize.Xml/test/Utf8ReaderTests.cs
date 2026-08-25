using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    /// <summary>
    /// <see cref="Utf8Reader.TryMatch"/> is a dumb, non-throwing prefix-match primitive over raw
    /// UTF-8 bytes - it has no XML awareness of its own (no whitespace/comment skipping, that is
    /// <see cref="Utf8XmlReader"/>'s job as the caller). All it answers is "does target appear at
    /// the very start of source, and if so how many bytes of source did that consume."
    /// </summary>
    public class Utf8ReaderTests
    {
        [Theory]
        [InlineData("<?xml version=\"1.0\"?>", "<?xml", 5)]
        [InlineData("<?xml", "<?xml", 5)]
        [InlineData("<!DOCTYPE foo>", "<!DOCTYPE", 9)]
        public void TryMatch_ForAMatchingPrefix_ItShould_ReturnTrueAndReportTargetLength(
            string sourceText, string targetText, int expectedBytesRead)
        {
            // Arrange
            byte[] source = Encoding.UTF8.GetBytes(sourceText);
            byte[] target = Encoding.UTF8.GetBytes(targetText);

            // Act
            bool matched = Utf8Reader.TryMatch(source, target, out int bytesRead);

            // Assert
            Assert.True(matched);
            Assert.Equal(expectedBytesRead, bytesRead);
        }

        [Theory]
        [InlineData("<?xm", "<?xml")]              // source shorter than target
        [InlineData("<?XML version", "<?xml")]     // wrong case - XML terminals are case-sensitive
        [InlineData("<?xmm version", "<?xml")]     // mismatch at the last byte of target
        [InlineData(" <?xml version", "<?xml")]    // leading space - TryMatch does not skip it
        public void TryMatch_ForANonMatchingPrefix_ItShould_ReturnFalseAndLeaveBytesReadAtZero(
            string sourceText, string targetText)
        {
            // Arrange
            byte[] source = Encoding.UTF8.GetBytes(sourceText);
            byte[] target = Encoding.UTF8.GetBytes(targetText);

            // Act
            bool matched = Utf8Reader.TryMatch(source, target, out int bytesRead);

            // Assert
            Assert.False(matched);
            Assert.Equal(0, bytesRead);
        }

        [Fact]
        public void TryMatch_ForAnEmptySource_ItShould_ReturnFalse()
        {
            // Act
            bool matched = Utf8Reader.TryMatch(ReadOnlySpan<byte>.Empty, "<?xml"u8, out int bytesRead);

            // Assert
            Assert.False(matched);
            Assert.Equal(0, bytesRead);
        }

        [Fact]
        public void TryMatch_ForAnEmptyTarget_ItShould_ReturnFalse()
        {
            // Act
            bool matched = Utf8Reader.TryMatch("<?xml"u8, ReadOnlySpan<byte>.Empty, out int bytesRead);

            // Assert
            Assert.False(matched);
            Assert.Equal(0, bytesRead);
        }

        [Fact]
        public void TryMatch_ForASourceLongerThanTarget_ItShould_OnlyReportTargetLengthNotFullSourceLength()
        {
            // Arrange - a real declaration is much longer than "<?xml"; bytesRead must reflect
            // only how much of the *target* was consumed, not everything present in source.
            byte[] source = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            byte[] target = "<?xml"u8.ToArray();

            // Act
            bool matched = Utf8Reader.TryMatch(source, target, out int bytesRead);

            // Assert
            Assert.True(matched);
            Assert.Equal(target.Length, bytesRead);
            Assert.NotEqual(source.Length, bytesRead);
        }

        [Fact]
        public void TryMatch_ForMultiByteUtf8Content_ItShould_MatchByRawBytesNotCharacters()
        {
            // Arrange - "café" is 4 characters but 5 UTF-8 bytes ('é' is 2 bytes); the comparison
            // has to be correct across a multi-byte sequence, not per displayed character.
            byte[] source = Encoding.UTF8.GetBytes("café bar");
            byte[] target = Encoding.UTF8.GetBytes("café");

            // Act
            bool matched = Utf8Reader.TryMatch(source, target, out int bytesRead);

            // Assert
            Assert.True(matched);
            Assert.Equal(5, target.Length); // sanity check on the fixture itself
            Assert.Equal(target.Length, bytesRead);
        }

        // <see cref="Utf8Reader.TrySkip"/> is the opposite shape of TryMatch: instead of checking
        // one terminal at the very start of source, it ignores everything - terminals and
        // non-terminals alike - until it finds a specific terminal *anywhere* in source. Its
        // bytesRead deliberately matches TryMatch's convention (distance to land just past what
        // was found), not just the position where the terminal starts, so the two compose without
        // the caller needing to special-case which one it just called. It also reports
        // lineNumbersRead/linePosition for whatever newlines were crossed *within the consumed
        // region only* - never anything beyond the terminal, even if more of source follows it.

        [Theory]
        [InlineData("version=\"1.0\"?>", "?>", 15)]         // terminal at the very end of source
        [InlineData("?>", "?>", 2)]                          // terminal is the entire source
        [InlineData("foo>", ">", 4)]                         // single-byte terminal
        [InlineData(">bar>", ">", 1)]                        // terminal at the very start - finds the first one
        public void TrySkip_ForATerminalPresentInSource_ItShould_ReturnTrueAndReportBytesPastTheTerminal(
            string sourceText, string terminalText, int expectedBytesRead)
        {
            // Arrange
            byte[] source = Encoding.UTF8.GetBytes(sourceText);
            byte[] terminal = Encoding.UTF8.GetBytes(terminalText);

            // Act
            bool skipped = Utf8Reader.TrySkip(source, terminal, out int bytesRead, out int lineNumbersRead, out int linePosition);

            // Assert
            Assert.True(skipped);
            Assert.Equal(expectedBytesRead, bytesRead);
            Assert.Equal(0, lineNumbersRead);       // none of these fixtures contain a newline
            Assert.Equal(expectedBytesRead, linePosition);
        }

        [Theory]
        [InlineData("version=\"1.0\"", "?>")]    // terminal never appears at all
        [InlineData("?", "?>")]                  // only half the terminal is present
        [InlineData("?X", "?>")]                 // near-miss - looks close but isn't a match
        public void TrySkip_ForATerminalNotPresentInSource_ItShould_ReturnFalseAndLeaveOutParametersAtZero(
            string sourceText, string terminalText)
        {
            // Arrange
            byte[] source = Encoding.UTF8.GetBytes(sourceText);
            byte[] terminal = Encoding.UTF8.GetBytes(terminalText);

            // Act
            bool skipped = Utf8Reader.TrySkip(source, terminal, out int bytesRead, out int lineNumbersRead, out int linePosition);

            // Assert
            Assert.False(skipped);
            Assert.Equal(0, bytesRead);
            Assert.Equal(0, lineNumbersRead);
            Assert.Equal(0, linePosition);
        }

        [Fact]
        public void TrySkip_ForAnEmptySource_ItShould_ReturnFalse()
        {
            // Act
            bool skipped = Utf8Reader.TrySkip(ReadOnlySpan<byte>.Empty, "?>"u8, out int bytesRead, out _, out _);

            // Assert
            Assert.False(skipped);
            Assert.Equal(0, bytesRead);
        }

        [Fact]
        public void TrySkip_ForAnEmptyTerminal_ItShould_ReturnFalse()
        {
            // Act
            bool skipped = Utf8Reader.TrySkip("version=\"1.0\"?>"u8, ReadOnlySpan<byte>.Empty, out int bytesRead, out _, out _);

            // Assert
            Assert.False(skipped);
            Assert.Equal(0, bytesRead);
        }

        [Fact]
        public void TrySkip_ForASourceShorterThanTheTerminal_ItShould_ReturnFalse()
        {
            // Act
            bool skipped = Utf8Reader.TrySkip("?"u8, "?>"u8, out int bytesRead, out _, out _);

            // Assert
            Assert.False(skipped);
            Assert.Equal(0, bytesRead);
        }

        [Fact]
        public void TrySkip_ForACaseDifferentTerminal_ItShould_ReturnFalse()
        {
            // Arrange - exact byte comparison, same as TryMatch; no case-insensitive fallback.
            byte[] source = Encoding.UTF8.GetBytes("foo END bar");
            byte[] terminal = Encoding.UTF8.GetBytes("end");

            // Act
            bool skipped = Utf8Reader.TrySkip(source, terminal, out int bytesRead, out _, out _);

            // Assert
            Assert.False(skipped);
            Assert.Equal(0, bytesRead);
        }

        [Fact]
        public void TrySkip_ForARealDeclarationBody_ItShould_ReportBytesThroughTheClosingTerminal()
        {
            // Arrange - the segment that's left once TryMatch has already consumed "<?xml"; TrySkip
            // is what carries the reader the rest of the way through the declaration's opaque
            // attributes to (and past) its closing "?>".
            byte[] remainder = Encoding.UTF8.GetBytes(" version=\"1.0\" encoding=\"utf-8\"?>");
            byte[] terminal = "?>"u8.ToArray();

            // Act
            bool skipped = Utf8Reader.TrySkip(remainder, terminal, out int bytesRead, out int lineNumbersRead, out _);

            // Assert
            Assert.True(skipped);
            Assert.Equal(remainder.Length, bytesRead); // "?>" is the last 2 bytes, so this consumes everything
            Assert.Equal(0, lineNumbersRead);           // single-line declaration, nothing to count
        }

        [Fact]
        public void TrySkip_ForADoctypeWithAnInternalSubset_ItShould_StopAtTheFirstAngleBracketNotTheRealOne()
        {
            // Arrange - documents the known, deferred POC gap from #17/#19: TrySkip has no concept
            // of the '[' ... ']' internal subset, so a '>' that legally belongs to a markup
            // declaration *inside* the subset (here, <!ELEMENT foo (#PCDATA)>) is indistinguishable
            // from the DOCTYPE's own real closing '>'. This test pins down today's actual (wrong,
            // but intentional-for-now) behavior so a future fix has something concrete to change.
            byte[] source = Encoding.UTF8.GetBytes(" foo [<!ELEMENT foo (#PCDATA)>]>");
            byte[] terminal = ">"u8.ToArray();

            // Act
            bool skipped = Utf8Reader.TrySkip(source, terminal, out int bytesRead, out _, out _);

            // Assert
            Assert.True(skipped);
            Assert.NotEqual(source.Length, bytesRead); // stops at the internal '>', not the real one
            Assert.Equal(" foo [<!ELEMENT foo (#PCDATA)>".Length, bytesRead);
        }

        [Fact]
        public void TrySkip_ForContentWithOneNewline_ItShould_ReportOneLineAndPositionSinceIt()
        {
            // Arrange - "foo\nbar?>": one newline, then 5 bytes ("bar?>") after it.
            byte[] source = Encoding.UTF8.GetBytes("foo\nbar?>");
            byte[] terminal = "?>"u8.ToArray();

            // Act
            bool skipped = Utf8Reader.TrySkip(source, terminal, out int bytesRead, out int lineNumbersRead, out int linePosition);

            // Assert
            Assert.True(skipped);
            Assert.Equal(source.Length, bytesRead);
            Assert.Equal(1, lineNumbersRead);
            Assert.Equal("bar?>".Length, linePosition);
        }

        [Fact]
        public void TrySkip_ForContentWithMultipleNewlines_ItShould_CountAllOfThemAndPositionSinceTheLast()
        {
            // Arrange - "a\nb\nc?>": two newlines, then 3 bytes ("c?>") after the last one.
            byte[] source = Encoding.UTF8.GetBytes("a\nb\nc?>");
            byte[] terminal = "?>"u8.ToArray();

            // Act
            bool skipped = Utf8Reader.TrySkip(source, terminal, out int bytesRead, out int lineNumbersRead, out int linePosition);

            // Assert
            Assert.True(skipped);
            Assert.Equal(source.Length, bytesRead);
            Assert.Equal(2, lineNumbersRead);
            Assert.Equal("c?>".Length, linePosition);
        }

        [Fact]
        public void TrySkip_ForANewlineImmediatelyBeforeTheTerminal_ItShould_ReportPositionAsJustTheTerminal()
        {
            // Arrange - "foo\n?>": the newline is the very last byte before the terminal starts.
            byte[] source = Encoding.UTF8.GetBytes("foo\n?>");
            byte[] terminal = "?>"u8.ToArray();

            // Act
            bool skipped = Utf8Reader.TrySkip(source, terminal, out int bytesRead, out int lineNumbersRead, out int linePosition);

            // Assert
            Assert.True(skipped);
            Assert.Equal(1, lineNumbersRead);
            Assert.Equal("?>".Length, linePosition);
        }

        [Fact]
        public void TrySkip_ForNewlinesAfterTheTerminal_ItShould_NotCountThemAtAll()
        {
            // Arrange - regression case for the scoping bug: source is everything left in the
            // segment, not just this token's own content. A comment "<!--hello-->" immediately
            // followed by "\n\n<Root>" still sitting in the same buffered segment - those two
            // newlines belong to whatever comes *after* the comment, not to it, and must not be
            // counted as if they were part of this skip.
            byte[] source = Encoding.UTF8.GetBytes("hello-->\n\n<Root>");
            byte[] terminal = "-->"u8.ToArray();

            // Act
            bool skipped = Utf8Reader.TrySkip(source, terminal, out int bytesRead, out int lineNumbersRead, out int linePosition);

            // Assert
            Assert.True(skipped);
            Assert.Equal("hello-->".Length, bytesRead);
            Assert.Equal(0, lineNumbersRead);      // the two newlines are past the terminal - not counted
            Assert.Equal("hello-->".Length, linePosition);
        }

        // <see cref="Utf8Reader.LineFeeds"/> is the building block TrySkip uses for newline
        // counting - tested directly here since it's public and self-contained.

        [Fact]
        public void LineFeeds_ForContentWithNoNewline_ItShould_ReturnZeroAndNegativeOneIndex()
        {
            // Act
            (int count, int lastIndex) = Utf8Reader.LineFeeds("hello"u8);

            // Assert
            Assert.Equal(0, count);
            Assert.Equal(-1, lastIndex);
        }

        [Theory]
        [InlineData("a\nb", 1, 1)]
        [InlineData("a\nb\nc", 2, 3)]
        [InlineData("\na", 1, 0)]          // newline at the very start
        [InlineData("a\n", 1, 1)]          // newline at the very end
        public void LineFeeds_ForContentWithNewlines_ItShould_ReturnTheCountAndLastIndex(
            string text, int expectedCount, int expectedLastIndex)
        {
            // Arrange
            byte[] bytes = Encoding.UTF8.GetBytes(text);

            // Act
            (int count, int lastIndex) = Utf8Reader.LineFeeds(bytes);

            // Assert
            Assert.Equal(expectedCount, count);
            Assert.Equal(expectedLastIndex, lastIndex);
        }

        // <see cref="Utf8Reader.IndexOfExceptWhiteSpace"/> - the building block intended for the
        // future Skip Spacing work, not currently used by TrySkip's own line-position math.

        [Fact]
        public void IndexOfExceptWhiteSpace_ForAllWhiteSpace_ItShould_ReturnTheSpanLength()
        {
            // Act
            int index = " \t\r\n "u8.IndexOfExceptWhiteSpace();

            // Assert
            Assert.Equal(5, index);
        }

        [Fact]
        public void IndexOfExceptWhiteSpace_ForNoLeadingWhiteSpace_ItShould_ReturnZero()
        {
            // Act
            int index = "foo"u8.IndexOfExceptWhiteSpace();

            // Assert
            Assert.Equal(0, index);
        }

        [Fact]
        public void IndexOfExceptWhiteSpace_ForLeadingWhiteSpaceThenContent_ItShould_ReturnTheFirstNonWhiteSpaceIndex()
        {
            // Act
            int index = "  \tfoo"u8.IndexOfExceptWhiteSpace();

            // Assert
            Assert.Equal(3, index);
        }

        [Fact]
        public void IndexOfExceptWhiteSpace_ForAnEmptySpan_ItShould_ReturnZero()
        {
            // Act
            int index = ReadOnlySpan<byte>.Empty.IndexOfExceptWhiteSpace();

            // Assert
            Assert.Equal(0, index);
        }
    }
}
