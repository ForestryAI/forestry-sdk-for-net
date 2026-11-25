using Forestry.Serialization.Definitions;
using Forestry.Serialization.Tests.Fixtures;
using Xunit;

namespace Forestry.Serialization.Tests
{
    /// <summary>
    /// Options own their definitions (tasks.md T027; later T054)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4). For the review gate (T029), the assumed surface:
    /// <c>DeserializeOptions.MakeReadOnly()</c> locks the options and builds their telemetry type lookup (what the
    /// first deserialization does, R6), and the internal
    /// <c>DeserializeOptions.TryGetTypeDefinition(Type, out TypeDefinition?)</c> reads that lookup (R5).
    /// For the review gate T055 (T054), also <c>options.Policies.Add&lt;TTelemetry&gt;(policy)</c> (research R19);
    /// a change after the options are locked throws <see cref="InvalidOperationException"/> (FR-013).
    /// </remarks>
    public sealed class OptionsTests
    {
        // T027 - flow 1: the telemetry type lookup is per options, never global (S0, R5)
        [Fact]
        public void TryGetTypeDefinition_ForATelemetryAddedToOtherOptions_ItShould_NotFindIt()
        {
            // Arrange
            TestingDeserializeOptions optionsA = new();
            optionsA.Telemetries.Add<Log>();
            optionsA.MakeReadOnly();

            TestingDeserializeOptions optionsB = new();
            optionsB.Telemetries.Add<Stem>();
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
            options.Telemetries.Add<Log>();
            options.MakeReadOnly();

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => options.Policies.Add<Log>(new SetMarkerPolicy()));
        }

        // T054 - quickstart 11 / US4-4 / FR-013: adding a telemetry type after first use throws
        [Fact]
        public void TelemetriesAdd_AfterTheOptionsAreLocked_ItShould_Throw()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Telemetries.Add<Log>();
            options.MakeReadOnly();

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => options.Telemetries.Add<Stem>());
        }

        // T054 - quickstart 11 / US4-4 / FR-013: adding a converter after first use throws
        [Fact]
        public void ConvertersAdd_AfterTheOptionsAreLocked_ItShould_Throw()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Telemetries.Add<Log>();
            options.MakeReadOnly();

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => options.Converters.Add(new MarkingInt32Converter()));
        }
    }
}
