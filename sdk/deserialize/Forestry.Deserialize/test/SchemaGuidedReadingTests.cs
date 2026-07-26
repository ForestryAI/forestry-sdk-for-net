using Xunit;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Where the two halves are headed: a schema class (<see cref="TestingMachine"/>) reflected
    /// into a <see cref="TypeDefinition"/> describes what a reader should see; a fake, in-memory
    /// <see cref="IReader"/> stands in for a real media reader so the two can be cross-checked
    /// without XML in the picture yet. There's no concrete Deserializer.Deserialize step wiring
    /// them together automatically - this shows the data needed to build one.
    /// </summary>
    public class SchemaGuidedReadingTests
    {
        [Fact]
        public async Task ReaderOutput_WhenItMatchesTheSchema_ItShould_NameOnlyExpectedProperties()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            TypeDefinition machine = options.GetTypeDefinition(typeof(TestingMachine));
            HashSet<string> expectedNames = [.. machine.Properties.Select(property => property.Name)];

            TestingReader reader = new(
                () => new TestingValue("MachineId", "8027"),
                () => new TestingValue("MachineType", "Harvester"));

            // Act
            List<Value> values = new();
            await foreach (Value value in reader.AsAsyncEnumerable())
            {
                values.Add(value);
            }

            // Assert
            Assert.Equal(2, values.Count);
            Assert.All(values, value => Assert.Contains(value.Name!, expectedNames));
        }

        [Fact]
        public async Task ReaderOutput_WhenItHasAnUnexpectedElement_ItShould_BeDetectableAgainstTheSchema()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            TypeDefinition machine = options.GetTypeDefinition(typeof(TestingMachine));
            HashSet<string> expectedNames = [.. machine.Properties.Select(property => property.Name)];

            TestingReader reader = new(
                () => new TestingValue("MachineId", "8027"),
                () => new TestingValue("EngineHours", "1234")); // not part of TestingMachine's schema

            // Act
            List<Value> values = new();
            await foreach (Value value in reader.AsAsyncEnumerable())
            {
                values.Add(value);
            }

            // Assert: the mismatch is visible today by comparing names; nothing yet plugs this
            // into ReadErrorHandling automatically while streaming (see TypeDefinitionReflectionTests'
            // pending position test - order/position is the other half needed for that).
            Value unexpected = Assert.Single(values, value => !expectedNames.Contains(value.Name!));
            Assert.Equal("EngineHours", unexpected.Name);
        }
    }
}
