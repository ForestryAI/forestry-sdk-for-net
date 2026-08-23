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
    }
}
