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
        // #27's architecture text alone (context table, delegation table S0-S9, requirements),
        // before delegation's body exists - see doc/dev/Velocity.md's Test shell phase.
        //
        // Test seam, settled during Understanding (src/Reading/CandidateNonTerminal.cs):
        // - `internal enum NonTerminal { None, Declaration, DocumentType, Comment,
        //   ProcessingInstruction, StartTag, EndTag, Attribute, AttributeValue, CharacterData }`,
        //   where None means the default delegate (S9) and StartTag is a known break from the
        //   EBNF (an empty element begins exactly like a start tag)
        // - `internal readonly struct CandidateNonTerminal` whose constructor
        //   `CandidateNonTerminal(ReadOnlySpan<byte> scratchPad, in ElementStack elementStack,
        //   TokenType currentTokenType)` deconstructs the scratch pad into `NonTerminal
        //   NonTerminal`. Taking the element stack lets the struct derive the context itself, so
        //   these shells cover that derivation too.
        // - ReadValue() becoming `internal`
        //
        // The delegates are separate backlog tasks, so no shell asserts that a delegate was
        // called; the candidate is the observable. Each reader is built from an internal
        // ReaderState so every context is reachable without a real document producing it, and
        // the scratch pad is filled by the real PeekStartingTerminal() (#17) before the
        // CandidateNonTerminal is constructed from it. Straddling segments
        // is not repeated here: the scratch pad is already filled when delegation runs, and #17
        // covers straddling.
        //
        // A bare Sn is #27's delegation state; #17's peek states are always written "#17's Sn"
        // and their test names carry no number, so the two never collide.

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
        /// Peek the starting terminal of the text into the scratch pad, then construct the
        /// candidate from the scratch pad and the reader state
        /// </summary>
        private static CandidateNonTerminal PeekCandidate(
            string text,
            Context context,
            TokenType current,
            bool isReadingCompleted = true
        ) {
            ReaderState state = ContextState(context, current);
            Utf8XmlReader reader = new(Encoding.UTF8.GetBytes(text), isReadingCompleted, state);
            Assert.True(reader.PeekStartingTerminal());

            ReadOnlySpan<byte> scratchPad = reader._startingTerminals;
            return new CandidateNonTerminal(scratchPad[..reader._startingTerminalCharacterCount], in state._elementStack, state._currentTokenType);
        }
        #endregion

        #region S0
        /// <summary>
        /// S0: <c>&lt;?xml</c> followed by any <c>S</c> (space, tab, CR, LF) is the declaration
        /// only in the prolog before any token, i.e. <c>VersionInfo ::= S 'version' ...</c>
        /// </summary>
        /// <remarks>
        /// <c>S</c> only separates the declaration from a processing instruction.  The declaration
        /// is an opaque value: its delegate only looks for the ending terminal <c>?&gt;</c>, so
        /// nothing after <c>S</c> (e.g. <c>VersionInfo</c>) is ever checked.
        /// </remarks>
        [Theory]
        [InlineData("<?xml version=\"1.0\"?>")]
        [InlineData("<?xml\tversion=\"1.0\"?>")]
        [InlineData("<?xml\rversion=\"1.0\"?>")]
        [InlineData("<?xml\nversion=\"1.0\"?>")]
        public void ReadValueDelegation_ForS0DeclarationFirstInTheProlog_ItShould_BeADeclarationCandidate(
            string text
        ) {
            // Arrange & Act
            CandidateNonTerminal candidate = PeekCandidate(text, Context.Prolog, TokenType.None);

            // Assert
            Assert.Equal(NonTerminal.Declaration, candidate.NonTerminal);
        }
        #endregion

        #region S1
        /// <summary>
        /// S1: <c>&lt;!DOCTYPE</c> in the prolog is the document type.  Its position within the
        /// prolog (e.g. a second document type) is the delegate's concern.
        /// </summary>
        [Theory]
        [InlineData(TokenType.None)]
        [InlineData(TokenType.Declaration)]
        [InlineData(TokenType.Comment)]
        [InlineData(TokenType.DocumentType)]
        public void ReadValueDelegation_ForS1DocumentTypeInTheProlog_ItShould_BeADocumentTypeCandidate(
            TokenType current
        ) {
            // Arrange & Act
            CandidateNonTerminal candidate = PeekCandidate("<!DOCTYPE root>", Context.Prolog, current);

            // Assert
            Assert.Equal(NonTerminal.DocumentType, candidate.NonTerminal);
        }
        #endregion

        #region S2
        /// <summary>
        /// S2: <c>&lt;!--</c> is a comment in the prolog, content and miscellaneous
        /// </summary>
        [Theory]
        [InlineData(Context.Prolog, TokenType.None)]
        [InlineData(Context.Content, TokenType.Element)]
        [InlineData(Context.Miscellaneous, TokenType.ElementEnd)]
        public void ReadValueDelegation_ForS2Comment_ItShould_BeACommentCandidate(
            Context context,
            TokenType current
        ) {
            // Arrange & Act
            CandidateNonTerminal candidate = PeekCandidate("<!-- comment -->", context, current);

            // Assert
            Assert.Equal(NonTerminal.Comment, candidate.NonTerminal);
        }
        #endregion

        #region S3
        /// <summary>
        /// S3: <c>&lt;?</c> with any extra is a processing instruction in the prolog, content and
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
        public void ReadValueDelegation_ForS3ProcessingInstruction_ItShould_BeAProcessingInstructionCandidate(
            string text,
            Context context,
            TokenType current
        ) {
            // Arrange & Act
            CandidateNonTerminal candidate = PeekCandidate(text, context, current);

            // Assert
            Assert.Equal(NonTerminal.ProcessingInstruction, candidate.NonTerminal);
        }
        #endregion

        #region S4
        /// <summary>
        /// S4: <c>&lt;</c> with a <c>NameStartChar</c> extra is a start tag in the prolog
        /// (the root) and content, including an empty element, which begins exactly like a
        /// start tag (a known break from the EBNF)
        /// </summary>
        [Theory]
        [InlineData("<a>", Context.Prolog, TokenType.None)]
        [InlineData("<_a/>", Context.Prolog, TokenType.Comment)]
        [InlineData("<:a>", Context.Content, TokenType.Element)]
        [InlineData("<A>", Context.Content, TokenType.Value)]
        public void ReadValueDelegation_ForS4StartTag_ItShould_BeAStartTagCandidate(
            string text,
            Context context,
            TokenType current
        ) {
            // Arrange & Act
            CandidateNonTerminal candidate = PeekCandidate(text, context, current);

            // Assert
            Assert.Equal(NonTerminal.StartTag, candidate.NonTerminal);
        }
        #endregion

        #region S5
        /// <summary>
        /// S5: <c>&lt;/</c> in content is an end tag
        /// </summary>
        [Theory]
        [InlineData(TokenType.Element)]
        [InlineData(TokenType.Value)]
        [InlineData(TokenType.ElementEnd)]
        public void ReadValueDelegation_ForS5EndTagInContent_ItShould_BeAnEndTagCandidate(
            TokenType current
        ) {
            // Arrange & Act
            CandidateNonTerminal candidate = PeekCandidate("</a>", Context.Content, current);

            // Assert
            Assert.Equal(NonTerminal.EndTag, candidate.NonTerminal);
        }
        #endregion

        #region S6
        /// <summary>
        /// S6: an empty scratch pad (#25) in a start tag is an attribute, which has no starting
        /// terminal.  The steps before delegation consume every other well-formed character
        /// (spacing, <c>&gt;</c>, <c>Eq</c> and <c>/&gt;</c>), so an attribute is the only
        /// well-formed possibility.
        /// </summary>
        [Theory]
        [InlineData("b=\"1\"", TokenType.Element)]
        [InlineData("_b=\"1\"", TokenType.Value)]
        [InlineData(":b=\"1\"", TokenType.Element)]
        [InlineData("=\"1\"", TokenType.Attribute)]  // malformed, the attribute delegate throws
        [InlineData("/x", TokenType.Element)]          // malformed, the attribute delegate throws
        [InlineData("/>", TokenType.Element)]          // consumed before delegation by the /> step
        public void ReadValueDelegation_ForS6AttributeInAStartTag_ItShould_BeAnAttributeCandidate(
            string text,
            TokenType current
        ) {
            // Arrange & Act
            CandidateNonTerminal candidate = PeekCandidate(text, Context.StartTag, current);

            // Assert
            Assert.Equal(NonTerminal.Attribute, candidate.NonTerminal);
        }
        #endregion

        #region S7
        /// <summary>
        /// S7: a quote in a start tag is <c>AttValue</c>'s starting terminal, reached after the
        /// Skip Delimiting Terminals step (separate backlog task) has skipped <c>Eq</c>
        /// </summary>
        [Theory]
        [InlineData("\"1\"")]
        [InlineData("'1'")]
        public void ReadValueDelegation_ForS7AttributeValueInAStartTag_ItShould_BeAnAttributeValueCandidate(
            string text
        ) {
            // Arrange & Act
            CandidateNonTerminal candidate = PeekCandidate(text, Context.StartTag, TokenType.Attribute);

            // Assert
            Assert.Equal(NonTerminal.AttributeValue, candidate.NonTerminal);
        }
        #endregion

        #region S8
        /// <summary>
        /// S8: in content an empty scratch pad (#25) or a quote is character data, which has no
        /// starting terminal.  <c>&amp;</c> also leaves an empty scratch pad: the character data
        /// delegate rejects it, i.e. <c>CharData ::= [^&lt;&amp;]*</c>.
        /// </summary>
        [Theory]
        [InlineData("a")]
        [InlineData(">")]
        [InlineData("/a")]
        [InlineData("/>")]
        [InlineData("=")]
        [InlineData("\"")]
        [InlineData("'")]
        [InlineData("&amp;")]  // malformed, the character data delegate throws
        public void ReadValueDelegation_ForS8CharacterDataInContent_ItShould_BeACharacterDataCandidate(
            string text
        ) {
            // Arrange & Act
            CandidateNonTerminal candidate = PeekCandidate(text, Context.Content, TokenType.Element);

            // Assert
            Assert.Equal(NonTerminal.CharacterData, candidate.NonTerminal);
        }
        #endregion

        #region S9
        /// <summary>
        /// S9: no candidate in the context goes to the default delegate, which decides whether
        /// the markup is malformed - delegation does not
        /// </summary>
        [Theory]
        [InlineData("<!Dx", Context.Prolog, TokenType.None)]
        [InlineData("<!-x", Context.Content, TokenType.Element)]
        [InlineData("<![CDATA[x]]>", Context.Content, TokenType.Element)]
        [InlineData("a", Context.Prolog, TokenType.None)]
        [InlineData("</a>", Context.Prolog, TokenType.None)]
        [InlineData("<a/>", Context.Miscellaneous, TokenType.ElementEnd)]
        [InlineData("</a>", Context.Miscellaneous, TokenType.ElementEnd)]
        [InlineData("<!DOCTYPE root>", Context.Content, TokenType.Element)]
        [InlineData("<!DOCTYPE root>", Context.Miscellaneous, TokenType.ElementEnd)]
        [InlineData("<!-- comment -->", Context.StartTag, TokenType.Element)]
        [InlineData("<?pi?>", Context.StartTag, TokenType.Element)]
        [InlineData("<a>", Context.StartTag, TokenType.Element)]
        [InlineData("/>", Context.Prolog, TokenType.None)]
        [InlineData("\"1\"", Context.Prolog, TokenType.None)]
        [InlineData("a", Context.Miscellaneous, TokenType.ElementEnd)]
        public void ReadValueDelegation_ForS9NoCandidateInTheContext_ItShould_BeTheDefaultCandidate(
            string text,
            Context context,
            TokenType current
        ) {
            // Arrange & Act
            CandidateNonTerminal candidate = PeekCandidate(text, context, current);

            // Assert
            Assert.Equal(NonTerminal.None, candidate.NonTerminal);
        }
        #endregion

        #region #17's S4
        /// <summary>
        /// Partial scratch pads when reading is completed (#17's S4) go through the same table;
        /// the delegate they reach finds that no more markup will follow
        /// </summary>
        /// <remarks>
        /// The expected candidate non-terminal is passed by name: NonTerminal is internal and can't be
        /// a parameter of a public theory
        /// </remarks>
        [Theory]
        [InlineData("<", Context.Prolog, TokenType.None, nameof(NonTerminal.None))]
        [InlineData("<!", Context.Prolog, TokenType.None, nameof(NonTerminal.None))]
        [InlineData("<!DOCTYP", Context.Prolog, TokenType.None, nameof(NonTerminal.None))]
        [InlineData("<!-", Context.Content, TokenType.Element, nameof(NonTerminal.None))]
        [InlineData("<?xml", Context.Prolog, TokenType.None, nameof(NonTerminal.ProcessingInstruction))]
        public void ReadValueDelegation_ForAPartialScratchPadWhenReadingIsCompleted_ItShould_GoThroughTheSameTable(
            string text,
            Context context,
            TokenType current,
            string expected
        ) {
            // Arrange & Act
            CandidateNonTerminal candidate = PeekCandidate(text, context, current, isReadingCompleted: true);

            // Assert
            Assert.Equal(expected, candidate.NonTerminal.ToString());
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
        public void ReadValueDelegation_ForAnIncompleteScratchPadWhenReadingIsNotCompleted_ItShould_BreakFastReturningFalse(
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
        /// Constructing the candidate has no side effects on the reader's local fields, including
        /// the reader state, and leaves the scratch pad as peeked
        /// </summary>
        /// <remarks>
        /// Mostly true by construction - the candidate only reads a span and a copy of the reader
        /// state - but it keeps the requirement visible once ReadValue() constructs it
        /// </remarks>
        [Theory]
        [InlineData("<?xml version=\"1.0\"?>", Context.Prolog, TokenType.None)]  // S0
        [InlineData("<a>", Context.Content, TokenType.Element)]                  // S4
        [InlineData("\"1\"", Context.StartTag, TokenType.Attribute)]             // S7
        [InlineData("<!Dx", Context.Prolog, TokenType.None)]                     // S9
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
            ReadOnlySpan<byte> peeked = reader._startingTerminals;
            _ = new CandidateNonTerminal(peeked[..reader._startingTerminalCharacterCount], in state._elementStack, state._currentTokenType);

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
