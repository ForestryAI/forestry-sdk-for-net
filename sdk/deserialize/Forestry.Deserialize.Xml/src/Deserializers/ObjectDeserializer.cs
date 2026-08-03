using Forestry.Deserialize.Deserializers;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Xml.Deserializers
{
    internal class ObjectDeserializer<T>: Deserializer<T> where T : notnull
    {
        public override ReadingStatus TryReadNullableValue<TBuffering, TStream>(ref TBuffering buffering, scoped ref ReaderPath readerPath, Type type, out Value<T> value, DeserializeOptions options)
        {
            value = default!;

            // TODO: Reader get next property

            return ReadingStatus.Partial;
        }

        protected internal sealed override DeserializerKind GetDeserializerKind() => DeserializerKind.Object;
    }
}