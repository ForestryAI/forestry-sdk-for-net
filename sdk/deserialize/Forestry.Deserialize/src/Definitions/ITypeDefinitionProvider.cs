namespace Forestry.Deserialize.Definitions
{
    public interface ITypeDefinitionProvider
    {
        TypeDefinition? GetTypeDefinition(Type type, DeserializeOptions options);
    }
}