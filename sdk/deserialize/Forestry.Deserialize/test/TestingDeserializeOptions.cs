using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Deserializers;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Minimal DeserializeOptions for testing schema reflection, and now the pipe-driven
    /// walk, over a fake, in-memory media - no real reader involved.
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

        // Not exercised by these tests yet - no concrete reader is ever constructed from state,
        // since TestingValueDeserializer<T> reads straight off the buffer's raw bytes.
        public override TState CreateReaderState<TState>(ReadOnlySpan<byte> buffer) =>
            throw new NotSupportedException($"{nameof(TestingDeserializeOptions)} does not construct a concrete reader from state.");
    }
}
