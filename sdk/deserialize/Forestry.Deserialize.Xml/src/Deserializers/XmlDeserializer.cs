using Forestry.Deserialize.Deserializers;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Xml.Deserializers
{
    internal class XmlDeserializer<T>: Deserializer<T> where T : notnull
    {
        public override ReadingStatus TryReadNullableValue<TBuffering, TStream>(
            ref TBuffering buffering, 
            scoped ref ReaderPath readerPath, 
            Type type, 
            out Value<T> value, 
            DeserializeOptions options
        )
        {
            // TODO: Create Reader from buffer

            try
            {
            } catch (Exception e)
            {
                switch (e)
                {
                    case InvalidOperationException:
                        break;
                }

                throw;
            }

            value = default!;

            // TODO: Reader get next property

            return ReadingStatus.Partial;
        }

    }
}