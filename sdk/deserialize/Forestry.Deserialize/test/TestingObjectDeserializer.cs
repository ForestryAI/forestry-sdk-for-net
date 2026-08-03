using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Deserializers;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Deserializer for a schema class (e.g. <see cref="TestingMachine"/>) - has properties,
    /// reflected via <see cref="TypeDefinitionProvider"/>. Only used for schema-reflection tests
    /// (<see cref="TypeDefinitionReflectionTests"/>) today - not driven through the pipe-based
    /// walk yet, so TryReadValue is an unused stub.
    /// </summary>
    internal sealed class TestingObjectDeserializer(Type type) : Deserializer
    {
        public override Type? Type { get; } = type;

        public override bool CanDeserialize(Type type) => type == Type;

        protected internal override DeserializerKind GetDeserializerKind() => DeserializerKind.Object;

        public override TypeDefinition InitializeTypeDefinition(DeserializeOptions options) =>
            new TestingTypeDefinition(Type!, this, options);

        internal override ReadingStatus TryReadValue<TBuffering, TStream>(ref TBuffering buffering, scoped ref ReaderPath readerPath, out Value? value, DeserializeOptions options, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException($"{nameof(TestingObjectDeserializer)} is only used for schema reflection today, not the pipe-based walk.");
    }
}
