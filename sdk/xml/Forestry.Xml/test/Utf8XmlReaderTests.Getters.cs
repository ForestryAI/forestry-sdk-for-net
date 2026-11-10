using System.Text;
using Xunit;
using Forestry.Xml.Reading;

namespace Forestry.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Value getter shells, from #28 (C3) and the developer's decisions of 2026-11-10 ----
        //
        // Written before the code changes (C4), so they do not compile until GetString() returns
        // a string.
        //
        // | Getter         | No token read (None) | Value token | Other tokens       | Not convertible |
        // |----------------|----------------------|-------------|--------------------|-----------------|
        // | GetString      | throws IOE           | text        | name or whole markup | throws IOE    |
        // | TryGetString   | false                | text        | name or whole markup | false         |
        // | TryGetBoolean  | false                | parsed      | false              | false           |
        //
        // IOE = InvalidOperationException with a message of its own, not the default one.
        //
        // - Names exclude the terminals (constitution, Token): Element, Attribute and an end
        //   tag's ElementEnd give the name; `/>` gives an empty string.
        // - The four opaque tokens give their whole markup, the same as Value (developer).
        // - References are not resolved: `&amp;` stays `&amp;` and HasEscapedValue says so.
        // - After Read() returned false the value is cleared (#28: unreliable), so the token keeps
        //   its type and the getters see an empty value.

        #region helpers
        /// <summary>
        /// Read a number of tokens, each of which must advance
        /// </summary>
        private static void ReadTokens(ref Utf8XmlReader reader, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Assert.True(reader.Read());
            }
        }

        /// <summary>
        /// Get string catching the exception, a ref struct can't be captured by Assert.Throws
        /// </summary>
        private static Exception? GetStringThrown(ref Utf8XmlReader reader)
        {
            try
            {
                _ = reader.GetString();
            }
            catch (Exception exception)
            {
                return exception;
            }

            return null;
        }

        /// <summary>
        /// The exception is an invalid operation with a message of its own
        /// </summary>
        private static void AssertInvalidOperationWithMessage(Exception? thrown)
        {
            InvalidOperationException exception = Assert.IsType<InvalidOperationException>(thrown);
            Assert.NotEqual(new InvalidOperationException().Message, exception.Message);
        }

        /// <summary>
        /// Character data that is not UTF-8, the reader passes bytes at or above 0x80 unchecked
        /// </summary>
        private static byte[] DocumentWithInvalidUtf8() => [.. "<a>"u8, 0xFF, .. "</a>"u8];
        #endregion

        #region GetString
        /// <summary>
        /// Every token that carries a value gives it: a name without its terminals, a Value, and
        /// the four opaque tokens as their whole markup
        /// </summary>
        [Theory]
        [InlineData("<a b=\"c\">d</a>", 1, TokenType.Element, "a")]
        [InlineData("<a b=\"c\">d</a>", 2, TokenType.Attribute, "b")]
        [InlineData("<a b=\"c\">d</a>", 3, TokenType.Value, "c")]
        [InlineData("<a b=\"c\">d</a>", 4, TokenType.Value, "d")]
        [InlineData("<a b=\"c\">d</a>", 5, TokenType.ElementEnd, "a")]
        [InlineData("<a/>", 2, TokenType.ElementEnd, "")]
        [InlineData("<a b=\"\"/>", 3, TokenType.Value, "")]
        [InlineData("<?xml version=\"1.0\"?><a/>", 1, TokenType.Declaration, "<?xml version=\"1.0\"?>")]
        [InlineData("<!DOCTYPE a><a/>", 1, TokenType.DocumentType, "<!DOCTYPE a>")]
        [InlineData("<!-- c --><a/>", 1, TokenType.Comment, "<!-- c -->")]
        [InlineData("<?pi data?><a/>", 1, TokenType.ProcessInstruction, "<?pi data?>")]
        public void GetString_ForATokenWithAValue_ItShould_ReturnTheValueAsText(
            string text,
            int reads,
            TokenType expectedToken,
            string expected
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text).AsSpan());
            ReadTokens(ref reader, reads);

            // Act
            string value = reader.GetString();

            // Assert
            Assert.Equal(expectedToken, reader.TokenType);
            Assert.Equal(expected, value);
        }

        /// <summary>
        /// Before any token, nothing can be converted
        /// </summary>
        [Fact]
        public void GetString_BeforeAnyRead_ItShould_ThrowAnInvalidOperation()
        {
            // Arrange
            Utf8XmlReader reader = new("<a/>"u8);

            // Act
            Exception? thrown = GetStringThrown(ref reader);

            // Assert
            Assert.Equal(TokenType.None, reader.TokenType);
            AssertInvalidOperationWithMessage(thrown);
        }

        /// <summary>
        /// The first Read() halted, so there is still no token
        /// </summary>
        [Fact]
        public void GetString_WhenTheFirstReadHalted_ItShould_ThrowAnInvalidOperation()
        {
            // Arrange
            Utf8XmlReader reader = new("<a"u8, isReadingCompleted: false, new ReaderState(default(ReaderOptions)));
            Assert.False(reader.Read());

            // Act
            Exception? thrown = GetStringThrown(ref reader);

            // Assert
            AssertInvalidOperationWithMessage(thrown);
        }

        /// <summary>
        /// After Read() returned false the token keeps its type but its value is cleared (#28)
        /// </summary>
        [Theory]
        [InlineData("<a>", false, 1, TokenType.Element)]
        [InlineData("<a>b<", false, 2, TokenType.Value)]
        [InlineData("<a>b</a>", true, 3, TokenType.ElementEnd)]
        public void GetString_AfterReadReturnedFalse_ItShould_ReturnEmpty(
            string text,
            bool isReadingCompleted,
            int reads,
            TokenType expectedToken
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text).AsSpan(), isReadingCompleted, new ReaderState(default(ReaderOptions)));
            ReadTokens(ref reader, reads);
            Assert.False(reader.Read());

            // Act
            string value = reader.GetString();

            // Assert
            Assert.Equal(expectedToken, reader.TokenType);
            Assert.Equal("", value);
        }

        /// <summary>
        /// A value straddling segments gives the same text as one span (#28: normalized). The
        /// last row is the empty value of <c>/&gt;</c> straight after a straddling value
        /// </summary>
        [Theory]
        [InlineData(new[] { "<a>he", "llo</a>" }, 2, "hello")]
        [InlineData(new[] { "<a b=\"he", "llo\"/>" }, 3, "hello")]
        [InlineData(new[] { "<lo", "ng/>" }, 1, "long")]
        [InlineData(new[] { "<!-- ", "c --><a/>" }, 1, "<!-- c -->")]
        [InlineData(new[] { "<a b=\"he", "llo\"/>" }, 4, "")]
        public void GetString_ForAValueStraddlingSegments_ItShould_ReturnTheJoinedText(
            string[] parts,
            int reads,
            string expected
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts));
            ReadTokens(ref reader, reads);

            // Act
            string value = reader.GetString();

            // Assert
            Assert.Equal(expected, value);
        }

        /// <summary>
        /// A segment boundary inside a multi-byte character still decodes the character
        /// </summary>
        [Fact]
        public void GetString_ForACharacterSplitBetweenSegments_ItShould_DecodeTheCharacter()
        {
            // Arrange
            Utf8XmlReader reader = new(SequenceSplitAt("<a>å</a>", 4));
            ReadTokens(ref reader, 2);

            // Act
            string value = reader.GetString();

            // Assert
            Assert.True(reader.HasValueSequence);
            Assert.Equal("å", value);
        }

        /// <summary>
        /// References are not resolved by the getter
        /// </summary>
        [Fact]
        public void GetString_ForAValueWithReferences_ItShould_ReturnThemRaw()
        {
            // Arrange
            Utf8XmlReader reader = new("<a>x&amp;y</a>"u8);
            ReadTokens(ref reader, 2);

            // Act
            string value = reader.GetString();

            // Assert
            Assert.True(reader.HasEscapedValue);
            Assert.Equal("x&amp;y", value);
        }

        /// <summary>
        /// A value that can't be converted is an invalid operation (#28)
        /// </summary>
        [Fact]
        public void GetString_ForInvalidUtf8_ItShould_ThrowAnInvalidOperation()
        {
            // Arrange
            Utf8XmlReader reader = new(DocumentWithInvalidUtf8().AsSpan());
            ReadTokens(ref reader, 2);

            // Act
            Exception? thrown = GetStringThrown(ref reader);

            // Assert
            AssertInvalidOperationWithMessage(thrown);
        }
        #endregion

        #region TryGetString
        /// <summary>
        /// The same values as <c>GetString</c>
        /// </summary>
        [Theory]
        [InlineData("<a b=\"c\">d</a>", 1, "a")]
        [InlineData("<a b=\"c\">d</a>", 2, "b")]
        [InlineData("<a b=\"c\">d</a>", 3, "c")]
        [InlineData("<a b=\"c\">d</a>", 4, "d")]
        [InlineData("<a b=\"c\">d</a>", 5, "a")]
        [InlineData("<a/>", 2, "")]
        [InlineData("<?xml version=\"1.0\"?><a/>", 1, "<?xml version=\"1.0\"?>")]
        [InlineData("<!DOCTYPE a><a/>", 1, "<!DOCTYPE a>")]
        [InlineData("<!-- c --><a/>", 1, "<!-- c -->")]
        [InlineData("<?pi data?><a/>", 1, "<?pi data?>")]
        public void TryGetString_ForATokenWithAValue_ItShould_ReturnTrueWithTheText(
            string text,
            int reads,
            string expected
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text).AsSpan());
            ReadTokens(ref reader, reads);

            // Act
            bool converted = reader.TryGetString(out string? value);

            // Assert
            Assert.True(converted);
            Assert.Equal(expected, value);
        }

        /// <summary>
        /// A value straddling segments gives the same text as one span
        /// </summary>
        [Fact]
        public void TryGetString_ForAValueStraddlingSegments_ItShould_ReturnTrueWithTheJoinedText()
        {
            // Arrange
            Utf8XmlReader reader = new(Sequence("<a>he", "llo</a>"));
            ReadTokens(ref reader, 2);

            // Act
            bool converted = reader.TryGetString(out string? value);

            // Assert
            Assert.True(converted);
            Assert.Equal("hello", value);
        }

        /// <summary>
        /// Before any token it returns false instead of throwing
        /// </summary>
        [Fact]
        public void TryGetString_BeforeAnyRead_ItShould_ReturnFalse()
        {
            // Arrange
            Utf8XmlReader reader = new("<a/>"u8);

            // Act
            bool converted = reader.TryGetString(out string? value);

            // Assert
            Assert.False(converted);
            Assert.Null(value);
        }

        /// <summary>
        /// A value that can't be converted returns false instead of throwing
        /// </summary>
        [Fact]
        public void TryGetString_ForInvalidUtf8_ItShould_ReturnFalse()
        {
            // Arrange
            Utf8XmlReader reader = new(DocumentWithInvalidUtf8().AsSpan());
            ReadTokens(ref reader, 2);

            // Act
            bool converted = reader.TryGetString(out string? value);

            // Assert
            Assert.False(converted);
            Assert.Null(value);
        }

        /// <summary>
        /// After Read() returned false the token keeps its type but its value is cleared (#28)
        /// </summary>
        [Fact]
        public void TryGetString_AfterReadReturnedFalse_ItShould_ReturnTrueWithEmpty()
        {
            // Arrange
            Utf8XmlReader reader = new("<a>b<"u8, isReadingCompleted: false, new ReaderState(default(ReaderOptions)));
            ReadTokens(ref reader, 2);
            Assert.False(reader.Read());

            // Act
            bool converted = reader.TryGetString(out string? value);

            // Assert
            Assert.True(converted);
            Assert.Equal("", value);
        }
        #endregion

        #region TryGetBoolean
        /// <summary>
        /// Character data and attribute values convert
        /// </summary>
        [Theory]
        [InlineData("<a>true</a>", 2, true)]
        [InlineData("<a>false</a>", 2, false)]
        [InlineData("<a b=\"true\"/>", 3, true)]
        [InlineData("<a b=\"false\"/>", 3, false)]
        public void TryGetBoolean_ForAValueToken_ItShould_ReturnTrueWithTheBoolean(
            string text,
            int reads,
            bool expected
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text).AsSpan());
            ReadTokens(ref reader, reads);

            // Act
            bool converted = reader.TryGetBoolean(out bool? value);

            // Assert
            Assert.True(converted);
            Assert.Equal(expected, value);
        }

        /// <summary>
        /// A value straddling segments converts like one span
        /// </summary>
        [Fact]
        public void TryGetBoolean_ForAValueStraddlingSegments_ItShould_ReturnTrueWithTheBoolean()
        {
            // Arrange
            Utf8XmlReader reader = new(Sequence("<a>tr", "ue</a>"));
            ReadTokens(ref reader, 2);

            // Act
            bool converted = reader.TryGetBoolean(out bool? value);

            // Assert
            Assert.True(converted);
            Assert.Equal((bool?)true, value);
        }

        /// <summary>
        /// A name or opaque markup is never a boolean, even when its text would parse
        /// </summary>
        [Theory]
        [InlineData("<true/>", 1, TokenType.Element)]
        [InlineData("<a true=\"x\"/>", 2, TokenType.Attribute)]
        [InlineData("<true></true>", 2, TokenType.ElementEnd)]
        [InlineData("<!--true--><a/>", 1, TokenType.Comment)]
        public void TryGetBoolean_ForAnotherToken_ItShould_ReturnFalse(
            string text,
            int reads,
            TokenType expectedToken
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text).AsSpan());
            ReadTokens(ref reader, reads);

            // Act
            bool converted = reader.TryGetBoolean(out bool? value);

            // Assert
            Assert.Equal(expectedToken, reader.TokenType);
            Assert.False(converted);
            Assert.Null(value);
        }

        /// <summary>
        /// Before any token it returns false instead of throwing
        /// </summary>
        [Fact]
        public void TryGetBoolean_BeforeAnyRead_ItShould_ReturnFalse()
        {
            // Arrange
            Utf8XmlReader reader = new("<a/>"u8);

            // Act
            bool converted = reader.TryGetBoolean(out bool? value);

            // Assert
            Assert.False(converted);
            Assert.Null(value);
        }

        /// <summary>
        /// A Value that isn't wholly a boolean returns false
        /// </summary>
        [Theory]
        [InlineData("<a>yes</a>", 2)]
        [InlineData("<a>truex</a>", 2)]
        [InlineData("<a> true</a>", 2)]
        [InlineData("<a b=\"\"/>", 3)]
        public void TryGetBoolean_ForAValueNotABoolean_ItShould_ReturnFalse(
            string text,
            int reads
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text).AsSpan());
            ReadTokens(ref reader, reads);

            // Act
            bool converted = reader.TryGetBoolean(out bool? value);

            // Assert
            Assert.False(converted);
            Assert.Null(value);
        }

        /// <summary>
        /// After Read() returned false on a Value token its value is cleared, so nothing converts
        /// </summary>
        [Fact]
        public void TryGetBoolean_AfterReadReturnedFalse_ItShould_ReturnFalse()
        {
            // Arrange
            Utf8XmlReader reader = new("<a>true<"u8, isReadingCompleted: false, new ReaderState(default(ReaderOptions)));
            ReadTokens(ref reader, 2);
            Assert.False(reader.Read());

            // Act
            bool converted = reader.TryGetBoolean(out bool? value);

            // Assert
            Assert.Equal(TokenType.Value, reader.TokenType);
            Assert.False(converted);
            Assert.Null(value);
        }
        #endregion
    }
}
