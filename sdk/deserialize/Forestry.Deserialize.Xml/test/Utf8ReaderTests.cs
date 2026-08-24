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
        // the caller needing to special-case which one it just called.

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
            bool skipped = Utf8Reader.TrySkip(source, terminal, out int bytesRead);

            // Assert
            Assert.True(skipped);
            Assert.Equal(expectedBytesRead, bytesRead);
        }

        [Theory]
        [InlineData("version=\"1.0\"", "?>")]    // terminal never appears at all
        [InlineData("?", "?>")]                  // only half the terminal is present
        [InlineData("?X", "?>")]                 // near-miss - looks close but isn't a match
        public void TrySkip_ForATerminalNotPresentInSource_ItShould_ReturnFalseAndLeaveBytesReadAtZero(
            string sourceText, string terminalText)
        {
            // Arrange
            byte[] source = Encoding.UTF8.GetBytes(sourceText);
            byte[] terminal = Encoding.UTF8.GetBytes(terminalText);

            // Act
            bool skipped = Utf8Reader.TrySkip(source, terminal, out int bytesRead);

            // Assert
            Assert.False(skipped);
            Assert.Equal(0, bytesRead);
        }

        [Fact]
        public void TrySkip_ForAnEmptySource_ItShould_ReturnFalse()
        {
            // Act
            bool skipped = Utf8Reader.TrySkip(ReadOnlySpan<byte>.Empty, "?>"u8, out int bytesRead);

            // Assert
            Assert.False(skipped);
            Assert.Equal(0, bytesRead);
        }

        [Fact]
        public void TrySkip_ForAnEmptyTerminal_ItShould_ReturnFalse()
        {
            // Act
            bool skipped = Utf8Reader.TrySkip("version=\"1.0\"?>"u8, ReadOnlySpan<byte>.Empty, out int bytesRead);

            // Assert
            Assert.False(skipped);
            Assert.Equal(0, bytesRead);
        }

        [Fact]
        public void TrySkip_ForASourceShorterThanTheTerminal_ItShould_ReturnFalse()
        {
            // Act
            bool skipped = Utf8Reader.TrySkip("?"u8, "?>"u8, out int bytesRead);

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
            bool skipped = Utf8Reader.TrySkip(source, terminal, out int bytesRead);

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
            bool skipped = Utf8Reader.TrySkip(remainder, terminal, out int bytesRead);

            // Assert
            Assert.True(skipped);
            Assert.Equal(remainder.Length, bytesRead); // "?>" is the last 2 bytes, so this consumes everything
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
            bool skipped = Utf8Reader.TrySkip(source, terminal, out int bytesRead);

            // Assert
            Assert.True(skipped);
            Assert.NotEqual(source.Length, bytesRead); // stops at the internal '>', not the real one
            Assert.Equal(" foo [<!ELEMENT foo (#PCDATA)>".Length, bytesRead);
        }
    }
}
