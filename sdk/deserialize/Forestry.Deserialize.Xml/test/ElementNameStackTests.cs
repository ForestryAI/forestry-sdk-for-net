using System.Text;
using Forestry.Deserialize.Xml.Reading;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    /// <summary>
    /// <see cref="ElementNameStack"/> - push/pop of packed element names for the WFC Element Type
    /// Match check, backed entirely by inline (non-allocating) storage for the common,
    /// non-deeply-nested case.
    /// </summary>
    public class ElementNameStackTests
    {
        private static ulong[] Packed(string name)
        {
            ulong[] destination = new ulong[ElementNameStack.PackedNameLength];
            ((ReadOnlySpan<byte>)Encoding.UTF8.GetBytes(name)).Pack(destination);
            return destination;
        }

        [Fact]
        public void PushThenPop_ForASingleName_ItShould_RoundTripTheSamePackedValue()
        {
            // Arrange
            ElementNameStack stack = default;
            ulong[] expected = Packed("Log");

            // Act
            stack.Push(Encoding.UTF8.GetBytes("Log"));
            ReadOnlySpan<ulong> popped = stack.Pop();

            // Assert
            Assert.True(popped.SequenceEqual(expected));
        }

        [Fact]
        public void Depth_AfterPushingAndPopping_ItShould_TrackHowManyNamesAreCurrentlyOpen()
        {
            // Arrange
            ElementNameStack stack = default;

            // Act & Assert
            Assert.Equal(0, stack.Depth);

            stack.Push(Encoding.UTF8.GetBytes("Log"));
            Assert.Equal(1, stack.Depth);

            stack.Push(Encoding.UTF8.GetBytes("LogDiameter"));
            Assert.Equal(2, stack.Depth);

            stack.Pop();
            Assert.Equal(1, stack.Depth);

            stack.Pop();
            Assert.Equal(0, stack.Depth);
        }

        [Fact]
        public void PushThenPop_ForMultipleNestedNames_ItShould_PopInLastInFirstOutOrder()
        {
            // Arrange - <HarvestedProduction><Log><LogDiameter> ... nested three deep
            ElementNameStack stack = default;
            stack.Push(Encoding.UTF8.GetBytes("HarvestedProduction"));
            stack.Push(Encoding.UTF8.GetBytes("Log"));
            stack.Push(Encoding.UTF8.GetBytes("LogDiameter"));

            // Act & Assert - innermost element closes first
            Assert.True(stack.Pop().SequenceEqual(Packed("LogDiameter")));
            Assert.True(stack.Pop().SequenceEqual(Packed("Log")));
            Assert.True(stack.Pop().SequenceEqual(Packed("HarvestedProduction")));
        }

        [Fact]
        public void Push_ForDifferentNamesAtDifferentDepths_ItShould_NotCorruptEachOthersPoolSlot()
        {
            // Arrange - regression case for the pool slicing itself: depth 0 and depth 1 must
            // land in genuinely separate slots, not overlap.
            ElementNameStack stack = default;

            // Act
            stack.Push(Encoding.UTF8.GetBytes("HarvestedProduction"));
            stack.Push(Encoding.UTF8.GetBytes("Log"));

            ReadOnlySpan<ulong> inner = stack.Pop(); // "Log"
            ReadOnlySpan<ulong> outer = stack.Pop(); // "HarvestedProduction"

            // Assert
            Assert.True(inner.SequenceEqual(Packed("Log")));
            Assert.True(outer.SequenceEqual(Packed("HarvestedProduction")));
        }

        [Fact]
        public void Push_ForANameLongerThanThePackedLength_ItShould_BeCappedNotThrow()
        {
            // Arrange - a name well beyond the 32-byte/4-ulong cap from #23's accepted tradeoff.
            ElementNameStack stack = default;
            string longName = new string('A', 64);

            // Act
            stack.Push(Encoding.UTF8.GetBytes(longName));
            ReadOnlySpan<ulong> popped = stack.Pop();

            // Assert - matches whatever Packed() (also capped by Pack itself) produces for the
            // same over-length input, proving the stack's own cap is consistent with Pack's.
            Assert.True(popped.SequenceEqual(Packed(longName)));
        }
    }
}
