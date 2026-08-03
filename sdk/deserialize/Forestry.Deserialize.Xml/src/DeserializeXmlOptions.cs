using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Deserializers;

namespace Forestry.Deserialize.Xml
{
    /// <summary>
    /// Deserialize XML options
    /// </summary>
    public sealed class DeserializeXmlOptions: DeserializeOptions
    {
        /// <summary>
        /// Default options instance
        /// </summary>
        public static readonly DeserializeXmlOptions Default = new DeserializeXmlOptions();

        internal override Func<Type, Deserializer, DeserializeOptions, TypeDefinition> TypeDefinitionReflectiveInstantiator => throw new NotImplementedException();

        internal override Func<Type, TypeDefinition, DeserializeOptions, PropertyDefinition> PropertyDefinitionReflectiveInstantiator => throw new NotImplementedException();

        internal override IDeserializerProvider DeserializerProvider => throw new NotImplementedException();

        public override TState CreateReaderState<TState>(ReadOnlySpan<byte> buffer)
        {
            throw new NotImplementedException();
        }
    }
}