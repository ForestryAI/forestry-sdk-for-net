using Forestry.Deserialize.Definitions;

namespace Forestry.Deserialize.Tests
{
    internal sealed class TestingPropertyDefinition(
        Type type,
        Type declaringType,
        TypeDefinition? declaringTypeDefinition,
        DeserializeOptions options
    ) : PropertyDefinition(type, declaringType, declaringTypeDefinition, options)
    {
    }
}
