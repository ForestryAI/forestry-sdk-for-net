using System.Reflection;
using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Exception formatting shells, from #42 -------------------------------------------
        //
        // The `Throwing_*` tests below are shells written from #42's architecture (exception
        // policies: category (why) + markup (what) as fields and in the message, one message
        // template per category, 1-based line number and line position) before it exists - see
        // doc/dev/Velocity.md's Test shell phase.
        //
        // Two groups:
        // - formatting: GetXmlException called directly on a reader at a known line number and
        //   line position (fields, 1-based position, message templates, Path removed)
        // - mapping: each current throw site, through its step, asserting the category and
        //   markup from #42's mapping table
        //
        // Positions are the reader's committed position + 1 (#42). Which byte the position points
        // at is #43's, so the mapping shells don't assert positions.
        //
        // Test seam - PLACEHOLDER, not decided by the architecture beyond the names in #42, to be
        // confirmed or renamed during Understanding. Won't compile until it exists:
        // - internal enumerations `Throwing.Category` and `Throwing.Markup`
        // - `Throwing.GetXmlException(ref reader, Category, Markup, byte nextByte, ReadOnlySpan<byte> bytes)`
        // - `XmlException.Category` and `XmlException.Markup` as string properties, the
        //   enumeration value names
        //
        // Theories pass the category and markup by name (nameof): a public test method can't
        // take the internal enumerations as parameters.
        //
        // The markup's text in the message (enumeration name or terminal) isn't decided, so the
        // template shells only assert the fixed parts of each template.

        #region helpers
        /// <summary>
        /// The exception <see cref="Throwing.GetXmlException"/> returns for a reader committed at
        /// the given 0-based line number and line position
        /// </summary>
        private static XmlException GetXmlExceptionAt(
            long lineNumber,
            long linePosition,
            Throwing.Category category,
            Throwing.Markup markup,
            byte? foundByte = null,
            byte[]? bytes = null
        ) {
            Utf8XmlReader reader = new(""u8, isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None, lineNumber, linePosition));
            return Throwing.GetXmlException(ref reader, category, markup, foundByte, bytes);
        }

        /// <summary>
        /// Theories pass the category and markup by name, a public test method can't take the
        /// internal enumerations as parameters
        /// </summary>
        private static XmlException GetXmlExceptionAt(
            long lineNumber,
            long linePosition,
            string category,
            string markup,
            byte? foundByte = null,
            byte[]? bytes = null
        ) => GetXmlExceptionAt(lineNumber, linePosition, Enum.Parse<Throwing.Category>(category), Enum.Parse<Throwing.Markup>(markup), foundByte, bytes);

        /// <summary>
        /// The thrown exception is an XML exception with the category and markup
        /// </summary>
        private static void AssertXmlException(
            Exception? thrown,
            Throwing.Category category,
            Throwing.Markup markup
        ) {
            XmlException exception = Assert.IsType<XmlException>(thrown);
            Assert.Equal(category.ToString(), exception.Category);
            Assert.Equal(markup.ToString(), exception.Markup);
        }
        #endregion

        #region fields
        /// <summary>
        /// The category and markup are fields named after the enumeration values, so callers
        /// don't parse the message
        /// </summary>
        [Theory]
        [InlineData(nameof(Throwing.Category.Incomplete), nameof(Throwing.Markup.Element))]
        [InlineData(nameof(Throwing.Category.Expected), nameof(Throwing.Markup.AttributeValue))]
        [InlineData(nameof(Throwing.Category.Name), nameof(Throwing.Markup.Name))]
        [InlineData(nameof(Throwing.Category.Order), nameof(Throwing.Markup.Declaration))]
        public void Throwing_ForACategoryAndMarkup_ItShould_SetTheFields(
            string category,
            string markup
        ) {
            // Act
            XmlException exception = GetXmlExceptionAt(0, 0, category, markup);

            // Assert
            Assert.Equal(category, exception.Category);
            Assert.Equal(markup, exception.Markup);
        }

        /// <summary>
        /// Line number and line position are 1-based in the exception, the reader tracks them
        /// 0-based (READER.md rule 17)
        /// </summary>
        [Theory]
        [InlineData(0, 0, 1, 1)]
        [InlineData(0, 4, 1, 5)]
        [InlineData(2, 5, 3, 6)]
        public void Throwing_ForAReaderPosition_ItShould_ReportItOneBased(
            long lineNumber,
            long linePosition,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Act
            XmlException exception = GetXmlExceptionAt(lineNumber, linePosition, Throwing.Category.Expected, Throwing.Markup.Spacing);

            // Assert
            Assert.Equal(expectedLineNumber, exception.LineNumber);
            Assert.Equal(expectedLinePosition, exception.LinePosition);
            Assert.EndsWith($" at line {expectedLineNumber}, position {expectedLinePosition}.", exception.Message);
        }

        /// <summary>
        /// <c>Path</c> and the two constructors taking a path are removed until the element path
        /// is designed
        /// </summary>
        [Fact]
        public void Throwing_ForXmlException_ItShould_HaveNoPath()
        {
            // Act
            PropertyInfo? path = typeof(XmlException).GetProperty("Path");
            int pathConstructors = typeof(XmlException)
                .GetConstructors()
                .Count(constructor => constructor.GetParameters().Any(parameter => parameter.Name == "path"));

            // Assert
            Assert.Null(path);
            Assert.Equal(0, pathConstructors);
        }
        #endregion

        #region message
        /// <summary>
        /// Every category has a template starting with <c>Malformed markup:</c>
        /// </summary>
        [Theory]
        [MemberData(nameof(Categories))]
        public void Throwing_ForEveryCategory_ItShould_HaveAMalformedMarkupMessage(
            string category
        ) {
            // Act
            XmlException exception = GetXmlExceptionAt(0, 0, category, nameof(Throwing.Markup.Element));

            // Assert
            Assert.StartsWith("Malformed markup: ", exception.Message);
            Assert.DoesNotContain("{", exception.Message);
        }

        public static TheoryData<string> Categories()
        {
            TheoryData<string> data = [];
            foreach (string category in Enum.GetNames<Throwing.Category>())
            {
                data.Add(category);
            }

            return data;
        }

        /// <summary>
        /// One template per category, the fixed parts around the markup
        /// </summary>
        [Theory]
        [InlineData(nameof(Throwing.Category.Incomplete), "Malformed markup: reading completed inside ", "")]
        [InlineData(nameof(Throwing.Category.Expected), "Malformed markup: expected ", "")]
        [InlineData(nameof(Throwing.Category.Name), "Malformed markup: ", " breaks the naming rules")]
        [InlineData(nameof(Throwing.Category.Order), "Malformed markup: ", " is out of order")]
        [InlineData(nameof(Throwing.Category.Unique), "Malformed markup: ", " is repeated")]
        public void Throwing_ForACategory_ItShould_UseItsTemplate(
            string category,
            string beforeMarkup,
            string afterMarkup
        ) {
            // Act
            XmlException exception = GetXmlExceptionAt(0, 0, category, nameof(Throwing.Markup.Element));

            // Assert
            Assert.StartsWith(beforeMarkup, exception.Message);
            Assert.EndsWith($"{afterMarkup} at line 1, position 1.", exception.Message);
        }

        /// <summary>
        /// Expected adds the offending byte when the throw site passes it, a non-printable byte
        /// as <c>0x..</c>
        /// </summary>
        [Theory]
        [InlineData((byte)'a', " found a ")]
        [InlineData((byte)' ', " found   ")]
        [InlineData((byte)'\n', " found 0x0A ")]
        public void Throwing_ForExpectedWithAByte_ItShould_FormatTheFoundByte(
            byte found,
            string expected
        ) {
            // Act
            XmlException exception = GetXmlExceptionAt(0, 0, Throwing.Category.Expected, Throwing.Markup.EmptyElementEndingTerminal, found);

            // Assert
            Assert.Contains(expected, exception.Message);
        }

        /// <summary>
        /// Incomplete and Name add the bytes in parentheses right after the markup when the throw
        /// site passes them, e.g. the missing ending terminal or the malformed name
        /// </summary>
        [Theory]
        [InlineData(nameof(Throwing.Category.Incomplete), nameof(Throwing.Markup.EndingTerminal), "-->", " (-->) at line 1, position 1.")]
        [InlineData(nameof(Throwing.Category.Name), nameof(Throwing.Markup.Name), "1a", " (1a) breaks the naming rules at line 1, position 1.")]
        public void Throwing_ForIncompleteOrNameWithBytes_ItShould_FormatTheBytes(
            string category,
            string markup,
            string bytes,
            string expectedEnd
        ) {
            // Act
            XmlException exception = GetXmlExceptionAt(0, 0, category, markup, bytes: Encoding.UTF8.GetBytes(bytes));

            // Assert
            Assert.EndsWith(expectedEnd, exception.Message);
        }

        /// <summary>
        /// A template only formats an argument the throw site passes: Incomplete and Name without
        /// bytes have no parentheses
        /// </summary>
        [Theory]
        [InlineData(nameof(Throwing.Category.Incomplete), nameof(Throwing.Markup.Element))]
        [InlineData(nameof(Throwing.Category.Name), nameof(Throwing.Markup.Name))]
        public void Throwing_ForIncompleteOrNameWithoutBytes_ItShould_NotFormatBytes(
            string category,
            string markup
        ) {
            // Act
            XmlException exception = GetXmlExceptionAt(0, 0, category, markup);

            // Assert
            Assert.DoesNotContain("(", exception.Message);
        }

        /// <summary>
        /// A template only formats an argument the throw site passes: Expected without a byte has
        /// no <c>found</c>
        /// </summary>
        [Fact]
        public void Throwing_ForExpectedWithoutAByte_ItShould_NotFormatAFoundByte()
        {
            // Act
            XmlException exception = GetXmlExceptionAt(0, 0, Throwing.Category.Expected, Throwing.Markup.AttributeValue);

            // Assert
            Assert.DoesNotContain(" found", exception.Message);
        }
        #endregion

        #region mapping - Incomplete
        /// <summary>
        /// Last readable segment without a root element
        /// </summary>
        [Fact]
        public void Throwing_ForNoRootElement_ItShould_BeIncompleteRootElement()
        {
            // Arrange
            Utf8XmlReader reader = new("\n"u8, isReadingCompleted: true, ContextState(Context.Prolog, TokenType.None));

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            AssertXmlException(thrown, Throwing.Category.Incomplete, Throwing.Markup.RootElement);
        }

        /// <summary>
        /// Last readable segment with an element not ended
        /// </summary>
        [Fact]
        public void Throwing_ForElementNotEnded_ItShould_BeIncompleteElement()
        {
            // Arrange
            Utf8XmlReader reader = new(">"u8, isReadingCompleted: true, State(ElementStackAtDepth(1, contentReady: false), TokenType.Element));

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            AssertXmlException(thrown, Throwing.Category.Incomplete, Throwing.Markup.Element);
        }

        /// <summary>
        /// Opaque value S4 (#24): reading completed before the ending terminal
        /// </summary>
        [Theory]
        [InlineData("<!-- comment")]
        [InlineData("<?pi data?")]
        [InlineData("<!DOCTYPE root")]
        [InlineData("<?xml version=\"1.0\"")]
        public void Throwing_ForOpaqueValueEndingTerminalMissing_ItShould_BeIncompleteEndingTerminal(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertXmlException(thrown, Throwing.Category.Incomplete, Throwing.Markup.EndingTerminal);
        }

        /// <summary>
        /// Opaque value S4 straddling segments, the multiple segment throw site
        /// </summary>
        [Fact]
        public void Throwing_ForOpaqueValueEndingTerminalMissingStraddlingSegments_ItShould_BeIncompleteEndingTerminal()
        {
            // Arrange
            Utf8XmlReader reader = new(Sequence("<!-- com", "ment -"), isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertXmlException(thrown, Throwing.Category.Incomplete, Throwing.Markup.EndingTerminal);
        }

        /// <summary>
        /// Empty element end S3 (#35): <c>/</c> is the last byte and reading is completed
        /// </summary>
        [Fact]
        public void Throwing_ForSlashAtTheEndOfData_ItShould_BeIncompleteEndingTerminal()
        {
            // Arrange
            Utf8XmlReader reader = new("/"u8, isReadingCompleted: true, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadEmptyElementEndThrown(ref reader);

            // Assert
            AssertXmlException(thrown, Throwing.Category.Incomplete, Throwing.Markup.EndingTerminal);
        }
        #endregion

        #region mapping - Expected
        /// <summary>
        /// Empty element end S4 (#35): <c>/</c> not followed by <c>&gt;</c>, the found byte is
        /// in the message
        /// </summary>
        [Fact]
        public void Throwing_ForSlashNotFollowedByGreaterThan_ItShould_BeExpectedEmptyElementEndingTerminal()
        {
            // Arrange
            Utf8XmlReader reader = new("/a"u8, isReadingCompleted: true, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadEmptyElementEndThrown(ref reader);

            // Assert
            AssertXmlException(thrown, Throwing.Category.Expected, Throwing.Markup.EmptyElementEndingTerminal);
            Assert.Contains(" found a ", thrown!.Message);
        }

        /// <summary>
        /// Empty element end S5.a (#40): <c>&lt;a b/&gt;</c>
        /// </summary>
        [Fact]
        public void Throwing_ForAttributeNameBeforeEmptyElementEnd_ItShould_BeExpectedAttributeValue()
        {
            // Arrange
            Utf8XmlReader reader = new("/>"u8, isReadingCompleted: true, StartTagState(TokenType.Attribute, hasSkippedSpacingInStartTag: false));

            // Act
            Exception? thrown = ReadEmptyElementEndThrown(ref reader);

            // Assert
            AssertXmlException(thrown, Throwing.Category.Expected, Throwing.Markup.AttributeValue);
        }

        /// <summary>
        /// Content ready S4 (#40): <c>&lt;a b&gt;</c>
        /// </summary>
        [Fact]
        public void Throwing_ForAttributeNameBeforeContentReady_ItShould_BeExpectedAttributeValue()
        {
            // Arrange
            Utf8XmlReader reader = new(">"u8, isReadingCompleted: true, State(ElementStackAtDepth(1, contentReady: false), TokenType.Attribute));

            // Act
            Exception? thrown = ContentReadyThrown(ref reader);

            // Assert
            AssertXmlException(thrown, Throwing.Category.Expected, Throwing.Markup.AttributeValue);
        }

        /// <summary>
        /// Attribute delegate S0 (#40): no spacing before the attribute
        /// </summary>
        [Fact]
        public void Throwing_ForNoSpacingBeforeAttribute_ItShould_BeExpectedSpacing()
        {
            // Arrange
            Utf8XmlReader reader = new("c=\"2\">"u8, isReadingCompleted: true, StartTagState(TokenType.Value, hasSkippedSpacingInStartTag: false, TokenType.Attribute));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertXmlException(thrown, Throwing.Category.Expected, Throwing.Markup.Spacing);
        }
        #endregion

        #region mapping - Name
        /// <summary>
        /// Read name S6 (#38): malformed name
        /// </summary>
        [Theory]
        [InlineData("1a>", false)]
        [InlineData("-a>", true)]
        public void Throwing_ForMalformedName_ItShould_BeNameName(
            string text,
            bool isReadingCompleted
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadNameThrown(ref reader);

            // Assert
            AssertXmlException(thrown, Throwing.Category.Name, Throwing.Markup.Name);
        }

        /// <summary>
        /// Read name S9 (#38): no name when reading is completed
        /// </summary>
        [Fact]
        public void Throwing_ForNoNameWhenReadingCompleted_ItShould_BeNameName()
        {
            // Arrange
            Utf8XmlReader reader = new(""u8, isReadingCompleted: true, ContextState(Context.StartTag, TokenType.Element));

            // Act
            Exception? thrown = ReadNameThrown(ref reader);

            // Assert
            AssertXmlException(thrown, Throwing.Category.Name, Throwing.Markup.Name);
        }

        /// <summary>
        /// Processing instruction S5/S6 (#24): target not a name, or <c>xml</c> in any case
        /// </summary>
        [Theory]
        [InlineData("<?1pi?>", Context.Prolog, TokenType.None)]
        [InlineData("<?xMl?>", Context.Content, TokenType.Element)]
        public void Throwing_ForMalformedProcessingInstructionTarget_ItShould_BeNameProcessingInstructionTarget(
            string text,
            Context context,
            TokenType current
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, OpaqueState(context, current));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertXmlException(thrown, Throwing.Category.Name, Throwing.Markup.ProcessingInstructionTarget);
        }
        #endregion

        #region mapping - Order
        /// <summary>
        /// Declaration S0 (#24): not at line 0, position 0
        /// </summary>
        [Fact]
        public void Throwing_ForDeclarationNotFirst_ItShould_BeOrderDeclaration()
        {
            // Arrange
            Utf8XmlReader reader = new("<?xml version=\"1.0\"?>"u8, isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None, lineNumber: 0, linePosition: 2));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertXmlException(thrown, Throwing.Category.Order, Throwing.Markup.Declaration);
        }

        /// <summary>
        /// Document type S1 (#24): a second document type
        /// </summary>
        [Fact]
        public void Throwing_ForDocumentTypeRepeated_ItShould_BeOrderDocumentType()
        {
            // Arrange
            Utf8XmlReader reader = new("<!DOCTYPE root>"u8, isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.Comment, documentType: true));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            AssertXmlException(thrown, Throwing.Category.Order, Throwing.Markup.DocumentType);
        }
        #endregion
    }
}
