using System.Buffers;
using System.Text;
using Xunit;
using Forestry.Xml.Reading;

namespace Forestry.Xml.Tests
{
    /// <summary>
    /// <see cref="ElementStack"/> - push/try-pop of whole element names for the WFC Element Type
    /// Match check, backed entirely by inline (non-allocating) storage: the open names end to end
    /// in a 2048-byte pool with the end offset of each (spec 001 research R4).
    /// </summary>
    /// <remarks>
    /// Spec 001 US2 (tasks.md T037, #66) shells, reviewed at T039: names are compared in full,
    /// <c>Push</c> became <c>TryPush</c> (false when a name does not fit), and <c>Pop()</c> drops
    /// the top name. They don't compile until T041.
    /// </remarks>
    public class ElementNameStackTests
    {
        #region helpers
        private static ReadOnlySpan<byte> Bytes(string name) => Encoding.UTF8.GetBytes(name);

        private sealed class Segment : ReadOnlySequenceSegment<byte>
        {
            public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;

            public Segment Append(ReadOnlyMemory<byte> memory)
            {
                Segment next = new(memory) { RunningIndex = RunningIndex + Memory.Length };
                Next = next;
                return next;
            }
        }

        /// <summary>
        /// The name as two segments split at a byte offset
        /// </summary>
        private static ReadOnlySequence<byte> SplitAt(string name, int split)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(name);
            Segment first = new(bytes.AsMemory(0, split));
            Segment last = first.Append(bytes.AsMemory(split));
            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }
        #endregion

        [Fact]
        public void TryPop_ForASingleMatchingName_ItShould_ReturnTrueAndPop()
        {
            // Arrange
            ElementStack stack = default;
            stack.TryPush(Bytes("Log"));

            // Act
            bool popped = stack.TryPop(Bytes("Log"));

            // Assert
            Assert.True(popped);
            Assert.Equal(0, stack.Depth);
        }

        [Fact]
        public void TryPop_ForAMismatchedName_ItShould_ReturnFalseWithoutPopping()
        {
            // Arrange - Element Type Match WFC violation: the ending tag's name doesn't match
            // the starting tag's. TryPop must not mutate the stack when it can't match - same
            // "peek, don't mutate on failure" contract as TryMatch/TrySkip.
            ElementStack stack = default;
            stack.TryPush(Bytes("Log"));

            // Act
            bool popped = stack.TryPop(Bytes("Wrong"));

            // Assert - still open, still poppable by its real name
            Assert.False(popped);
            Assert.Equal(1, stack.Depth);
            Assert.True(stack.TryPop(Bytes("Log")));
        }

        [Fact]
        public void TryPop_ForAnEmptyStack_ItShould_ReturnFalse()
        {
            // Arrange
            ElementStack stack = default;

            // Act
            bool popped = stack.TryPop(Bytes("Log"));

            // Assert
            Assert.False(popped);
        }

        [Fact]
        public void Depth_AfterPushingAndPopping_ItShould_TrackHowManyNamesAreCurrentlyOpen()
        {
            // Arrange
            ElementStack stack = default;

            // Act & Assert
            Assert.Equal(0, stack.Depth);

            stack.TryPush(Bytes("Log"));
            Assert.Equal(1, stack.Depth);

            stack.TryPush(Bytes("LogDiameter"));
            Assert.Equal(2, stack.Depth);

            Assert.True(stack.TryPop(Bytes("LogDiameter")));
            Assert.Equal(1, stack.Depth);

            Assert.True(stack.TryPop(Bytes("Log")));
            Assert.Equal(0, stack.Depth);
        }

        [Fact]
        public void PushThenTryPop_ForMultipleNestedNames_ItShould_PopInLastInFirstOutOrder()
        {
            // Arrange - <HarvestedProduction><Log><LogDiameter> ... nested three deep
            ElementStack stack = default;
            stack.TryPush(Bytes("HarvestedProduction"));
            stack.TryPush(Bytes("Log"));
            stack.TryPush(Bytes("LogDiameter"));

            // Act & Assert - innermost element closes first; the wrong LIFO order can't match
            Assert.False(stack.TryPop(Bytes("HarvestedProduction")));
            Assert.True(stack.TryPop(Bytes("LogDiameter")));
            Assert.True(stack.TryPop(Bytes("Log")));
            Assert.True(stack.TryPop(Bytes("HarvestedProduction")));
        }

        [Fact]
        public void Push_ForDifferentNamesAtDifferentDepths_ItShould_NotCorruptEachOthersBytes()
        {
            // Arrange - regression case for the pool itself: depth 0 and depth 1 must land in
            // separate bytes, not overlap.
            ElementStack stack = default;

            // Act
            stack.TryPush(Bytes("HarvestedProduction"));
            stack.TryPush(Bytes("Log"));

            // Assert
            Assert.True(stack.TryPop(Bytes("Log")));
            Assert.True(stack.TryPop(Bytes("HarvestedProduction")));
        }

