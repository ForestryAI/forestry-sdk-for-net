namespace Forestry.Deserialize.Deserializers
{
    public interface IDeserializerProvider {
        Dictionary<Type, Deserializer> GetSimpleDeserializers();

        DeserializerFactory[] GetFactoryDeserializers();
    }
}