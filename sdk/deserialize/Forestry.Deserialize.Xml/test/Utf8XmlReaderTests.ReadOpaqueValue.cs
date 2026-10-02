using System.Buffers;
using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- Read opaque value shells, from #24 ----------------------------------------------
        //
        // The `ReadOpaqueValue_*` tests below are shells written from #24's architecture text
        // alone (requirements, malformed position, malformed non-terminal, advancement, state
        // table S0-S6), before the opaque value delegate exists - see doc/dev/Velocity.md's Test
        // shell phase.
        //
        // The delegate is reached through ReadValue() (#27's delegation), so no shell names the
        // delegate method. Multi-token scenarios (a second document type, trailing spacing left
        // for the next read) go through Read().
        //
        // Test seam - PLACEHOLDER, not decided by the architecture, to be confirmed or renamed
        // during Understanding. Won't compile until it exists:
        // - the document type flag as an internal ReaderState field `_isDocumentTypeRead`, set
        //   through a new ReaderState constructor parameter `isDocumentTypeRead`
        //
        // Exceptions are only asserted by type (XmlException): the architecture doesn't decide
        // messages. Whether a value that fits in one segment of a byte sequence is a Value or a
        // ValueSequence isn't decided either, so only a value that straddles segments asserts
        // HasValueSequence.
        //
        // Not covered: the BOM and line tracking in SkipMultipleSpacing, both named as backlog
        // warnings in #24 rather than requirements.

        #region helpers
        /// <summary>
        /// Reader state for a context with explicit line tracking and document type flag
        /// </summary>
        private static ReaderState OpaqueState(
            Context context,
            TokenType current,
            long lineNumber = 0,
            long linePosition = 0,
            bool documentType = false
        ) => new(
            lineNumber: lineNumber,
            linePosition: linePosition,
            currentTokenType: current,
            previousTokenType: TokenType.None,
            elementStack: context switch
            {
                Context.Prolog => ElementStackBeforeAnyElement(),
                Context.StartTag => ElementStackAtDepth(1, contentReady: false),
                Context.Content => ElementStackAtDepth(1, contentReady: true),
                Context.Miscellaneous => ElementStackAfterRootClosed(),
                _ => throw new ArgumentOutOfRangeException(nameof(context)),
            },
            readerOptions: default,
            documentType: documentType
        );

        /// <summary>
        /// The value as text, whether it is a byte span or a byte sequence
        /// </summary>
        private static string ValueText(ref Utf8XmlReader reader) =>
            Encoding.UTF8.GetString(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.Value);

        /// <summary>
        /// Read value catching the exception, a ref struct can't be captured by Assert.Throws
        /// </summary>
        private static Exception? ReadValueThrown(ref Utf8XmlReader reader)
        {
            try
            {
                reader.ReadValue();
            }
            catch (Exception exception)
            {
                return exception;
            }

            return null;
        }

        /// <summary>
        /// Read catching the exception, a ref struct can't be captured by Assert.Throws
        /// </summary>
        private static Exception? ReadThrown(ref Utf8XmlReader reader)
        {
            try
            {
                reader.Read();
            }
            catch (Exception exception)
            {
                return exception;
            }

            return null;
        }
        #endregion

        #region S2
        /// <summary>
        /// S2: the ending terminal is found, so the value runs from the first character of the
        /// starting terminal to the last character of the ending terminal, the token is set and
        /// the delegate returns true
        /// </summary>
        /// <remarks>
        /// <c>&lt;?x?&gt;</c> and <c>&lt;?xm?&gt;</c> guard advancement skipping only the starting
        /// terminal: their scratch pads (<c>&lt;?x?</c>, <c>&lt;?xm?</c>) overshoot into the
        /// ending terminal.  When reader construction is from a byte span.
        /// </remarks>
        [Theory]
        [InlineData("<?xml version=\"1.0\"?>", Context.Prolog, TokenType.None, TokenType.Declaration)]
        [InlineData("<?xml\tversion=\"1.0\"?>", Context.Prolog, TokenType.None, TokenType.Declaration)]
        [InlineData("<!DOCTYPE root>", Context.Prolog, TokenType.None, TokenType.DocumentType)]
        [InlineData("<!DOCTYPE root>", Context.Prolog, TokenType.Declaration, TokenType.DocumentType)]
        [InlineData("<!-- comment -->", Context.Prolog, TokenType.None, TokenType.Comment)]
        [InlineData("<!---->", Context.Prolog, TokenType.None, TokenType.Comment)]
        [InlineData("<!-- a - b -->", Context.Content, TokenType.Element, TokenType.Comment)]
        [InlineData("<!-- comment -->", Context.Miscellaneous, TokenType.ElementEnd, TokenType.Comment)]
        [InlineData("<?pi data?>", Context.Prolog, TokenType.None, TokenType.ProcessInstruction)]
        [InlineData("<?pi?>", Context.Content, TokenType.Element, TokenType.ProcessInstruction)]
        [InlineData("<?pi-1.x data?>", Context.Miscellaneous, TokenType.ElementEnd, TokenType.ProcessInstruction)]
        [InlineData("<?x?>", Context.Prolog, TokenType.None, TokenType.ProcessInstruction)]
        [InlineData("<?xm?>", Context.Prolog, TokenType.None, TokenType.ProcessInstruction)]
        [InlineData("<?xml-stylesheet href=\"a.xsl\"?>", Context.Prolog, TokenType.None, TokenType.ProcessInstruction)]
        public void ReadOpaqueValue_ForS2EndingTerminalFound_ItShould_SetTheValueAndTokenReturningTrue(
            string text,
            Context context,
            TokenType current,
            TokenType expected
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, OpaqueState(context, current));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.Equal(text, ValueText(ref reader));
            Assert.Equal(text.Length, reader.Position);
            Assert.Equal(expected, reader.TokenType);
            Assert.Equal(current, reader.ReaderState._previousTokenType);
        }

        /// <summary>
        /// S2: the delegate stops after the last character of the ending terminal and never
        /// consumes subsequent spacing
        /// </summary>
        [Theory]
        [InlineData("<!-- comment -->  <root/>", "<!-- comment -->")]
        [InlineData("<?pi data?>\n<root/>", "<?pi data?>")]
        [InlineData("<!DOCTYPE root> <root/>", "<!DOCTYPE root>")]
        public void ReadOpaqueValue_ForS2FollowedBySpacing_ItShould_StopAfterTheEndingTerminal(
            string text,
            string expected
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.Equal(expected, ValueText(ref reader));
            Assert.Equal(expected.Length, reader.Position);
        }

        /// <summary>
        /// S2: the value straddles segments, including an ending terminal split between two
        /// segments, and becomes a value sequence
        /// </summary>
        /// <remarks>When reader construction is from a byte sequence</remarks>
        [Theory]
        [InlineData(new[] { "<!-", "- comment -", "->" }, TokenType.Comment)]
        [InlineData(new[] { "<?pi data?", ">" }, TokenType.ProcessInstruction)]
        [InlineData(new[] { "<!DOCTYPE root", "", ">" }, TokenType.DocumentType)]
        [InlineData(new[] { "<?xml version=\"1.0\"", "?>" }, TokenType.Declaration)]
        public void ReadOpaqueValue_ForS2EndingTerminalFoundStraddlingSegments_ItShould_SetAValueSequenceReturningTrue(
            string[] parts,
            TokenType expected
        ) {
            // Arrange
            string text = string.Concat(parts);
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.True(reader.HasValueSequence);
            Assert.Equal(text, ValueText(ref reader));
            Assert.Equal(text.Length, reader.Position);
            Assert.Equal(expected, reader.TokenType);
        }

        /// <summary>
        /// S2: line feeds inside the value advance the line number and restart the line position
        /// (0-based), otherwise the line position advances by the value's length
        /// </summary>
        [Theory]
        [InlineData("<!-- comment -->", 0, 16)]
        [InlineData("<!-- a\nb -->", 1, 5)]
        [InlineData("<?pi\n\ndata?>", 2, 6)]
        public void ReadOpaqueValue_ForS2_ItShould_AdvanceTheLineNumberAndLinePosition(
            string text,
            long expectedLineNumber,
            long expectedLinePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.Equal(expectedLineNumber, reader.ReaderState._lineNumber);
            Assert.Equal(expectedLinePosition, reader.ReaderState._linePosition);
        }

        /// <summary>
        /// S2: reading a document type sets the document type flag in the reader state
        /// </summary>
        [Fact]
        public void ReadOpaqueValue_ForS2DocumentType_ItShould_SetTheDocumentTypeFlag()
        {
            // Arrange
            Utf8XmlReader reader = new("<!DOCTYPE root>"u8, isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.True(advancement);
            Assert.True(reader.ReaderState._documentType);
        }
        #endregion

        #region S3
        /// <summary>
        /// S3: the ending terminal is not found and reading is not completed, so the delegate
        /// rolls back committing nothing and returns false
        /// </summary>
        /// <remarks>When reader construction is from a byte span</remarks>
        [Theory]
        [InlineData("<!-- comment")]
        [InlineData("<!-- comment --")]
        [InlineData("<?pi data?")]
        [InlineData("<!DOCTYPE root")]
        [InlineData("<?xml version=\"1.0\"")]
        [InlineData("<!-- a\nb")]
        public void ReadOpaqueValue_ForS3EndingTerminalNotFoundAndReadingNotCompleted_ItShould_RollBackReturningFalse(
            string text
        ) {
            // Arrange
            ReaderState state = OpaqueState(Context.Prolog, TokenType.None, lineNumber: 0, linePosition: 0);
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: false, state);

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.False(advancement);
            Assert.Equal(0, reader.Position);
            Assert.Equal(state._lineNumber, reader.ReaderState._lineNumber);
            Assert.Equal(state._linePosition, reader.ReaderState._linePosition);
            Assert.Equal(state._currentTokenType, reader.TokenType);
            Assert.Equal(state._previousTokenType, reader.ReaderState._previousTokenType);
            Assert.False(reader.ReaderState._documentType);
            Assert.Equal("", ValueText(ref reader));
        }

        /// <summary>
        /// S3: a rollback after advancing into following segments restores the position and
        /// the sequence position the caller expands the byte sequence from
        /// </summary>
        /// <remarks>When reader construction is from a byte sequence</remarks>
        [Theory]
        [InlineData([new[] { "<!-- com", "ment -" }])]
        [InlineData([new[] { "<?pi", " data", "?" }])]
        [InlineData([new[] { "<!DOC", "TYPE root" }])]
        public void ReadOpaqueValue_ForS3EndingTerminalNotFoundStraddlingSegments_ItShould_RestoreThePositionAndSequencePosition(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: false, OpaqueState(Context.Prolog, TokenType.None));
            long position = reader.Position;
            SequencePosition sequencePosition = reader.SequencePosition;

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.False(advancement);
            Assert.Equal(position, reader.Position);
            Assert.Equal(sequencePosition, reader.SequencePosition);
            Assert.Equal(TokenType.None, reader.TokenType);
        }
        #endregion

        #region S4
        /// <summary>
        /// S4: the ending terminal is not found and reading is completed, so the markup is
        /// malformed and the delegate throws
        /// </summary>
        [Theory]
        [InlineData("<!-- comment")]
        [InlineData("<!-- comment --")]
        [InlineData("<!-- comment -- >")]
        [InlineData("<?pi data?")]
        [InlineData("<?pi data ? >")]
        [InlineData("<!DOCTYPE root")]
        [InlineData("<?xml version=\"1.0\"")]
        public void ReadOpaqueValue_ForS4EndingTerminalNotFoundAndReadingCompleted_ItShould_Throw(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }

        /// <summary>
        /// S4: straddling segments, reading completed
        /// </summary>
        [Theory]
        [InlineData([new[] { "<!-- com", "ment -" }])]
        [InlineData([new[] { "<?pi", " data", "?" }])]
        public void ReadOpaqueValue_ForS4EndingTerminalNotFoundAndReadingCompletedStraddlingSegments_ItShould_Throw(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion

        #region S0
        /// <summary>
        /// S0: a declaration not at line number 0, line position 0 is a malformed position
        /// </summary>
        [Theory]
        [InlineData(0, 2)]
        [InlineData(1, 0)]
        [InlineData(1, 2)]
        public void ReadOpaqueValue_ForS0DeclarationNotAtTheStart_ItShould_Throw(
            long lineNumber,
            long linePosition
        ) {
            // Arrange
            Utf8XmlReader reader = new("<?xml version=\"1.0\"?>"u8, isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None, lineNumber, linePosition));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }

        /// <summary>
        /// S0 through Read(): spacing skipped before a declaration advances the line position,
        /// so the declaration is not at the start
        /// </summary>
        /// <remarks>When reader construction is from a byte span (see #24's warning on
        /// SkipMultipleSpacing)</remarks>
        [Fact]
        public void ReadOpaqueValue_ForS0SpacingBeforeTheDeclaration_ItShould_Throw()
        {
            // Arrange
            Utf8XmlReader reader = new("  <?xml version=\"1.0\"?>"u8, isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None));

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion

        #region S1
        /// <summary>
        /// S1: a document type after a document type has been read is a malformed position,
        /// asserted before advancement
        /// </summary>
        [Theory]
        [InlineData(TokenType.DocumentType)]
        [InlineData(TokenType.Comment)]
        [InlineData(TokenType.ProcessInstruction)]
        public void ReadOpaqueValue_ForS1SecondDocumentType_ItShould_Throw(
            TokenType current
        ) {
            // Arrange
            Utf8XmlReader reader = new("<!DOCTYPE root>"u8, isReadingCompleted: true, OpaqueState(Context.Prolog, current, documentType: true));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
            Assert.Equal(0, reader.Position);
        }

        /// <summary>
        /// S1 through Read(): the document type flag survives an intervening comment, which the
        /// current and previous tokens can't
        /// </summary>
        [Fact]
        public void ReadOpaqueValue_ForS1SecondDocumentTypeAfterAComment_ItShould_Throw()
        {
            // Arrange
            Utf8XmlReader reader = new("<!DOCTYPE a><!-- c --><!DOCTYPE b>"u8, isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None));
            Assert.True(reader.Read());   // document type
            Assert.True(reader.Read());   // comment

            // Act
            Exception? thrown = ReadThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion

        #region S5
        /// <summary>
        /// S5: the PI target is not a <c>Name</c> followed by spacing or <c>?&gt;</c>, asserted
        /// after the ending terminal is found
        /// </summary>
        [Theory]
        [InlineData("<?1pi?>")]
        [InlineData("<?-pi?>")]
        [InlineData("<? pi?>")]
        [InlineData("<?pi!x?>")]
        [InlineData("<??>")]
        public void ReadOpaqueValue_ForS5MalformedPITarget_ItShould_Throw(
            string text
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }

        /// <summary>
        /// S5 straddling segments: the PI target is split between segments
        /// </summary>
        [Theory]
        [InlineData([new[] { "<?p", "i!x?>" }])]
        [InlineData([new[] { "<?", "1pi?>" }])]
        public void ReadOpaqueValue_ForS5MalformedPITargetStraddlingSegments_ItShould_Throw(
            string[] parts
        ) {
            // Arrange
            Utf8XmlReader reader = new(Sequence(parts), isReadingCompleted: true, OpaqueState(Context.Prolog, TokenType.None));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion

        #region S6
        /// <summary>
        /// S6: a PI target equal to <c>xml</c> in any case is malformed, including a declaration
        /// out of place, which delegation (#27's S3) hands over as a processing instruction
        /// </summary>
        [Theory]
        [InlineData("<?XML version=\"1.0\"?>", Context.Prolog, TokenType.None)]
        [InlineData("<?xMl?>", Context.Content, TokenType.Element)]
        [InlineData("<?xml version=\"1.0\"?>", Context.Prolog, TokenType.Comment)]
        [InlineData("<?xml version=\"1.0\"?>", Context.Miscellaneous, TokenType.ElementEnd)]
        public void ReadOpaqueValue_ForS6PITargetEqualToXml_ItShould_Throw(
            string text,
            Context context,
            TokenType current
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, OpaqueState(context, current));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }

        /// <summary>
        /// S6 straddling segments: the PI target <c>xml</c> is split between segments
        /// </summary>
        [Fact]
        public void ReadOpaqueValue_ForS6PITargetEqualToXmlStraddlingSegments_ItShould_Throw()
        {
            // Arrange
            Utf8XmlReader reader = new(Sequence("<?x", "M", "l?>"), isReadingCompleted: true, OpaqueState(Context.Content, TokenType.Element));

            // Act
            Exception? thrown = ReadValueThrown(ref reader);

            // Assert
            Assert.IsType<XmlException>(thrown);
        }
        #endregion
    }
}
