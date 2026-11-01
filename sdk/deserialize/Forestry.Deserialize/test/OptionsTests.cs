using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Tests.Fixtures;
using Xunit;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Options own their definitions (tasks.md T027; later T054)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4). For the review gate (T029), the assumed surface:
    /// <c>DeserializeOptions.MakeReadOnly()</c> locks the options and builds their row type lookup (what the
    /// first deserialization does, R6), and the internal
    /// <c>DeserializeOptions.TryGetTypeDefinition(Type, out TypeDefinition?)</c> reads that lookup (R5).
    /// </remarks>
    public sealed class OptionsTests
    {
        // T027 - flow 1: the row type lookup is per options, never global (S0, R5)
        [Fact]
        public void TryGetTypeDefinition_ForARowAddedToOtherOptions_ItShould_NotFindIt()
        {
            TestingDeserializeOptions optionsA = new();
            optionsA.Rows.Add<Log>();
            optionsA.MakeReadOnly();

            TestingDeserializeOptions optionsB = new();
            optionsB.Rows.Add<LogWithParameters>();
            optionsB.MakeReadOnly();

            Assert.True(optionsA.TryGetTypeDefinition(typeof(Log), out TypeDefinition? definitionA));
            Assert.Equal(typeof(Log), definitionA!.Type);
            Assert.False(optionsB.TryGetTypeDefinition(typeof(Log), out _));
        }
    }
}
