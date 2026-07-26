namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Minimal DeserializeOptions for testing schema reflection over a fake, in-memory
    /// media - no real reader involved.
    /// </summary>
    internal sealed class TestingDeserializeOptions : DeserializeOptions
    {
        public TestingDeserializeOptions()
        {
            // Fixed, ready-to-use fixture - no further configuration happens after construction.
            SetReadOnly();
        }

        internal override Func<Type, Deserializer, DeserializeOptions, TypeDefinition> TypeDefinitionReflectiveInstantiator =>
            (type, deserializer, options) => new TestingTypeDefinition(type, deserializer, options);

        internal override Func<Type, TypeDefinition, DeserializeOptions, PropertyDefinition> PropertyDefinitionReflectiveInstantiator =>
            (memberType, declaringTypeDefinition, options) =>
                new TestingPropertyDefinition(memberType, declaringTypeDefinition.Type, declaringTypeDefinition, options);

        internal override IDeserializerProvider DeserializerProvider { get; } = new TestingDeserializerProvider();
    }
}
