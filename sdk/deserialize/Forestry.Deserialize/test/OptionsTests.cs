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
    /// For the review gate T055 (T054), also <c>options.Policies.Add&lt;TRow&gt;(policy)</c> (research R19);
    /// a change after the options are locked throws <see cref="InvalidOperationException"/> (FR-013).
    /// </remarks>
    public sealed class OptionsTests
    {
        // T027 - flow 1: the row type lookup is per options, never global (S0, R5)
        [Fact]
        public void TryGetTypeDefinition_ForARowAddedToOtherOptions_ItShould_NotFindIt()
        {
            // Arrange
            TestingDeserializeOptions optionsA = new();
            optionsA.Rows.Add<Log>();
            optionsA.MakeReadOnly();

            TestingDeserializeOptions optionsB = new();
            optionsB.Rows.Add<Stem>();
            optionsB.MakeReadOnly();

            // Act + Assert
            Assert.True(optionsA.TryGetTypeDefinition(typeof(Log), out TypeDefinition? definitionA));
            Assert.Equal(typeof(Log), definitionA!.Type);
            Assert.False(optionsB.TryGetTypeDefinition(typeof(Log), out _));
        }

        // T054 - quickstart 11 / US4-4 / FR-013: adding a policy after first use throws
        [Fact]
        public void PoliciesAdd_AfterTheOptionsAreLocked_ItShould_Throw()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Rows.Add<Log>();
            options.MakeReadOnly();

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => options.Policies.Add<Log>(new SetMarkerPolicy()));
        }

        // T054 - quickstart 11 / US4-4 / FR-013: adding a row type after first use throws
        [Fact]
        public void RowsAdd_AfterTheOptionsAreLocked_ItShould_Throw()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Rows.Add<Log>();
            options.MakeReadOnly();

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => options.Rows.Add<Stem>());
        }

        // T054 - quickstart 11 / US4-4 / FR-013: adding a converter after first use throws
        [Fact]
        public void ConvertersAdd_AfterTheOptionsAreLocked_ItShould_Throw()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Rows.Add<Log>();
            options.MakeReadOnly();

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => options.Converters.Add(new MarkingInt32Converter()));
        }
    }
}
