using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    /// <summary>
    /// Coverage of <see cref="Utf8XmlReader"/>. Mostly empty as of the #20/#28 redesign - the
    /// previous end-to-end coverage (Declaration/Comment/ProcessInstruction/Element reading via
    /// the old ReadDocument/ReadProlog/opaque-value pipeline) tested a dispatch chain that no
    /// longer exists: <c>Read()</c> is now drained-check -> spacing-skip -> ContentReady (#22) ->
    /// ReadStartingTerminal/ReadValue (#17/#27) -> set token (#25), each rebuilt as its own task
    /// rather than patched in place. Those scenarios (declaration/comment/PI round-trip, the
    /// xml-stylesheet-vs-declaration disambiguation, multi-comment prolog, self-closing element
    /// across two Read() calls, malformed-name/mismatched-end-tag throws) still need re-covering
    /// once #17/#25/#27 land - removed rather than left failing, not forgotten.
    ///
    /// The <c>ContentReady_*</c> tests below are shells written from #22's architecture text
    /// alone (state table S0-S7, the guard, and the Debug.Assert fall-through), before
    /// ContentReady()'s body exists - see doc/dev/Velocity.md's Test shell phase.
    /// </summary>
    public class Utf8XmlReaderTests
    {
        [Fact]
        public void ReaderState_ForAFreshDefaultInstance_ItShould_HaveNothingOpen()
        {
            // Arrange & Act - a fresh ReaderState represents "nothing read yet". The explicit
            // bare ReaderState() constructor (forwarding to the real one) is what makes this
            // safe to call with no arguments at all - without it, a struct's implicit
            // parameterless constructor would zero-initialize every field instead.
            ReaderState state = new();

            // Assert
            Assert.Equal(0, state._elementStack.Depth);
        }

        // ---- ContentReady() shells, from #22 -------------------------------------------------
        //
        // ContentReady() is currently `private`; these assume it becomes `internal` (the "test
        // seam" needed since Read() can't drive this step in isolation until #17 exists) - won't
        // compile until that one-line accessibility change lands.
        //
        // Every reader below is built directly from an internal ReaderState so each row's exact
        // precondition (depth, content-ready flag, current/previous token, root element) is
        // reachable without a real document producing it - matching #22's Pre-conditions section:
        // segment not drained, spacing already skipped, positioned right at the byte under test.
        // A 1-byte source is enough since ContentReady() only ever looks at one byte.

        /// <summary>
        /// Builds an <see cref="ElementStack"/> at the given depth. When <paramref name="contentReady"/>
        /// is true, reaches it via Push then TryPop of a synthetic child - TryPop is the only
        /// existing way to observe ContentReady == true (there's no direct setter; push/pop
        /// semantics are explicitly out of #22's scope, this only borrows an already-implemented
        /// side effect of TryPop, not asserting anything new about it).
        /// </summary>
        private static ElementStack ElementStackAtDepth(int depth, bool contentReady)
        {
            ElementStack stack = default;
            for (int i = 0; i < depth; i++)
            {
                stack.Push(Encoding.UTF8.GetBytes($"E{i}"));
            }

            if (contentReady)
            {
                stack.Push(Encoding.UTF8.GetBytes("Child"));
                stack.TryPop(Encoding.UTF8.GetBytes("Child"));
            }

            return stack;
        }

        /// <summary>
        /// Depth 0, RootElement still false - the state before any element has ever been pushed
        /// (S0/S1's shared precondition).
        /// </summary>
        private static ElementStack ElementStackBeforeAnyElement() => default;

        /// <summary>
        /// Depth 0, but RootElement true - the root was pushed then unconditionally popped back
        /// closed (S2's precondition: trailing miscellaneous after the document's one element).
        /// </summary>
        private static ElementStack ElementStackAfterRootClosed()
        {
            ElementStack stack = default;
            stack.Push(Encoding.UTF8.GetBytes("Root"));
            stack.Pop(stackalloc byte[ElementStack.PackedNameLength * 8]);
            return stack;
        }

        private static ReaderState State(
            ElementStack elementStack,
            TokenType current,
            TokenType previous = TokenType.None) => new(
            lineNumber: 0,
            linePosition: 0,
            currentTokenType: current,
            previousTokenType: previous,
            elementStack: elementStack,
            readerOptions: default
        );

        [Fact]
        public void ContentReady_ForANonGreaterThanCharacter_ItShould_BreakFastWithoutChangingPositionOrFlag()
        {
            // Arrange - S3's own precondition (Depth != 0, flag false, current Element), which
            // WOULD skip-and-set if the byte were '>' - proves the guard runs before state is
            // even consulted, not just that this particular row happens to be a no-op.
            ReaderState state = State(ElementStackAtDepth(1, contentReady: false), TokenType.Element);
            Utf8XmlReader reader = new("a"u8, isReadingCompleted: true, state);

            // Act
            reader.ContentReady();

            // Assert
            Assert.Equal(0, reader.Position);
            Assert.Equal(state._elementStack.ContentReady, reader.ReaderState._elementStack.ContentReady);
        }

        /// <summary>
        /// S0 means advancement to either the prolog or element non-terminals 
        /// is possible i.e. Token Type == None.  The '>' character 
        /// is ignored because the document is malformed.
        /// </summary>
        [Fact]
        public void ContentReady_ForS0StartOfDocument_ItShould_LeaveTheCharacterForTheStartingTerminalStep()
        {
            // Arrange
            ReaderState state = State(ElementStackBeforeAnyElement(), TokenType.None);
            Utf8XmlReader reader = new(">"u8, isReadingCompleted: true, state);

            // Act
            reader.ContentReady();

            // Assert
            Assert.Equal(0, reader.Position);
            Assert.Equal(state._elementStack.ContentReady, reader.ReaderState._elementStack.ContentReady);
        }

        /// <summary>
        /// S1 means advancement only to the prolog non-terminal when no root element 
        /// non-terminal exists.  The '>' character is ignored because the document is malformed 
        /// because the preceding non-terminal's own terminator was already consumed as part 
        /// of reading it as one opaque value.
        /// </summary>
        [Fact]
        public void ContentReady_ForS1BeforeRootWithPriorProlog_ItShould_LeaveTheCharacterForTheStartingTerminalStep()
        {
            // Arrange 
            ReaderState state = State(ElementStackBeforeAnyElement(), TokenType.Comment);
            Utf8XmlReader reader = new(">"u8, isReadingCompleted: true, state);

            // Act
            reader.ContentReady();

            // Assert
            Assert.Equal(0, reader.Position);
            Assert.Equal(state._elementStack.ContentReady, reader.ReaderState._elementStack.ContentReady);
        }

        /// <summary>
        /// S2 means advancement after the root element in the miscellaneous non-terminal. 
        /// The '>' character is ignored because the document is malformed 
        /// because the preceding non-terminal's own terminator was already consumed as part 
        /// of reading it as one opaque value.
        /// </summary>
        [Fact]
        public void ContentReady_ForS2AfterRoot_ItShould_LeaveTheCharacterForTheStartingTerminalStep()
        {
            // Arrange
            ReaderState state = State(ElementStackAfterRootClosed(), TokenType.Comment);
            Utf8XmlReader reader = new(">"u8, isReadingCompleted: true, state);

            // Act
            reader.ContentReady();

            // Assert
            Assert.Equal(0, reader.Position);
            Assert.Equal(state._elementStack.ContentReady, reader.ReaderState._elementStack.ContentReady);
        }

        /// <summary>
        /// S3 means advancement to the ending terminal of a start tag.  The '>' character
        /// with the current token == Element is an ending terminal closing the start 
        /// tag.  The segment position advances past the '>' character setting the 
        /// markup as content ready i.e. the element non-terminal structure is the 
        /// start tag, content and end tag.
        /// </summary>
        [Fact]
        public void ContentReady_ForS3AfterElementName_ItShould_SkipAndSetContentReady()
        {
            // Arrange
            ReaderState state = State(ElementStackAtDepth(1, contentReady: false), TokenType.Element);
            Utf8XmlReader reader = new(">"u8, isReadingCompleted: true, state);

            // Act
            reader.ContentReady();

            // Assert 
            Assert.Equal(1, reader.Position);
            Assert.True(reader.ReaderState._elementStack.ContentReady);
        }

        /// <summary>
        /// S4 means that the attribute non-terminal in the start tag is missing 
        /// the attribute value non-terminal.  The '>' character is ignored because 
        /// the document is malformed.
        /// </summary>
        [Fact]
        public void ContentReady_ForS4AfterAttributeName_ItShould_LeaveTheCharacterForTheStartingTerminalStep()
        {
            // Arrange
            ReaderState state = State(ElementStackAtDepth(1, contentReady: false), TokenType.Attribute);
            Utf8XmlReader reader = new(">"u8, isReadingCompleted: true, state);

            // Act
            reader.ContentReady();

            // Assert
            Assert.Equal(0, reader.Position);
            Assert.Equal(state._elementStack.ContentReady, reader.ReaderState._elementStack.ContentReady);
        }

        /// <summary>
        /// S5 means that the attribute non-terminal in the start tag is NOT missing 
        /// the attribute value non-terminal.  The segment position advances past the '>' character setting the 
        /// markup as content ready i.e. the element non-terminal structure is the 
        /// start tag, content and end tag.
        /// </summary>
        [Fact]
        public void ContentReady_ForS5AfterAttributeValue_ItShould_SkipAndSetContentReady()
        {
            // Arrange
            ReaderState state = State(
                ElementStackAtDepth(1, contentReady: false), TokenType.Value, previous: TokenType.Attribute);
            Utf8XmlReader reader = new(">"u8, isReadingCompleted: true, state);

            // Act
            reader.ContentReady();

            // Assert
            Assert.Equal(1, reader.Position);
            Assert.True(reader.ReaderState._elementStack.ContentReady);
        }

        /// <summary>
        /// S6 means non-terminal is likely an empty element.  The '>' character is ignored 
        /// because following read steps are responsible for the '/>' ending terminal.
        /// </summary>
        /// <param name="previous"></param>
        [Theory]
        [InlineData(TokenType.Element)]
        [InlineData(TokenType.Value)]
        public void ContentReady_ForS6EmptyElementEndingTag_ItShould_LeaveTheCharacterForTheStartingTerminalStep(
            TokenType previous)
        {
            // Arrange
            ReaderState state = State(ElementStackAtDepth(1, contentReady: false), TokenType.Value, previous);
            Utf8XmlReader reader = new(">"u8, isReadingCompleted: true, state);

            // Act
            reader.ContentReady();

            // Assert
            Assert.Equal(0, reader.Position);
            Assert.Equal(state._elementStack.ContentReady, reader.ReaderState._elementStack.ContentReady);
        }

        /// <summary>
        /// S7 means that the markup is content ready and the '>' character is likely 
        /// character data.  The '>' character is ignored because the document is 
        /// well-formed.
        /// </summary>
        [Fact]
        public void ContentReady_ForS7AlreadyContentReady_ItShould_LeaveTheCharacterForTheStartingTerminalStep()
        {
            // Arrange
            ReaderState state = State(ElementStackAtDepth(1, contentReady: true), TokenType.Element);
            Utf8XmlReader reader = new(">"u8, isReadingCompleted: true, state);

            // Act
            reader.ContentReady();

            // Assert
            Assert.Equal(0, reader.Position);
            Assert.True(reader.ReaderState._elementStack.ContentReady);
        }

        /// <summary>
        /// When no assertions are possible i.e. no state between S0 - S7 exists then 
        /// a Debug Assert is caught. 
        /// </summary>
        [Fact]
        public void ContentReady_ForAStateCombinationNoRowCovers_ItShould_TriggerTheDebugAssert()
        {
            // Arrange
            ReaderState state = State(ElementStackAtDepth(1, contentReady: false), TokenType.ElementEnd);
            Utf8XmlReader reader = new(">"u8, isReadingCompleted: true, state);

            // Act & Assert - reader is a ref struct, so it can't be captured by Assert.Throws'
            // lambda; a plain try/catch is the only option, matching this file's existing pattern
            // for other expected-throw cases. Catching the base Exception type, not something more
            // specific: a failed Debug.Assert under `dotnet test` is translated to
            // Microsoft.VisualStudio.TestPlatform.TestHost.DebugAssertException (verified
            // empirically - it does NOT crash the whole test process the way a raw Debug.Assert
            // failure would outside a test host), but that type is internal to the test host's
            // own assembly and isn't accessible from here.
            bool threw = false;
            try
            {
                reader.ContentReady();
            }
            catch (Exception)
            {
                threw = true;
            }

            Assert.True(threw);
        }
    }
}
