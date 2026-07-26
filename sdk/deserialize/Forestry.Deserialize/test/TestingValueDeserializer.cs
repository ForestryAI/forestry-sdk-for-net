namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Deserializer for a leaf/scalar type (e.g. string) - a property whose value is read
    /// directly, with no further nested properties.
    /// </summary>
    internal sealed class TestingValueDeserializer(Type type) : Deserializer
    {
        public override Type? Type { get; } = type;

        public override bool CanReadValues => true;

        public override bool CanDeserialize(Type type) => type == Type;

        protected internal override DeserializerKind GetDeserializerKind() => DeserializerKind.Value;

        public override TypeDefinition InitializeTypeDefinition(DeserializeOptions options) =>
            new TestingTypeDefinition(Type!, this, options);
    }
}
