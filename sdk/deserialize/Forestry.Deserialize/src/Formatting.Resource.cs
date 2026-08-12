namespace Forestry.Deserialize.Immutable
{
    internal static class Formatting {}
}

namespace Forestry.Deserialize
{
    internal static partial class Formatting
    {
        internal static System.Resources.ResourceManager ResourceManager => s_resourceManager ??= new System.Resources.ResourceManager(typeof(Immutable.Formatting));

        private static System.Resources.ResourceManager s_resourceManager = null!;

        internal static string TypeDefinitionIsReadOnly => GetResourceString(nameof(TypeDefinitionIsReadOnly), @"Type definition is readonly (immutable) i.e. type has been deserialized at least once.");

        internal static string DeserializeOptionsIsReadOnly => GetResourceString(nameof(DeserializeOptionsIsReadOnly), @"Deserialize options instance is read-only i.e. locked for changes.");

        internal static string WrongDeclaringTypeDefintion => GetResourceString(nameof(WrongDeclaringTypeDefintion),  @"Wrong declaring type definition [declaring type: {0}] for property definition [name: {1}]");

        internal static string ConfigurePropertiesWrongDeclaringTypeDefintion => GetResourceString(nameof(ConfigurePropertiesWrongDeclaringTypeDefintion), "Type definition kind '{0}' not object");

        internal static string WhenNotSingularAttribute => GetResourceString(nameof(WhenNotSingularAttribute), @"Attribute [name: '{0}',target: '{1}'] is not singular");

        internal static string PipeReaderCanceled => GetResourceString(nameof(PipeReaderCanceled), @"PipeReader was canceled");

        internal static string DuplicatePropertyName => GetResourceString(nameof(DuplicatePropertyName), @"Duplicate property name '{1}' on type '{0}'");
    }
}