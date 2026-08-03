using System.IO.Pipelines;
using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize
{
    public static partial class Deserialization
    {
        public static IAsyncEnumerator<Value> DeserializeAsync<T>(PipeReader stream, DeserializeOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);

            TypeDefinition<T> typeDefinition = GetTypeDefinition<T>(options);
            return new ValueAsyncEnumerable<T, PipeReaderBuffering, PipeReader>(typeDefinition, new PipeReaderBuffering(stream), stream).GetAsyncEnumerator(cancellationToken);
        }
    }
}