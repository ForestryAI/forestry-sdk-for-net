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
    }
}
