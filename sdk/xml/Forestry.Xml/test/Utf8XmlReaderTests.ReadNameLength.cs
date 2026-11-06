using System.Text;
using Xunit;
using Forestry.Xml.Reading;

namespace Forestry.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Reader spec 001, User Story 2: names compared in full (tasks.md T028-T036, T038, #66)
        //
        // Test shells written before the code (C4), reviewed at T039. Every document is read whole
        // and cut up with `ReadWhole`, `ReadEverySplit` and `ReadInParts` (T003); a malformed one
        // with `ReadThrown`, `ReadThrownEverySplit` and `ReadThrownInParts`, through the US1
        // helpers `AssertTokensEveryWay` and `AssertThrownEveryWay`. New categories and markups
        // are compared as strings ("Limit", "ElementStack").
        //
        // These shells don't compile until T040 declares `ReaderOptions.MaxNameLength` and
        // `ElementStack.NameBytesCapacity`. Positions are 1-based (READER.md rule 17).

        #region helpers
        private const string LimitCategory = "Limit";

        /// <summary>
        /// An ASCII name of <paramref name="length"/> bytes, starting with <c>N</c>
        /// </summary>
        private static string NameOf(int length) => "N" + new string('a', length - 1);
        #endregion

        #region T028-T030 end tags compared in full
        /// <summary>
        /// T028, US2-1 / SC-004: StanForD's 35-byte name reads with its full name
        /// </summary>
        [Fact]
        public void Read_ForAThirtyFiveByteName_ItShould_GiveTheFullName()
        {
            // Arrange, Act, Assert
            AssertTokensEveryWay(
                "<ButtEndProfileExtrapolationFunction>1</ButtEndProfileExtrapolationFunction>",
                Plain(TokenType.Element, "ButtEndProfileExtrapolationFunction"),
                Plain(TokenType.Value, "1"),
                Plain(TokenType.ElementEnd, "ButtEndProfileExtrapolationFunction"));
        }

        /// <summary>
        /// T029, US2-2 / FR-008: names equal on their first 32 bytes and different after are a
        /// mismatch, at the end tag's name start
        /// </summary>
        [Fact]
        public void Read_ForNamesEqualOnThirtyTwoBytes_ItShould_ThrowAMismatch()
        {
            // Arrange, Act, Assert
            AssertThrownEveryWay(
                "<ButtEndProfileExtrapolationFunctionX></ButtEndProfileExtrapolationFunctionY>",
                "Name", "EndTag", lineNumber: 1, linePosition: 41);
        }

        /// <summary>
        /// T030, US2-3 / FR-008: an end tag that is a prefix or an extension of the start tag's
        /// name is a mismatch
        /// </summary>
        [Theory]
        [InlineData("<abc></ab>", 8)]
        [InlineData("<ab></abc>", 7)]
        public void Read_ForAnEndTagPrefixOrExtension_ItShould_ThrowAMismatch(string document, long linePosition)
        {
            // Arrange, Act, Assert
            AssertThrownEveryWay(document, "Name", "EndTag", lineNumber: 1, linePosition);
        }
        #endregion

        #region T031-T032 names bounded by MaxNameLength
        /// <summary>
        /// T031, US2-4 / FR-010: element and attribute names up to the default 128 bytes read
        /// </summary>
        [Theory]
        [InlineData(32)]
        [InlineData(33)]
        [InlineData(128)]
        public void Read_ForNamesUpToTheLimit_ItShould_Read(int length)
        {
            // Arrange
            string name = NameOf(length);

            // Act, Assert
            AssertTokensEveryWay(
                $"<{name}/>",
                Plain(TokenType.Element, name),
                Plain(TokenType.ElementEnd, ""));

            AssertTokensEveryWay(
                $"<a {name}=\"1\"/>",
                Plain(TokenType.Element, "a"),
                Plain(TokenType.Attribute, name),
                Plain(TokenType.Value, "1"),
                Plain(TokenType.ElementEnd, ""));
        }

        /// <summary>
        /// T031, US2-4 / FR-010 / S1: a 129-byte name throws at its byte at index 128
        /// </summary>
        [Fact]
        public void Read_ForANameOneByteBeyondTheLimit_ItShould_ThrowLimit()
        {
            // Arrange
            string name = NameOf(129);

            // Act, Assert
            AssertThrownEveryWay($"<{name}/>", LimitCategory, "Name", lineNumber: 1, linePosition: 130);
            AssertThrownEveryWay($"<a {name}=\"1\"/>", LimitCategory, "Name", lineNumber: 1, linePosition: 132);
        }

        /// <summary>
        /// T031, S1: the limit throws before more of the name is buffered, even when reading is
        /// not completed and the name could still go on
        /// </summary>
        [Fact]
        public void Read_ForANameBeyondTheLimitWithReadingNotCompleted_ItShould_ThrowRatherThanHalt()
        {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes("<" + NameOf(130)), isReadingCompleted: false, new ReaderState());

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            XmlException exception = Assert.IsType<XmlException>(thrown);
            Assert.Equal(LimitCategory, exception.Category);
            Assert.Equal("Name", exception.Markup);
            Assert.Equal(130, exception.LinePosition);
        }

        /// <summary>
        /// T032, US2-5 / FR-009: the default reads 128 bytes; an override of 16 throws on 17
        /// bytes, also once the option has passed through <see cref="ReaderState"/> (in parts)
        /// </summary>
        [Fact]
        public void Read_ForAnOverriddenMaxNameLength_ItShould_BoundNamesByIt()
        {
            // Arrange
            ReaderOptions sixteen = new() { MaxNameLength = 16 };

            // Act, Assert
            AssertTokensEveryWay(
                $"<{NameOf(128)}/>",
                default(ReaderOptions),
                Plain(TokenType.Element, NameOf(128)),
                Plain(TokenType.ElementEnd, ""));

            AssertThrownEveryWay($"<{NameOf(17)}/>", LimitCategory, "Name", lineNumber: 1, linePosition: 18, options: sixteen);
        }

        /// <summary>
        /// T032, FR-009: an explicit 0, a negative value or more than the element stack's name
        /// bytes is rejected when set (an unset option gets the default)
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(ElementStack.NameBytesCapacity + 1)]
        public void MaxNameLength_ForAValueOutOfRange_ItShould_Throw(int value)
        {
            // Act, Assert
            Assert.Throws<InvalidOperationException>(() => new ReaderOptions { MaxNameLength = value });
        }
        #endregion

        #region T034-T035 end tags and long names cut up
        /// <summary>
        /// T034, US2-6 / FR-011: an end tag longer than any start tag name is a mismatch, never a
        /// limit
        /// </summary>
        [Fact]
        public void Read_ForAnEndTagBeyondTheLimit_ItShould_ThrowAMismatch()
        {
            // Arrange, Act, Assert
            AssertThrownEveryWay($"<abc></{NameOf(129)}>", "Name", "EndTag", lineNumber: 1, linePosition: 8);
        }

        /// <summary>
        /// T034, FR-011: the end tag is evaluated only to the start-tag name's length plus one
        /// byte, so it throws the mismatch rather than halting for more of the name
        /// </summary>
        [Fact]
        public void Read_ForAnEndTagLongerThanItsStartTagWithReadingNotCompleted_ItShould_ThrowRatherThanHalt()
        {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes("<abc></abcd"), isReadingCompleted: false, new ReaderState());
            Assert.True(reader.Read());

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            XmlException exception = Assert.IsType<XmlException>(thrown);
            Assert.Equal("Name", exception.Category);
            Assert.Equal("EndTag", exception.Markup);
            Assert.Equal(8, exception.LinePosition);
        }

        /// <summary>
        /// T035, US2-7 / C1: long element and attribute names give the whole document's tokens
        /// however they are split
        /// </summary>
        [Fact]
        public void Read_ForLongNamesCutUp_ItShould_GiveTheSameTokens()
        {
            // Arrange
            string element = NameOf(35);
            string child = NameOf(128);
            string attribute = "A" + new string('b', 127);

            // Act, Assert
            AssertTokensEveryWay(
                $"<{element} {attribute}=\"1\"><{child}/></{element}>",
                Plain(TokenType.Element, element),
                Plain(TokenType.Attribute, attribute),
                Plain(TokenType.Value, "1"),
                Plain(TokenType.Element, child),
                Plain(TokenType.ElementEnd, ""),
                Plain(TokenType.ElementEnd, element));
        }
        #endregion

        #region T036 other names
        /// <summary>
        /// T036, FR-010: a processing instruction target is bounded too, checked once the
        /// instruction is delimited (after <c>?&gt;</c> is found)
        /// </summary>
        [Fact]
        public void Read_ForAProcessingInstructionTargetBeyondTheLimit_ItShould_ThrowLimit()
        {
            // Arrange
            ReaderOptions sixteen = new() { MaxNameLength = 16 };

            // Act, Assert
            AssertThrownEveryWay($"<?{NameOf(17)}?><a/>", LimitCategory, "ProcessingInstructionTarget", lineNumber: 1, linePosition: 19, options: sixteen);
        }

        /// <summary>
        /// T036, FR-010: after a document type an entity name is bounded, at the first byte
        /// beyond the limit
        /// </summary>
        [Fact]
        public void Read_ForAnEntityNameBeyondTheLimitAfterADocumentType_ItShould_ThrowLimit()
        {
            // Arrange
            ReaderOptions sixteen = new() { MaxNameLength = 16 };

            // Act, Assert
            AssertThrownEveryWay($"<!DOCTYPE a SYSTEM \"a.dtd\"><a>&{NameOf(17)};</a>", LimitCategory, EntityReferenceMarkup, lineNumber: 1, linePosition: 48, options: sixteen);
        }
        #endregion

        #region T038 the element stack's limits
        /// <summary>
        /// T038, R4: open names beyond the stack's 2048 bytes throw "not supported" at the start
        /// tag that does not fit, nothing pushed: 17 names of 120 bytes use 2040 bytes, the 18th
        /// does not fit
        /// </summary>
        [Fact]
        public void Read_ForOpenNamesBeyondTheStacksBytes_ItShould_ThrowNotSupported()
        {
            // Arrange
            string tag = $"<{NameOf(120)}>";
            string document = string.Concat(Enumerable.Repeat(tag, 20));

            // Act, Assert
            AssertThrownEveryWay(document, NotSupportedCategory, "ElementStack", lineNumber: 1, linePosition: 17 * tag.Length + 2);
            AssertThrowsCommittingNothing(document, valueStart: 17 * tag.Length, expectedDepth: 17);
        }

        /// <summary>
        /// T038, S1: with the default <c>MaxDepth</c> of 64 the 65th open element throws a limit
        /// at its start tag, nothing pushed (today the <c>PushAllocating</c>
        /// <c>NotImplementedException</c>)
        /// </summary>
        [Fact]
        public void Read_ForASixtyFifthOpenElement_ItShould_ThrowLimit()
        {
            // Arrange
            string document = string.Concat(Enumerable.Repeat("<a>", 65));

            // Act, Assert
            AssertThrownEveryWay(document, LimitCategory, "Element", lineNumber: 1, linePosition: 64 * 3 + 2);
            AssertThrowsCommittingNothing(document, valueStart: 64 * 3, expectedDepth: 64);
        }
        #endregion
    }
}
