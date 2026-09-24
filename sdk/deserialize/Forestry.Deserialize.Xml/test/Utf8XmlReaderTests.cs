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
    /// drained-check -> PeekStartingTerminal/ReadValue (#17/#27) -> set token (#25), each rebuilt
    /// as its own task rather than patched in place. Those scenarios (declaration/comment/PI
    /// round-trip, the xml-stylesheet-vs-declaration disambiguation, multi-comment prolog,
    /// self-closing element across two Read() calls, malformed-name/mismatched-end-tag throws)
    /// still need re-covering once #17/#25/#27 land - removed rather than left failing, not
    /// forgotten.
    ///
    /// One partial file per logical step of <c>Read()</c>, named after the step's method
    /// (<c>Utf8XmlReaderTests.ContentReady.cs</c>, <c>Utf8XmlReaderTests.PeekStartingTerminal.cs</c>).
    /// This file holds the shared helpers and the tests of <c>Read()</c>'s own wiring.
    /// </summary>
    public partial class Utf8XmlReaderTests
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

        // ---- Shared helpers ------------------------------------------------------------------

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

        // ---- Read() drained check after ContentReady(), #17 pre-condition --------------------
        //
        // Not shells for #17's own step: these cover Read()'s wiring, fixed after #17 moved to
        // Ready (see #17's Pre-conditions). S3 skips the '>' landing exactly at the end of the
        // segment, which must halt Read() before the starting terminal is peeked.

        /// <summary>
        /// State S3 when asserting content ready will skip the <c>&lt;</c> character 
        /// advancing the segment position forcing a drainage assertion to be called 
        /// only if reading is not completed
        /// </summary>
        [Fact]
        public void Read_ForS3DrainingTheSegmentWhenReadingNotCompleted_ItShould_ReturnFalseKeepingTheSkip()
        {
            // Arrange
            ReaderState state = State(ElementStackAtDepth(1, contentReady: false), TokenType.Element);
            Utf8XmlReader reader = new(">"u8, isReadingCompleted: false, state);

            // Act
            bool advancement = reader.Read();

            // Assert - the skipped '>' and content ready flag carry over to the next reader
            Assert.False(advancement);
            Assert.Equal(1, reader.Position);
            Assert.True(reader.ReaderState._elementStack.ContentReady);
        }

        /// <summary>
        /// State S3 when asserting content ready will skip the <c>&lt;</c> character 
        /// advancing the segment position but unable to assert drainage assertion 
        /// because reading is completed
        /// </summary>
        [Fact]
        public void Read_ForS3DrainingTheSegmentWhenReadingCompleted_ItShould_ThrowElementNotEnded()
        {
            // Arrange
            ReaderState state = State(ElementStackAtDepth(1, contentReady: false), TokenType.Element);
            Utf8XmlReader reader = new(">"u8, isReadingCompleted: true, state);

            // Act & Assert - ref struct, so try/catch rather than Assert.Throws. An
            // IndexOutOfRangeException here would mean the starting terminal was peeked past
            // the end of the segment.
            Exception? thrown = null;
            try
            {
                reader.Read();
            }
            catch (Exception exception)
            {
                thrown = exception;
            }

            Assert.IsType<XmlException>(thrown);
            Assert.Contains("element not ended", thrown.Message);
        }
    }
}
