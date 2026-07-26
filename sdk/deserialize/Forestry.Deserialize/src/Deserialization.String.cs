namespace Forestry.Deserialize
{
    public static partial class Deserialization
    {
        public static IEnumerable<Value> Deserialize<T>(string media, DeserializeOptions options)
        {
             ArgumentNullException.ThrowIfNull(media);

             TypeDefinition typeDefinition = GetTypeDefinition(typeof(T), options);

             // TODO: no IReader exists yet for raw string media (see IReader-based enumeration
             // via ValueAsyncEnumerable for the working, reader-driven path).
             throw new NotImplementedException("String-media reading is not wired up yet.");
        }
    }
}