namespace Forestry.Deserialize.Tests
{
    internal sealed class TestingDeserializerProvider : IDeserializerProvider
    {
        public Dictionary<Type, Deserializer> GetSimpleDeserializers() => new()
        {
            [typeof(string)] = new TestingValueDeserializer(typeof(string)),
            [typeof(TestingMachine)] = new TestingObjectDeserializer(typeof(TestingMachine)),
        };

        public DeserializerFactory[] GetFactoryDeserializers() => [];
    }
}
