using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    /// <summary>
    /// <see cref="ElementStack"/> - push/try-pop of packed element names for the WFC Element
    /// Type Match check, backed entirely by inline (non-allocating) storage for the common,
    /// non-deeply-nested case.
    /// </summary>
    public class ElementNameStackTests
    {
        [Fact]
        public void TryPop_ForASingleMatchingName_ItShould_ReturnTrueAndPop()
        {
            // Arrange
            ElementStack stack = default;
            stack.Push(Encoding.UTF8.GetBytes("Log"));

            // Act
            bool popped = stack.TryPop(Encoding.UTF8.GetBytes("Log"));

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
            stack.Push(Encoding.UTF8.GetBytes("Log"));

            // Act
            bool popped = stack.TryPop(Encoding.UTF8.GetBytes("Wrong"));

            // Assert - still open, still poppable by its real name
            Assert.False(popped);
            Assert.Equal(1, stack.Depth);
            Assert.True(stack.TryPop(Encoding.UTF8.GetBytes("Log")));
        }

        [Fact]
        public void TryPop_ForAnEmptyStack_ItShould_ReturnFalse()
        {
            // Arrange
            ElementStack stack = default;

            // Act
            bool popped = stack.TryPop(Encoding.UTF8.GetBytes("Log"));

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

            stack.Push(Encoding.UTF8.GetBytes("Log"));
            Assert.Equal(1, stack.Depth);

            stack.Push(Encoding.UTF8.GetBytes("LogDiameter"));
            Assert.Equal(2, stack.Depth);

            Assert.True(stack.TryPop(Encoding.UTF8.GetBytes("LogDiameter")));
            Assert.Equal(1, stack.Depth);

            Assert.True(stack.TryPop(Encoding.UTF8.GetBytes("Log")));
            Assert.Equal(0, stack.Depth);
        }

        [Fact]
        public void PushThenTryPop_ForMultipleNestedNames_ItShould_PopInLastInFirstOutOrder()
        {
            // Arrange - <HarvestedProduction><Log><LogDiameter> ... nested three deep
            ElementStack stack = default;
            stack.Push(Encoding.UTF8.GetBytes("HarvestedProduction"));
            stack.Push(Encoding.UTF8.GetBytes("Log"));
            stack.Push(Encoding.UTF8.GetBytes("LogDiameter"));

            // Act & Assert - innermost element closes first; the wrong LIFO order can't match
            Assert.False(stack.TryPop(Encoding.UTF8.GetBytes("HarvestedProduction")));
            Assert.True(stack.TryPop(Encoding.UTF8.GetBytes("LogDiameter")));
            Assert.True(stack.TryPop(Encoding.UTF8.GetBytes("Log")));
            Assert.True(stack.TryPop(Encoding.UTF8.GetBytes("HarvestedProduction")));
        }

        [Fact]
        public void Push_ForDifferentNamesAtDifferentDepths_ItShould_NotCorruptEachOthersPoolSlot()
        {
            // Arrange - regression case for the pool slicing itself: depth 0 and depth 1 must
            // land in genuinely separate slots, not overlap.
            ElementStack stack = default;

            // Act
            stack.Push(Encoding.UTF8.GetBytes("HarvestedProduction"));
            stack.Push(Encoding.UTF8.GetBytes("Log"));

            // Assert
            Assert.True(stack.TryPop(Encoding.UTF8.GetBytes("Log")));
            Assert.True(stack.TryPop(Encoding.UTF8.GetBytes("HarvestedProduction")));
        }

        [Fact]
        public void TryPop_ForANameLongerThanThePackedLength_ItShould_MatchOnTheCappedPrefix()
        {
            // Arrange - a name well beyond the 32-byte/4-ulong cap from #23's accepted tradeoff.
            // Two distinct names that only diverge after the cap are expected to be wrongly
            // treated as equal - that's the accepted tradeoff itself, not a bug in this test.
            ElementStack stack = default;
            string longName = new string('A', 64);

            // Act
            stack.Push(Encoding.UTF8.GetBytes(longName));

            // Assert
            Assert.True(stack.TryPop(Encoding.UTF8.GetBytes(longName)));
        }

        [Fact]
        public void Pop_ForAnOpenElement_ItShould_ReturnTheUnpackedNameAndDecrementDepth()
        {
            // Arrange
            ElementStack stack = default;
            stack.Push(Encoding.UTF8.GetBytes("Log"));
            byte[] buffer = new byte[ElementStack.PackedNameLength * 8];

            // Act
            int length = stack.Pop(buffer);

            // Assert
            Assert.Equal("Log", Encoding.UTF8.GetString(buffer, 0, length));
            Assert.Equal(0, stack.Depth);
        }

        [Fact]
        public void Pop_ForAnEmptyStack_ItShould_Throw()
        {
            // Arrange - calling Pop with nothing open is a caller bug, not a document
            // condition, so unlike TryPop it throws rather than returning something misleading.
            ElementStack stack = default;
            byte[] buffer = new byte[ElementStack.PackedNameLength * 8];

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => stack.Pop(buffer));
        }
    }
}