        /// <summary>
        /// T037, FR-008: names are compared in full, so two names equal on their first 32 bytes
        /// and different after don't match (replaces the capped-prefix test of #23's tradeoff)
        /// </summary>
        [Fact]
        public void TryPop_ForNamesDifferentAfterThirtyTwoBytes_ItShould_NotMatch()
        {
            // Arrange
            ElementStack stack = default;
            string name = new string('A', 64);
            stack.TryPush(Bytes(name));

            // Act
            bool popped = stack.TryPop(Bytes(new string('A', 63) + "B"));

            // Assert
            Assert.False(popped);
            Assert.True(stack.TryPop(Bytes(name)));
        }

        /// <summary>
        /// T037, R4's worked layout: <c>_names: "ab" "Log" "LogDiameter"</c>, <c>_ends: 2 5 16</c>,
        /// <c>_depth: 3</c>; the top name is <c>LogDiameter</c>
        /// </summary>
        [Fact]
        public void TryPeek_ForTheWorkedLayout_ItShould_CompareTheTopNameInFull()
        {
            // Arrange
            ElementStack stack = default;
            stack.TryPush(Bytes("ab"));
            stack.TryPush(Bytes("Log"));
            stack.TryPush(Bytes("LogDiameter"));

            // Act, Assert
            Assert.Equal(3, stack.Depth);
            Assert.Equal(11, stack.TopLength);
            Assert.True(stack.TryPeek(Bytes("LogDiameter"), out _));
            Assert.False(stack.TryPeek(Bytes("LogDiameteX"), out _));
            Assert.False(stack.TryPeek(Bytes("Log"), out _));
        }

        /// <summary>
        /// T037: a name straddling segments is pushed and peeked as a sequence, at every split
        /// </summary>
        [Fact]
        public void TryPush_ForANameStraddlingSegments_ItShould_CompareInFull()
        {
            // Arrange
            const string name = "ButtEndProfileExtrapolationFunction";

            for (int split = 0; split <= name.Length; split++)
            {
                ElementStack stack = default;

                // Act
                bool pushed = stack.TryPush(SplitAt(name, split));

                // Assert
                Assert.True(pushed);
                Assert.Equal(name.Length, stack.TopLength);
                Assert.True(stack.TryPeek(SplitAt(name, name.Length - split), out _));
                Assert.False(stack.TryPeek(SplitAt(name + "X", split), out _));
                Assert.True(stack.TryPeek(Bytes(name), out _));
            }
        }

        /// <summary>
        /// T037: <c>Pop()</c> drops the top name, the one below becomes the top
        /// </summary>
        [Fact]
        public void Pop_ForAnOpenElement_ItShould_DropTheTopName()
        {
            // Arrange
            ElementStack stack = default;
            stack.TryPush(Bytes("Log"));
            stack.TryPush(Bytes("LogDiameter"));

            // Act
            stack.Pop();

            // Assert
            Assert.Equal(1, stack.Depth);
            Assert.Equal(3, stack.TopLength);
            Assert.True(stack.TryPeek(Bytes("Log"), out _));
        }

        [Fact]
        public void Pop_ForAnEmptyStack_ItShould_Throw()
        {
            // Arrange - calling Pop with nothing open is a caller bug, not a document
            // condition, so unlike TryPop it throws rather than returning something misleading
            // (and never makes the depth negative, R4).
            ElementStack stack = default;

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => stack.Pop());
        }

        /// <summary>
        /// T037, R4: a push that would pass the 2048 name bytes reports it does not fit and leaves
        /// the stack unchanged
        /// </summary>
        [Fact]
        public void TryPush_ForANameBeyondTheBytes_ItShould_ReturnFalseUnchanged()
        {
            // Arrange - 17 names of 120 bytes use 2040 bytes
            ElementStack stack = default;
            string name = new string('A', 120);
            for (int i = 0; i < 17; i++)
            {
                Assert.True(stack.TryPush(Bytes(name)));
            }

            // Act
            bool pushed = stack.TryPush(Bytes(name));

            // Assert
            Assert.False(pushed);
            Assert.Equal(17, stack.Depth);
            Assert.True(stack.TryPeek(Bytes(name), out _));
            Assert.True(stack.TryPush(Bytes("abcdefgh")));  // 2040 + 8 = 2048 still fits
        }

        /// <summary>
        /// T037, R4: a push beyond 64 levels reports it does not fit and leaves the stack unchanged
        /// </summary>
        [Fact]
        public void TryPush_ForASixtyFifthName_ItShould_ReturnFalseUnchanged()
        {
            // Arrange
            ElementStack stack = default;
            for (int i = 0; i < ElementStack.NonAllocatingMaxDepth; i++)
            {
                Assert.True(stack.TryPush(Bytes("a")));
            }

            // Act
            bool pushed = stack.TryPush(Bytes("a"));

            // Assert
            Assert.False(pushed);
            Assert.Equal(64, stack.Depth);
        }
    }
}
