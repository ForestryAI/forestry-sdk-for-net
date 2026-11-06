using System.Text;
using Xunit;

namespace Forestry.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Reader spec 001, Polish: SC-004 end to end (tasks.md T068, #67) ----------------------
        //
        // Test shell for the developer's review (C4). A hand-written StanForD-shaped document with
        // all three stories in it: a UTF-8 byte order mark and StanForD's lowercase declaration
        // (US3), the 35-byte `ButtEndProfileExtrapolationFunction` (US2), and `&amp;` in a text
        // field (US1). Read to the end with default options, whole and cut up with `ReadWhole`,
        // `ReadEverySplit` and `ReadInParts` (T003). It passes with no code change.

        /// <summary>
        /// T068, SC-004: a StanForD-shaped document with a UTF-8 BOM, a 35-byte name and a
        /// reference reads to the end with default options, however it is split
        /// </summary>
        [Fact]
        public void Read_ForAStanForDShapedDocument_ItShould_ReadToTheEnd()
        {
            // Arrange
            byte[] document = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<HarvestedProduction xmlns=\"urn:skogforsk:stanford2010\">\n" +
                "  <Machine>\n" +
                "    <MachineKey>1</MachineKey>\n" +
                "    <OperatorComment>Spruce &amp; pine</OperatorComment>\n" +
                "  </Machine>\n" +
                "  <ButtEndProfileExtrapolationFunction>2</ButtEndProfileExtrapolationFunction>\n" +
                "</HarvestedProduction>\n")];

            // Act
            List<ReadToken> tokens = ReadEverySplit(document);
            ReadInParts(document);

            // Assert
            Assert.Equal(
                new[]
                {
                    Plain(TokenType.Declaration, "<?xml version=\"1.0\" encoding=\"utf-8\"?>"),
                    Plain(TokenType.Element, "HarvestedProduction"),
                    Plain(TokenType.Attribute, "xmlns"),
                    Plain(TokenType.Value, "urn:skogforsk:stanford2010"),
                    Plain(TokenType.Element, "Machine"),
                    Plain(TokenType.Element, "MachineKey"),
                    Plain(TokenType.Value, "1"),
                    Plain(TokenType.ElementEnd, "MachineKey"),
                    Plain(TokenType.Element, "OperatorComment"),
                    Escaped("Spruce &amp; pine"),
                    Plain(TokenType.ElementEnd, "OperatorComment"),
                    Plain(TokenType.ElementEnd, "Machine"),
                    Plain(TokenType.Element, "ButtEndProfileExtrapolationFunction"),
                    Plain(TokenType.Value, "2"),
                    Plain(TokenType.ElementEnd, "ButtEndProfileExtrapolationFunction"),
                    Plain(TokenType.ElementEnd, "HarvestedProduction"),
                },
                tokens);
        }
    }
}
