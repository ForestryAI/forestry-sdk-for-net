using Xunit;

namespace Forestry.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Reader spec 001, User Story 3: the declaration's encoding (tasks.md T055, #67) -----
        //
        // Test shells written before the code (C4), reviewed at T059: only what the reader shows
        // (the token, line and position, nothing committed, read whole and cut up). The encoding
        // names and shapes are on the constraint itself, T056 (`DeclarationEncodingConstraintTests.cs`).

        /// <summary>
        /// T055, US3-5 / FR-014: a declaration naming UTF-8, or no encoding, is read as one
        /// Declaration token, its value the raw declaration
        /// </summary>
        [Theory]
        [InlineData("<?xml version=\"1.0\" encoding=\"UTF-8\"?>")]
        [InlineData("<?xml version=\"1.0\" encoding=\"utf-8\"?>")]   // StanForD's
        [InlineData("<?xml version=\"1.0\"?>")]
        public void Read_ForADeclarationInUtf8_ItShould_ReadTheRawDeclaration(string declaration)
        {
            // Arrange, Act, Assert
            AssertTokensEveryWay(
                declaration + "<a/>",
                Plain(TokenType.Declaration, declaration),
                Plain(TokenType.Element, "a"),
                Plain(TokenType.ElementEnd, ""));
        }

        /// <summary>
        /// T055, US3-5 / FR-014: a declaration naming another encoding throws "not supported" at
        /// the encoding name's first byte, nothing committed, however it is split
        /// </summary>
        [Fact]
        public void Read_ForADeclarationInAnotherEncoding_ItShould_ThrowNotSupported()
        {
            // Arrange
            const string document = "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><a/>";

            // Act, Assert
            AssertThrownEveryWay(document, NotSupportedCategory, "Encoding", lineNumber: 1, linePosition: 31);
            AssertThrowsCommittingNothing(document, valueStart: 0, expectedDepth: 0);
        }
    }
}
