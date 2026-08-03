using Forestry.Deserialize.Deserializers;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Xml.Deserializers
{
    internal class BooleanDeserializer : Deserializer<bool>
    {
        // TODO: Base Value Deserializer to handle reader path, 

        public override ReadingStatus TryReadNullableValue<TBuffering, TStream>(ref TBuffering buffering, scoped ref ReaderPath readerPath, Type type, out Value<bool> value, DeserializeOptions options)
        {
            throw new NotImplementedException();
        }
    }
}