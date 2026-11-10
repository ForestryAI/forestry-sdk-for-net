using System.Text;
using Xunit;
using Forestry.Xml.Reading;

namespace Forestry.Xml.Tests
{
    /// <summary>
    /// <see cref="ReaderOptions.MaxDepth"/>: rejected values and enforcement (spec 001 US2,
    /// tasks.md T033, #66)
    /// </summary>
    /// <remarks>
    /// Test shells written before the code (C4), reviewed at T039. A struct's field at 0 can only
    /// mean "never set": the setter rejects an explicit 0, the constructors default an unset one
    /// to 64 (research.md R5). <c>MaxDepth</c> is enforced: a start tag at
    /// <c>Depth == MaxDepth</c> throws Limit + Element at its name, before committing (S1).
    /// </remarks>
    public class ReaderOptionsTests
    {
        #region helpers
        /// <summary>
        /// Read the document as one span to its end, returning the token types
        /// </summary>
        private static List<TokenType> ReadToEnd(string document, ReaderOptions options)
        {
            List<TokenType> tokens = [];
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(document), isReadingCompleted: true, new ReaderState(options));

            while (reader.Read())
            {
                tokens.Add(reader.TokenType);
            }

            return tokens;
        }
        #endregion

        #region setter
        /// <summary>
        /// An explicit 0, a negative value or more than the element stack's 64 levels is rejected
        /// when set
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(ElementStack.NonAllocatingMaxDepth + 1)]
        public void MaxDepth_ForAValueOutOfRange_ItShould_Throw(int value)
        {
            // Act, Assert
            Assert.Throws<InvalidOperationException>(() => new ReaderOptions { MaxDepth = value });
        }

        /// <summary>
        /// The element stack's 64 levels can be set
        /// </summary>
        [Fact]
        public void MaxDepth_ForSixtyFour_ItShould_BeAccepted()
        {
            // Act
            ReaderOptions options = new() { MaxDepth = 64 };

            // Assert
            Assert.Equal(64, options.MaxDepth);
        }
        #endregion

        #region packaging gate (eng spec 001 T023)
        /// <summary>
        /// Default cell of <c>MaxDepth</c>: never set, 64 nested elements read and the 65th start
        /// tag throws Limit + Element
        /// </summary>
        [Fact]
        public void MaxDepth_Default_ItShould_ReadSixtyFourLevelsAndThrowAtTheNext()
        {
            // Arrange
            static string Nested(int levels) =>
                string.Concat(Enumerable.Repeat("<a>", levels)) + string.Concat(Enumerable.Repeat("</a>", levels));

            // Act
            List<TokenType> tokens = ReadToEnd(Nested(64), default);
            XmlException thrown = Assert.Throws<XmlException>(() => ReadToEnd(Nested(65), default));

            // Assert
            Assert.Equal(128, tokens.Count);
            Assert.Equal("Limit", thrown.Category);
            Assert.Equal("Element", thrown.Markup);
        }

        /// <summary>
        /// Override cell of <c>MaxNameLength</c>: the upper bound, the element stack's 2048 name
        /// bytes, can be set
        /// </summary>
        [Fact]
        public void MaxNameLength_Override_ForTheCapacity_ItShould_BeAccepted()
        {
            // Act
            ReaderOptions options = new() { MaxNameLength = ElementStack.NameBytesCapacity };

            // Assert
            Assert.Equal(2048, options.MaxNameLength);
        }
        #endregion

        #region enforcement
        /// <summary>
        /// Never set, <c>MaxDepth</c> gets the default: three nested elements read
        /// </summary>
        [Fact]
        public void Read_ForDefaultOptions_ItShould_ReadNestedElements()
        {
            // Act
            List<TokenType> tokens = ReadToEnd("<a><b><c/></b></a>", default);

            // Assert
            Assert.Equal(
                new[] { TokenType.Element, TokenType.Element, TokenType.Element, TokenType.ElementEnd, TokenType.ElementEnd, TokenType.ElementEnd },
                tokens);
        }

        /// <summary>
        /// With <c>MaxDepth = 2</c> two nested elements read
        /// </summary>
        [Fact]
        public void Read_ForADepthAtTheLimit_ItShould_Read()
        {
            // Act
            List<TokenType> tokens = ReadToEnd("<a><b/></a>", new ReaderOptions { MaxDepth = 2 });

            // Assert
            Assert.Equal(new[] { TokenType.Element, TokenType.Element, TokenType.ElementEnd, TokenType.ElementEnd }, tokens);
        }

        /// <summary>
        /// With <c>MaxDepth = 2</c> the third nested start tag throws Limit + Element at its name,
        /// nothing of it committed
        /// </summary>
        [Fact]
        public void Read_ForADepthBeyondTheLimit_ItShould_ThrowLimitCommittingNothing()
        {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes("<a><b><c/></b></a>"), isReadingCompleted: true, new ReaderState(new ReaderOptions { MaxDepth = 2 }));
            Assert.True(reader.Read());
            Assert.True(reader.Read());

            // Act
            XmlException? thrown = null;
            try
            {
                reader.Read();
            }
            catch (XmlException exception)
            {
                thrown = exception;
            }

            // Assert
            Assert.NotNull(thrown);
            Assert.Equal("Limit", thrown.Category);
            Assert.Equal("Element", thrown.Markup);
            Assert.Equal(1, thrown.LineNumber);
            Assert.Equal(8, thrown.LinePosition);
            Assert.Equal(2, reader.Depth);
            Assert.Equal(6, reader.Position);
            Assert.Equal(TokenType.Element, reader.TokenType);
        }
        #endregion
    }
}
