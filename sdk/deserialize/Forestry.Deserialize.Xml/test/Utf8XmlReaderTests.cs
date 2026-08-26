using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    /// <summary>
    /// First end-to-end coverage of <see cref="Utf8XmlReader.Read"/> - exercising the real
    /// <see cref="Utf8XmlReader.ReadDocument"/>/<see cref="Utf8XmlReader.ReadSingleSegmentOpaqueValue"/>
    /// path for a Declaration, not just the <see cref="Utf8Reader"/> primitives it's built from.
    /// </summary>
    public class Utf8XmlReaderTests
    {
        [Theory]
        [InlineData("<?xml version=\"1.0\"?>")]
        [InlineData("<?xml version=\"1.0\" encoding=\"utf-8\"?>")]
        [InlineData("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>")]
        public void Read_ForADeclaration_ItShould_ReturnTrueAndYieldTheWholeRawDeclarationAsTheValue(
            string declarationText)
        {
            // Arrange
            byte[] source = Encoding.UTF8.GetBytes(declarationText);
            Utf8XmlReader reader = new(source);

            // Act
            bool readable = reader.Read();

            // Assert
            Assert.True(readable);
            Assert.Equal(TokenType.Declaration, reader.TokenType);
            Assert.True(((ReadOnlySpan<byte>)source).SequenceEqual(reader.Value));
        }

        [Fact]
        public void Read_ForADeclaration_ItShould_TransitionTheDocumentNonTerminalToProlog()
        {
            // Arrange
            Utf8XmlReader reader = new("<?xml version=\"1.0\"?>"u8);

            // Act
            reader.Read();

            // Assert
            Assert.Equal(EBNF.Document.Prolog, reader.ReaderState._documentNonTerminal);
        }

        [Fact]
        public void Read_ForADeclaration_ItShould_SetPreviousTokenTypeToNone()
        {
            // Arrange - Declaration can only ever be the very first token, so whatever came
            // "before" it, per the reader's own bookkeeping, has to be None.
            Utf8XmlReader reader = new("<?xml version=\"1.0\"?>"u8);

            // Act
            reader.Read();

            // Assert
            Assert.Equal(TokenType.None, reader.ReaderState._previousTokenType);
        }

        [Fact]
        public void Read_ForADeclaration_ItShould_AdvanceLinePositionByTheFullTokenLength()
        {
            // Arrange - no newline anywhere in this declaration, so _linePosition should end up
            // exactly at the token's total length: the "<?xml" TryMatch consumed *plus* everything
            // TrySkip consumed through "?>" - not just the TrySkip portion on its own.
            byte[] source = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?>");
            Utf8XmlReader reader = new(source);

            // Act
            reader.Read();

            // Assert
            Assert.Equal(source.Length, reader.ReaderState._linePosition);
        }

        [Fact]
        public void Read_ForATruncatedDeclarationOnTheFinalSegment_ItShould_Throw()
        {
            // Arrange - missing the closing "?>" terminal, and this is the only (final) segment,
            // so it can never be completed no matter how much more is waited for.
            Utf8XmlReader reader = new("<?xml version=\"1.0\""u8);

            // Act & Assert - reader is a ref struct, so it can't be captured by Assert.Throws'
            // lambda; a plain try/catch is the only option here.
            bool threw = false;
            try
            {
                reader.Read();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.True(threw);
        }

        [Theory]
        [InlineData("<!--a comment-->")]
        [InlineData("<!---->")]
        public void Read_ForAComment_ItShould_ReturnTrueAndYieldTheWholeRawCommentAsTheValue(string commentText)
        {
            // Arrange
            byte[] source = Encoding.UTF8.GetBytes(commentText);
            Utf8XmlReader reader = new(source);

            // Act
            bool readable = reader.Read();

            // Assert
            Assert.True(readable);
            Assert.Equal(TokenType.Comment, reader.TokenType);
            Assert.True(((ReadOnlySpan<byte>)source).SequenceEqual(reader.Value));
        }

        [Fact]
        public void Read_ForAProcessingInstruction_ItShould_ReturnTrueAndYieldTheWholeRawPIAsTheValue()
        {
            // Arrange - a target that has nothing to do with "xml"
            byte[] source = Encoding.UTF8.GetBytes("<?target data?>");
            Utf8XmlReader reader = new(source);

            // Act
            bool readable = reader.Read();

            // Assert
            Assert.True(readable);
            Assert.Equal(TokenType.ProcessInstruction, reader.TokenType);
            Assert.True(((ReadOnlySpan<byte>)source).SequenceEqual(reader.Value));
        }

        [Fact]
        public void Read_ForAnXmlStylesheetProcessingInstruction_ItShould_NotBeMisreadAsADeclaration()
        {
            // Arrange - "<?xml-stylesheet ...?>" is a real, common PI (the standard way to
            // associate an XSLT stylesheet). Its target merely *starts with* "xml" - only the
            // exact target "xml" (case-insensitive) is reserved for the declaration, per spec.
            // A 5-byte prefix match against "<?xml" alone can't tell these apart.
            byte[] source = Encoding.UTF8.GetBytes("<?xml-stylesheet type=\"text/xsl\" href=\"style.xsl\"?>");
            Utf8XmlReader reader = new(source);

            // Act
            bool readable = reader.Read();

            // Assert
            Assert.True(readable);
            Assert.Equal(TokenType.ProcessInstruction, reader.TokenType);
        }

        [Fact]
        public void Read_ForLeadingSpacingBeforeAnElement_ItShould_ProgressThroughToElementPhaseWithinOneCall()
        {
            // Arrange - no declaration, just leading whitespace before the root element. Without
            // ReadDocument's loop also watching segment position (not just phase - spacing
            // consumes bytes but produces no token and doesn't change _documentNonTerminal), this
            // would look identical to "final segment, nothing ever read" and throw before ever
            // recognizing the element start sitting right after the spaces.
            Utf8XmlReader reader = new("   <Root/>"u8);

            // Act - Read() still throws here, but only because ReadMarkup() is an unbuilt stub
            // and genuinely can't produce the element token yet - a separate, expected gap, not
            // a regression. What this actually verifies is that _documentNonTerminal already
            // reached Element *before* that happens, proving spacing-then-element-detection ran
            // within this one call rather than needing an extra round-trip.
            try
            {
                reader.Read();
            }
            catch (InvalidOperationException)
            {
            }

            // Assert
            Assert.Equal(EBNF.Document.Element, reader.ReaderState._documentNonTerminal);
        }
    }
}
