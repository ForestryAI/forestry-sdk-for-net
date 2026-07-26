using Xunit;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Shows how a StanForD-shaped schema class (here <see cref="TestingMachine"/>, a stand-in
    /// until a real Forestry.StanForD project exists) is reflected into a <see cref="TypeDefinition"/>:
    /// [Collection] names the type when it appears as a list item, and [Element] names each
    /// property - independent of any particular media format.
    /// </summary>
    public class TypeDefinitionReflectionTests
    {
        [Fact]
        public void GetTypeDefinition_ForASchemaClass_ItShould_ReflectItsShape()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act
            TypeDefinition typeDefinition = options.GetTypeDefinition(typeof(TestingMachine));

            // Assert
            Assert.Equal(TypeDefinitionKind.Object, typeDefinition.Kind);
            Assert.Equal("Machine", typeDefinition.ElementCollection);
        }

        [Fact]
        public void GetTypeDefinition_ForASchemaClass_ItShould_NamePropertiesFromElementAttributes()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act
            TypeDefinition typeDefinition = options.GetTypeDefinition(typeof(TestingMachine));

            // Assert: names come from [Element("...")], not the raw C# member names
            string[] names = [.. typeDefinition.Properties.Select(property => property.Name).OrderBy(name => name)];
            Assert.Equal(["MachineId", "MachineType"], names);

            Assert.All(typeDefinition.Properties, property => Assert.Equal(typeof(string), property.Type));
        }

        [Fact]
        public void GetTypeDefinition_CalledTwice_ItShould_ReuseTheSameTypeDefinition()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act
            TypeDefinition first = options.GetTypeDefinition(typeof(TestingMachine));
            TypeDefinition second = options.GetTypeDefinition(typeof(TestingMachine));

            // Assert: reflection only walks a type once per DeserializeOptions
            Assert.Same(first, second);
        }

        [Fact(Skip =
            "Element/attribute position within the document isn't modeled yet - ElementAttribute " +
            "only carries a name, and PropertyDefinition doesn't track document order. Needed " +
            "before a reader can validate 'is this the Nth expected element here' while streaming.")]
        public void GetTypeDefinition_ItShould_ExposeElementPositionWithinTheDocument()
        {
        }
    }
}
