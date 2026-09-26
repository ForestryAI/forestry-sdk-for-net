using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    public partial class Utf8XmlReaderTests
    {
        // ---- ReadValue() delegation shells, from #27 -----------------------------------------
        //
        // The `ReadValueDelegation_*` tests below are shells written from
        // #27's architecture text alone (context table, delegation table D0-D10, requirements),
        // before delegation's body exists - see doc/dev/Velocity.md's Test shell phase.
        //
        // Test seam - PLACEHOLDERS, not decided by the architecture, to be confirmed or renamed
        // during Understanding. Won't compile until they exist:
        // - `internal enum Candidate { None, Declaration, DocumentType, Comment,
        //   ProcessingInstruction, ElementStart, EndTag, EmptyElementEnd, AttributeName,
        //   AttributeValue, CharacterData }`, where None means the default delegate (D10)
        // - `internal readonly Candidate DeconstructScratchPad()` - readonly makes the compiler
        //   enforce the no side effects requirement, like PeekCharacter() does for #17
        // - ReadValue() becoming `internal`
        //
        // The delegates are separate backlog tasks, so no shell asserts that a delegate was
        // called; the candidate is the observable. Each reader is built from an internal
        // ReaderState so every context is reachable without a real document producing it, and
        // the scratch pad is filled by the real PeekStartingTerminal() (#17). Straddling segments
        // is not repeated here: the scratch pad is already filled when delegation runs, and #17
        // covers straddling.

        #region helpers
        /// <summary>
        /// #27's context table, derived from the reader state
        /// </summary>
        public enum Context
        {
            Prolog,
            StartTag,
            Content,
            Miscellaneous,
        }

        /// <summary>
        /// Reader state for a context: Prolog is depth 0 with no root element, Start tag is
        /// depth != 0 with content ready false, Content is depth != 0 with content ready true and
        /// Miscellaneous is depth 0 after the root element
        /// </summary>
        private static ReaderState ContextState(Context context, TokenType current) => context switch
        {
            Context.Prolog => State(ElementStackBeforeAnyElement(), current),
            Context.StartTag => State(ElementStackAtDepth(1, contentReady: false), current),
            Context.Content => State(ElementStackAtDepth(1, contentReady: true), current),
            Context.Miscellaneous => State(ElementStackAfterRootClosed(), current),
            _ => throw new ArgumentOutOfRangeException(nameof(context)),
        };

        /// <summary>
        /// Peek the starting terminal of the text into the scratch pad, then deconstruct it
        /// </summary>
        private static Candidate Deconstruct(
            string text,
            Context context,
            TokenType current,
            bool isReadingCompleted = true
        ) {
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted, ContextState(context, current));
            Assert.True(reader.PeekStartingTerminal());

            return reader.DeconstructScratchPad();
        }
        #endregion

        #region D0
        /// <summary>
        /// D0: <c>&lt;?xml</c> followed by any <c>S</c> (space, tab, CR, LF) is the declaration
        /// only in the prolog before any token, i.e. <c>VersionInfo ::= S 'version' ...</c>
        /// </summary>
        [Theory]
        [InlineData("<?xml version=\"1.0\"?>")]
        [InlineData("<?xml\tversion=\"1.0\"?>")]
        [InlineData("<?xml\rversion=\"1.0\"?>")]
        [InlineData("<?xml\nversion=\"1.0\"?>")]
        public void ReadValueDelegation_ForD0DeclarationFirstInTheProlog_ItShould_BeADeclarationCandidate(
            string text
        ) {
            // Arrange & Act
            Candidate candidate = Deconstruct(text, Context.Prolog, TokenType.None);

            // Assert
            Assert.Equal(Candidate.Declaration, candidate);
        }
        #endregion

        #region D1
        /// <summary>
        /// D1: <c>&lt;!DOCTYPE</c> in the prolog is the document type.  Its position within the
        /// prolog (e.g. a second document type) is the delegate's concern.
        /// </summary>
        [Theory]
        [InlineData(TokenType.None)]
        [InlineData(TokenType.Declaration)]
        [InlineData(TokenType.Comment)]
        [InlineData(TokenType.DocumentType)]
        public void ReadValueDelegation_ForD1DocumentTypeInTheProlog_ItShould_BeADocumentTypeCandidate(
            TokenType current
        ) {
            // Arrange & Act
            Candidate candidate = Deconstruct("<!DOCTYPE root>", Context.Prolog, current);

            // Assert
            Assert.Equal(Candidate.DocumentType, candidate);
        }
        #endregion

        #region D2
        /// <summary>
        /// D2: <c>&lt;!--</c> is a comment in the prolog, content and miscellaneous
        /// </summary>
        [Theory]
        [InlineData(Context.Prolog, TokenType.None)]
        [InlineData(Context.Content, TokenType.Element)]
        [InlineData(Context.Miscellaneous, TokenType.ElementEnd)]
        public void ReadValueDelegation_ForD2Comment_ItShould_BeACommentCandidate(
            Context context,
            TokenType current
        ) {
            // Arrange & Act
            Candidate candidate = Deconstruct("<!-- comment -->", context, current);

            // Assert
            Assert.Equal(Candidate.Comment, candidate);
        }
        #endregion

        #region D3
        /// <summary>
        /// D3: <c>&lt;?</c> with any extra is a processing instruction in the prolog, content and
        /// miscellaneous, e.g. <c>xml-</c> is the start of <c>PITarget</c> and <c>&lt;?xml </c>
        /// after the first token is a PI (its reserved target is the delegate's concern)
        /// </summary>
        [Theory]
        [InlineData("<?pi?>", Context.Prolog, TokenType.None)]
        [InlineData("<?xml-stylesheet?>", Context.Prolog, TokenType.None)]
        [InlineData("<?xml version=\"1.0\"?>", Context.Prolog, TokenType.Declaration)]
        [InlineData("<?xml version=\"1.0\"?>", Context.Prolog, TokenType.Comment)]
        [InlineData("<?pi?>", Context.Content, TokenType.Element)]
        [InlineData("<?pi?>", Context.Miscellaneous, TokenType.ElementEnd)]
        public void ReadValueDelegation_ForD3ProcessingInstruction_ItShould_BeAProcessingInstructionCandidate(
            string text,
            Context context,
            TokenType current
        ) {
            // Arrange & Act
            Candidate candidate = Deconstruct(text, context, current);

            // Assert
            Assert.Equal(Candidate.ProcessingInstruction, candidate);
        }
        #endregion

        #region D4
        /// <summary>
        /// D4: <c>&lt;</c> with a <c>NameStartChar</c> extra is an element start in the prolog
        /// (the root) and content
        /// </summary>
        [Theory]
        [InlineData("<a>", Context.Prolog, TokenType.None)]
        [InlineData("<_a/>", Context.Prolog, TokenType.Comment)]
        [InlineData("<:a>", Context.Content, TokenType.Element)]
        [InlineData("<A>", Context.Content, TokenType.Value)]
        public void ReadValueDelegation_ForD4ElementStart_ItShould_BeAnElementStartCandidate(
            string text,
            Context context,
            TokenType current
        ) {
            // Arrange & Act
            Candidate candidate = Deconstruct(text, context, current);

            // Assert
            Assert.Equal(Candidate.ElementStart, candidate);
        }
        #endregion

        #region D5
        /// <summary>
        /// D5: <c>&lt;/</c> in content is an end tag
        /// </summary>
        [Theory]
        [InlineData(TokenType.Element)]
        [InlineData(TokenType.Value)]
        [InlineData(TokenType.ElementEnd)]
        public void ReadValueDelegation_ForD5EndTagInContent_ItShould_BeAnEndTagCandidate(
            TokenType current
        ) {
            // Arrange & Act
            Candidate candidate = Deconstruct("</a>", Context.Content, current);

            // Assert
            Assert.Equal(Candidate.EndTag, candidate);
        }
        #endregion

        #region D6
        /// <summary>
        /// D6: <c>/&gt;</c> in a start tag is the empty element end
        /// </summary>
        [Theory]
        [InlineData(TokenType.Element)]
        [InlineData(TokenType.Value)]
        public void ReadValueDelegation_ForD6EmptyElementEndInAStartTag_ItShould_BeAnEmptyElementEndCandidate(
            TokenType current
        ) {
            // Arrange & Act
            Candidate candidate = Deconstruct("/>", Context.StartTag, current);

            // Assert
            Assert.Equal(Candidate.EmptyElementEnd, candidate);
        }
        #endregion

        #region D7
        /// <summary>
        /// D7: a <c>NameStartChar</c> in a start tag is an attribute name, with no starting
        /// terminal i.e. the whole scratch pad is extra
        /// </summary>
        [Theory]
        [InlineData("b=\"1\"", TokenType.Element)]
        [InlineData("_b=\"1\"", TokenType.Value)]
        [InlineData(":b=\"1\"", TokenType.Element)]
        public void ReadValueDelegation_ForD7AttributeNameInAStartTag_ItShould_BeAnAttributeNameCandidate(
            string text,
            TokenType current
        ) {
            // Arrange & Act
            Candidate candidate = Deconstruct(text, Context.StartTag, current);

            // Assert
            Assert.Equal(Candidate.AttributeName, candidate);
        }
        #endregion

        #region D8
        /// <summary>
        /// D8: a quote in a start tag is <c>AttValue</c>'s starting terminal, reached after the
        /// skip equal step (separate backlog task) has skipped <c>Eq</c>
        /// </summary>
        [Theory]
        [InlineData("\"1\"")]
        [InlineData("'1'")]
        public void ReadValueDelegation_ForD8AttributeValueInAStartTag_ItShould_BeAnAttributeValueCandidate(
            string text
        ) {
            // Arrange & Act
            Candidate candidate = Deconstruct(text, Context.StartTag, TokenType.Attribute);

            // Assert
            Assert.Equal(Candidate.AttributeValue, candidate);
        }
        #endregion

        #region D9
        /// <summary>
        /// D9: in content anything not starting with <c>&lt;</c> or <c>&amp;</c> is character
        /// data, i.e. <c>CharData ::= [^&lt;&amp;]*</c>, including <c>/&gt;</c> which is only an
        /// empty element end in a start tag
        /// </summary>
        [Theory]
        [InlineData("a")]
        [InlineData(">")]
        [InlineData("/a")]
        [InlineData("/>")]
        [InlineData("=")]
        [InlineData("\"")]
        public void ReadValueDelegation_ForD9CharacterDataInContent_ItShould_BeACharacterDataCandidate(
            string text
        ) {
            // Arrange & Act
            Candidate candidate = Deconstruct(text, Context.Content, TokenType.Element);

            // Assert
            Assert.Equal(Candidate.CharacterData, candidate);
        }
        #endregion

        #region D10
        /// <summary>
        /// D10: no candidate in the context goes to the default delegate, which decides whether
        /// the markup is malformed - delegation does not
        /// </summary>
        [Theory]
        [InlineData("<!Dx", Context.Prolog, TokenType.None)]
        [InlineData("<!-x", Context.Content, TokenType.Element)]
        [InlineData("<![CDATA[x]]>", Context.Content, TokenType.Element)]
        [InlineData("&amp;", Context.Content, TokenType.Element)]
        [InlineData("a", Context.Prolog, TokenType.None)]
        [InlineData("</a>", Context.Prolog, TokenType.None)]
        [InlineData("<a/>", Context.Miscellaneous, TokenType.ElementEnd)]
        [InlineData("</a>", Context.Miscellaneous, TokenType.ElementEnd)]
        [InlineData("<!DOCTYPE root>", Context.Content, TokenType.Element)]
        [InlineData("<!DOCTYPE root>", Context.Miscellaneous, TokenType.ElementEnd)]
        [InlineData("=\"1\"", Context.StartTag, TokenType.Attribute)]
        [InlineData("<!-- comment -->", Context.StartTag, TokenType.Element)]
        [InlineData("<?pi?>", Context.StartTag, TokenType.Element)]
        [InlineData("<a>", Context.StartTag, TokenType.Element)]
        [InlineData("/>", Context.Prolog, TokenType.None)]
        public void ReadValueDelegation_ForD10NoCandidateInTheContext_ItShould_BeTheDefaultCandidate(
            string text,
            Context context,
            TokenType current
        ) {
            // Arrange & Act
            Candidate candidate = Deconstruct(text, context, current);

            // Assert
            Assert.Equal(Candidate.None, candidate);
        }
        #endregion

        #region #17's S4
        /// <summary>
        /// Partial scratch pads when reading is completed (#17's S4) go through the same table;
        /// the delegate they reach finds that no more markup will follow
        /// </summary>
        /// <remarks>
        /// The expected candidate is passed by name: Candidate is internal and can't be a
        /// parameter of a public theory
        /// </remarks>
        [Theory]
        [InlineData("<", Context.Prolog, TokenType.None, nameof(Candidate.None))]
        [InlineData("<!", Context.Prolog, TokenType.None, nameof(Candidate.None))]
        [InlineData("<!DOCTYP", Context.Prolog, TokenType.None, nameof(Candidate.None))]
        [InlineData("<!-", Context.Content, TokenType.Element, nameof(Candidate.None))]
        [InlineData("<?xml", Context.Prolog, TokenType.None, nameof(Candidate.ProcessingInstruction))]
        [InlineData("/", Context.Content, TokenType.Element, nameof(Candidate.CharacterData))]
        [InlineData("/", Context.StartTag, TokenType.Element, nameof(Candidate.None))]
        public void ReadValueDelegation_ForS4PartialScratchPadWhenReadingIsCompleted_ItShould_GoThroughTheSameTable(
            string text,
            Context context,
            TokenType current,
            string expected
        ) {
            // Arrange & Act
            Candidate candidate = Deconstruct(text, context, current, isReadingCompleted: true);

            // Assert
            Assert.Equal(expected, candidate.ToString());
        }
        #endregion

        #region #17's S3
        /// <summary>
        /// An incomplete scratch pad while reading is not completed (#17's S3) breaks fast
        /// returning false without delegating, telling the consumer to expand the segment(s)
        /// </summary>
        [Theory]
        [InlineData("<", Context.Prolog, TokenType.None)]
        [InlineData("<!DOC", Context.Prolog, TokenType.None)]
        [InlineData("<?xml", Context.Prolog, TokenType.None)]
        [InlineData("/", Context.StartTag, TokenType.Element)]
        public void ReadValueDelegation_ForS3IncompleteScratchPadWhenReadingIsNotCompleted_ItShould_BreakFastReturningFalse(
            string text,
            Context context,
            TokenType current
        ) {
            // Arrange
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: false, ContextState(context, current));

            // Act
            bool advancement = reader.ReadValue();

            // Assert
            Assert.False(advancement);
            Assert.Equal(0, reader.Position);
        }
        #endregion

        #region No side effects
        /// <summary>
        /// Deconstruction has no side effects on the reader's local fields, including the
        /// reader state, and leaves the scratch pad as peeked
        /// </summary>
        [Theory]
        [InlineData("<?xml version=\"1.0\"?>", Context.Prolog, TokenType.None)]  // D0
        [InlineData("<a>", Context.Content, TokenType.Element)]                  // D4
        [InlineData("\"1\"", Context.StartTag, TokenType.Attribute)]             // D8
        [InlineData("<!Dx", Context.Prolog, TokenType.None)]                     // D10
        public void ReadValueDelegation_ForAnyRow_ItShould_LeavePositionReaderStateAndScratchPadUnchanged(
            string text,
            Context context,
            TokenType current
        ) {
            // Arrange
            ReaderState state = ContextState(context, current);
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted: true, state);
            Assert.True(reader.PeekStartingTerminal());
            string scratchPad = ScratchPad(ref reader);

            // Act
            reader.DeconstructScratchPad();

            // Assert
            Assert.Equal(0, reader.Position);
            Assert.Equal(state._currentTokenType, reader.ReaderState._currentTokenType);
            Assert.Equal(state._previousTokenType, reader.ReaderState._previousTokenType);
            Assert.Equal(state._elementStack.Depth, reader.ReaderState._elementStack.Depth);
            Assert.Equal(state._elementStack.ContentReady, reader.ReaderState._elementStack.ContentReady);
            Assert.Equal(state._elementStack.RootElement, reader.ReaderState._elementStack.RootElement);
            Assert.Equal(scratchPad, ScratchPad(ref reader));
        }
        #endregion
    }
}
