namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Deserializer for a schema class (e.g. <see cref="TestingMachine"/>) - has properties,
    /// reflected via <see cref="TypeDefinitionProvider"/>.
    /// </summary>
    internal sealed class TestingObjectDeserializer(Type type) : Deserializer
    {
        public override Type? Type { get; } = type;

        public override bool CanDeserialize(Type type) => type == Type;

        protected internal override DeserializerKind GetDeserializerKind() => DeserializerKind.Object;

        public override TypeDefinition InitializeTypeDefinition(DeserializeOptions options) =>
            new TestingTypeDefinition(Type!, this, options);
    }
}
