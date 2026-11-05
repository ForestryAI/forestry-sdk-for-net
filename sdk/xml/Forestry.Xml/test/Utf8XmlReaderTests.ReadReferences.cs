using System.Text;
using Xunit;
using Forestry.Xml.Reading;

namespace Forestry.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Reader spec 001, User Story 1: references (tasks.md T005-T013, #65) -----------------
        //
        // Test shells written before the code (C4), reviewed at T017. Every document is read whole
        // and cut up with `ReadWhole`, `ReadEverySplit` and `ReadInParts` (T004); a malformed one
        // with `ReadThrown`, `ReadThrownEverySplit` and `ReadThrownInParts`. Exceptions are
        // asserted by category, markup, line and position, never by message (#49). New
        // categories and markups are compared as strings ("NotSupported", "Reference",
        // "EntityReference") so the shells compile before `Throwing` has them.
        //
        // These shells don't compile until T018 declares `Utf8XmlReader.HasEscapedValue`.
        // The full malformed-reference table is on the constraint itself, T011
        // (`ReferenceConstraintTests.cs`); here only what the reader shows: tokens, the flag on
        // the token, line and position, and nothing committed.

        #region helpers
        private const string ReferenceMarkup = "Reference";

        private const string EntityReferenceMarkup = "EntityReference";

        private const string NotSupportedCategory = "NotSupported";

        /// <summary>
        /// T015 (R6): a non-ASCII name byte throws an XML exception "not supported" for the name
        /// </summary>
        private static void AssertNotSupportedName(Exception? thrown)
        {
            XmlException exception = Assert.IsType<XmlException>(thrown);
            Assert.Equal(NotSupportedCategory, exception.Category);
            Assert.Equal("Name", exception.Markup);
        }

        private static ReadToken Escaped(string value) => new(TokenType.Value, value, HasEscapedValue: true);

        private static ReadToken Plain(TokenType type, string value) => new(type, value, HasEscapedValue: false);

        /// <summary>
        /// Read the document whole, split at every byte and in parts, asserting the three agree,
        /// and assert the tokens
        /// </summary>
        private static void AssertTokensEveryWay(string document, params ReadToken[] expected) =>
            AssertTokensEveryWay(document, default, expected);

        /// <summary>
        /// As <see cref="AssertTokensEveryWay(string, ReadToken[])"/>, with reader options (US2)
        /// </summary>
        private static void AssertTokensEveryWay(string document, ReaderOptions options, params ReadToken[] expected)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(document);

            List<ReadToken> tokens = ReadEverySplit(bytes, options);
            ReadInParts(bytes, options);

            Assert.Equal(expected, tokens);
        }

        /// <summary>
        /// Read the document whole, split at every byte and in parts, asserting each throws the
        /// same exception, and assert it
        /// </summary>
        private static void AssertThrownEveryWay(string document, string category, string markup, long lineNumber, long linePosition, ReaderOptions options = default)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(document);

            XmlException thrown = ReadThrownEverySplit(bytes, options);
            ReadThrownInParts(bytes, options);

            Assert.Equal(category, thrown.Category);
            Assert.Equal(markup, thrown.Markup);
            Assert.Equal(lineNumber, thrown.LineNumber);
            Assert.Equal(linePosition, thrown.LinePosition);
        }

        /// <summary>
        /// Read the document as one span; at the read that throws, assert the token is the one
        /// before that read and the reader is at <paramref name="valueStart"/>, the start of the
        /// markup that throws: nothing of it is committed. Steps earlier in the same cycle
        /// (skipping <c>=</c> or a start tag's <c>&gt;</c>) still commit, as in T009.
        /// </summary>
        private static void AssertThrowsCommittingNothing(string document, long valueStart, int expectedDepth = -1, ReaderOptions options = default)
        {
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(document), isReadingCompleted: true, new ReaderState(options));

            while (true)
            {
                TokenType tokenType = reader.TokenType;

                bool advancement;
                try
                {
                    advancement = reader.Read();
                }
                catch (XmlException)
                {
                    Assert.Equal(valueStart, reader.Position);
                    Assert.Equal(tokenType, reader.TokenType);
                    if (expectedDepth >= 0)
                    {
                        Assert.Equal(expectedDepth, reader.Depth);  // US2: the failing start tag pushed nothing
                    }
                    return;
                }

                Assert.True(advancement, "The document read to its end without throwing");
            }
        }
        #endregion

        #region T005-T008 references read and flagged
        /// <summary>
        /// T005, US1-1 / FR-001 / FR-002: text and a reference are one raw Value, flagged
        /// </summary>
        [Fact]
        public void Read_ForAReferenceInText_ItShould_GiveOneEscapedValue()
        {
            // Arrange, Act, Assert
            AssertTokensEveryWay(
                "<a>Spruce &amp; pine</a>",
                Plain(TokenType.Element, "a"),
                Escaped("Spruce &amp; pine"),
                Plain(TokenType.ElementEnd, "a"));
        }

        /// <summary>
        /// T006, US1-2 / FR-002: text without a reference is not flagged, and the flag lives for
        /// one <c>Read()</c>: only <c>b</c>'s Value is flagged
        /// </summary>
        [Fact]
        public void Read_ForTextWithoutReferences_ItShould_ClearTheFlag()
        {
            // Arrange, Act, Assert
            AssertTokensEveryWay(
                "<a>Spruce and pine</a>",
                Plain(TokenType.Element, "a"),
                Plain(TokenType.Value, "Spruce and pine"),
                Plain(TokenType.ElementEnd, "a"));

            AssertTokensEveryWay(
                "<a><b>&amp;</b><c>x</c></a>",
                Plain(TokenType.Element, "a"),
                Plain(TokenType.Element, "b"),
                Escaped("&amp;"),
                Plain(TokenType.ElementEnd, "b"),
                Plain(TokenType.Element, "c"),
                Plain(TokenType.Value, "x"),
                Plain(TokenType.ElementEnd, "c"),
                Plain(TokenType.ElementEnd, "a"));
        }

        /// <summary>
        /// T007, US1-3 / FR-001: references in an attribute value, in either quote
        /// </summary>
        [Fact]
        public void Read_ForReferencesInAnAttributeValue_ItShould_GiveOneEscapedValue()
        {
            // Arrange, Act, Assert
            AssertTokensEveryWay(
                "<a b=\"&lt;&#60;&#x3C;\"/>",
                Plain(TokenType.Element, "a"),
                Plain(TokenType.Attribute, "b"),
                Escaped("&lt;&#60;&#x3C;"),
                Plain(TokenType.ElementEnd, ""));

            AssertTokensEveryWay(
                "<a b='x &quot; y'/>",
                Plain(TokenType.Element, "a"),
                Plain(TokenType.Attribute, "b"),
                Escaped("x &quot; y"),
                Plain(TokenType.ElementEnd, ""));
        }

        /// <summary>
        /// T008, US1-4: each reference alone, as an element's whole text and as an attribute's
        /// whole value
        /// </summary>
        [Theory]
        [InlineData("&amp;")]
        [InlineData("&lt;")]
        [InlineData("&gt;")]
        [InlineData("&quot;")]
        [InlineData("&apos;")]
        [InlineData("&#65;")]
        [InlineData("&#x41;")]
        [InlineData("&#x1F332;")]
        public void Read_ForAReferenceAlone_ItShould_GiveOneEscapedValue(string reference)
        {
            // Arrange, Act, Assert
            AssertTokensEveryWay(
                $"<a>{reference}</a>",
                Plain(TokenType.Element, "a"),
                Escaped(reference),
                Plain(TokenType.ElementEnd, "a"));

            AssertTokensEveryWay(
                $"<a b=\"{reference}\"/>",
                Plain(TokenType.Element, "a"),
                Plain(TokenType.Attribute, "b"),
                Escaped(reference),
                Plain(TokenType.ElementEnd, ""));
        }
        #endregion

        #region T009 a segment ending inside a reference
        /// <summary>
        /// T009, US1-5 / FR-004 / C1: a segment ending inside a reference halts with nothing of
        /// the Value committed; a new reader over both segments continues to the whole
        /// document's tokens. <c>&gt;</c> is skipped by content ready (step 4) in the halting
        /// cycle, so the committed position is 3, after <c>&lt;a&gt;</c>.
        /// </summary>
        [Fact]
        public void Read_ForASegmentEndingInsideAReference_ItShould_HaltThenContinue()
        {
            // Arrange
            byte[] first = Encoding.UTF8.GetBytes("<a>x &am");
            byte[] whole = Encoding.UTF8.GetBytes("<a>x &amp; y</a>");
            Utf8XmlReader reader = new(first, isReadingCompleted: false, new ReaderState());
            Assert.True(reader.Read());

            // Act
            bool advancement = reader.Read();

            // Assert
            Assert.False(advancement);
            Assert.Equal(TokenType.Element, reader.TokenType);
            Assert.Equal(1, reader.Depth);
            Assert.Equal(3, reader.Position);
            Assert.Equal(0, reader.ReaderState._lineNumber);
            Assert.Equal(3, reader.ReaderState._linePosition);

            List<ReadToken> rest = [];
            Utf8XmlReader next = new(SequenceSplitAt(whole[(int)reader.Position..], first.Length - (int)reader.Position), isReadingCompleted: true, reader.ReaderState);
            ReadAll(ref next, rest);
            Assert.Equal(new List<ReadToken> { Escaped("x &amp; y"), Plain(TokenType.ElementEnd, "a") }, rest);
        }

        /// <summary>
        /// T009: every split inside a character reference in an attribute value gives the whole
        /// document's tokens
        /// </summary>
        [Fact]
        public void Read_ForEverySplitInsideACharacterReference_ItShould_GiveTheSameTokens()
        {
            // Arrange, Act, Assert
            AssertTokensEveryWay(
                "<a b=\"&#x1F332;\"/>",
                Plain(TokenType.Element, "a"),
                Plain(TokenType.Attribute, "b"),
                Escaped("&#x1F332;"),
                Plain(TokenType.ElementEnd, ""));
        }
        #endregion

        #region T010 malformed references, reader level
        /// <summary>
        /// T010, US1-6 / FR-005 / S0: representative malformed references in content, at the
        /// first byte where they are known (1-based), the same whole, split and in parts
        /// </summary>
        [Theory]
        [InlineData("<a>x & y</a>", "Expected", 7)]          // bare '&': at the space
        [InlineData("<a>&nbsp;</a>", "Name", 5)]             // not one of the five, no document type: at 'n'
        [InlineData("<a>&#x110000;</a>", "Expected", 12)]    // above U+10FFFF: at the last '0'
        [InlineData("<a>&#0;</a>", "Expected", 7)]           // not a legal Char: at ';'
        public void Read_ForAMalformedReferenceInContent_ItShould_ThrowAtTheFirstKnownByte(string document, string category, long linePosition)
        {
            // Arrange, Act, Assert
            AssertThrownEveryWay(document, category, ReferenceMarkup, lineNumber: 1, linePosition);
            AssertThrowsCommittingNothing(document, valueStart: 3);  // the text, after <a>
        }

        /// <summary>
        /// T010: the same references in an attribute value
        /// </summary>
        [Theory]
        [InlineData("<a b=\"x & y\"/>", "Expected", 10)]
        [InlineData("<a b=\"&nbsp;\"/>", "Name", 8)]
        [InlineData("<a b=\"&#x110000;\"/>", "Expected", 15)]
        [InlineData("<a b=\"&#0;\"/>", "Expected", 10)]
        public void Read_ForAMalformedReferenceInAnAttributeValue_ItShould_ThrowAtTheFirstKnownByte(string document, string category, long linePosition)
        {
            // Arrange, Act, Assert
            AssertThrownEveryWay(document, category, ReferenceMarkup, lineNumber: 1, linePosition);
            AssertThrowsCommittingNothing(document, valueStart: 5);  // the starting quote, after b=
        }

        /// <summary>
        /// T010: a malformed reference on line 2, so line and position walk across the line feed
        /// </summary>
        [Fact]
        public void Read_ForAMalformedReferenceOnLineTwo_ItShould_ReportLineTwo()
        {
            // Arrange, Act, Assert
            AssertThrownEveryWay("<a>\n&nbsp;</a>", "Name", ReferenceMarkup, lineNumber: 2, linePosition: 2);
        }
        #endregion

        #region T012 entities with a document type
        /// <summary>
        /// T012, US1-7 / FR-006 / S2: after a document type an entity other than the five may be
        /// declared, but the reader never applies it: "not supported" at the <c>&amp;</c>
        /// </summary>
        [Fact]
        public void Read_ForAnEntityAfterADocumentType_ItShould_ThrowNotSupported()
        {
            // Arrange, Act, Assert
            AssertThrownEveryWay("<!DOCTYPE a SYSTEM \"a.dtd\"><a>&e;</a>", NotSupportedCategory, EntityReferenceMarkup, lineNumber: 1, linePosition: 31);
        }

        /// <summary>
        /// T012: a missing <c>;</c> is malformed with or without a document type
        /// </summary>
        [Fact]
        public void Read_ForAnEntityMissingItsSemicolonAfterADocumentType_ItShould_ThrowMalformed()
        {
            // Arrange, Act, Assert
            AssertThrownEveryWay("<!DOCTYPE a SYSTEM \"a.dtd\"><a>&e</a>", "Expected", ReferenceMarkup, lineNumber: 1, linePosition: 33);
        }

        /// <summary>
        /// T012, US1-8 / FR-006 / C0 (option A, developer 2026-11-05): an internal subset is an
        /// opaque inside the opaque document type, delimited by its own <c>[</c> and <c>]</c>;
        /// until that is built it throws "not supported" at the <c>[</c>, nothing committed
        /// </summary>
        [Fact]
        public void Read_ForADocumentTypeWithAnInternalSubset_ItShould_ThrowNotSupported()
        {
            // Arrange
            const string document = "<!DOCTYPE a [<!ENTITY e \"x\">]><a/>";

            // Act, Assert
            AssertThrownEveryWay(document, NotSupportedCategory, "DocumentType", lineNumber: 1, linePosition: 13);
            AssertThrowsCommittingNothing(document, valueStart: 0);
        }

        /// <summary>
        /// T012, US1-8: a <c>[</c> inside a quoted literal of the document type is not an
        /// internal subset
        /// </summary>
        [Fact]
        public void Read_ForADocumentTypeWithABracketInALiteral_ItShould_Read()
        {
            // Arrange, Act, Assert
            AssertTokensEveryWay(
                "<!DOCTYPE a SYSTEM \"a[1].dtd\"><a/>",
                Plain(TokenType.DocumentType, "<!DOCTYPE a SYSTEM \"a[1].dtd\">"),
                Plain(TokenType.Element, "a"),
                Plain(TokenType.ElementEnd, ""));
        }
        #endregion

        #region T013 edge cases
        /// <summary>
        /// T013, spec edge case: spacing next to a reference is text, not spacing-only character
        /// data (READER.md rule 18)
        /// </summary>
        [Fact]
        public void Read_ForSpacingNextToAReference_ItShould_KeepTheSpacing()
        {
            // Arrange, Act, Assert
            AssertTokensEveryWay(
                "<a> &amp; </a>",
                Plain(TokenType.Element, "a"),
                Escaped(" &amp; "),
                Plain(TokenType.ElementEnd, "a"));

            AssertTokensEveryWay(
                "<a>\n  &amp;<b/></a>",
                Plain(TokenType.Element, "a"),
                Escaped("\n  &amp;"),
                Plain(TokenType.Element, "b"),
                Plain(TokenType.ElementEnd, ""),
                Plain(TokenType.ElementEnd, "a"));
        }

        /// <summary>
        /// T013: one leading-zeros row at the reader; the rest are on the constraint (T011)
        /// </summary>
        [Fact]
        public void Read_ForACharacterReferenceWithLeadingZeros_ItShould_GiveOneEscapedValue()
        {
            // Arrange, Act, Assert
            AssertTokensEveryWay(
                "<a>&#x0000000041;</a>",
                Plain(TokenType.Element, "a"),
                Escaped("&#x0000000041;"),
                Plain(TokenType.ElementEnd, "a"));
        }

        /// <summary>
        /// T013: in an attribute value <c>&gt;</c> and the other quote stay plain bytes next to a
        /// reference, and <c>&lt;</c> still throws
        /// </summary>
        [Fact]
        public void Read_ForAnAttributeValueWithAReferenceAndPlainBytes_ItShould_KeepThePlainBytes()
        {
            // Arrange, Act, Assert
            AssertTokensEveryWay(
                "<a b=\"&lt;>'\"/>",
                Plain(TokenType.Element, "a"),
                Plain(TokenType.Attribute, "b"),
                Escaped("&lt;>'"),
                Plain(TokenType.ElementEnd, ""));

            AssertThrownEveryWay("<a b=\"x<y\"/>", "Expected", "AttributeValue", lineNumber: 1, linePosition: 8);
        }

        /// <summary>
        /// T013: a reference running to the end of the data with reading completed is incomplete
        /// </summary>
        [Theory]
        [InlineData("<a>&amp", "CharacterData", 8)]
        [InlineData("<a b=\"&amp", "AttributeValue", 11)]
        public void Read_ForAReferenceAtTheEndOfTheData_ItShould_ThrowIncomplete(string document, string markup, long linePosition)
        {
            // Arrange, Act, Assert
            AssertThrownEveryWay(document, "Incomplete", markup, lineNumber: 1, linePosition);
        }
        #endregion
    }
}
