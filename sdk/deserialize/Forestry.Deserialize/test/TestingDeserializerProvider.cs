using System.Text;
using Forestry.Deserialize.Deserializers;

namespace Forestry.Deserialize.Tests
{
    internal sealed class TestingDeserializerProvider : IDeserializerProvider
    {
        public Dictionary<Type, Deserializer> GetSimpleDeserializers() => new()
        {
            [typeof(string)] = new TestingValueDeserializer<string>(Encoding.UTF8.GetString),
            [typeof(TestingMachine)] = new TestingObjectDeserializer(typeof(TestingMachine)),
        };

        public DeserializerFactory[] GetFactoryDeserializers() => [];
    }
}
