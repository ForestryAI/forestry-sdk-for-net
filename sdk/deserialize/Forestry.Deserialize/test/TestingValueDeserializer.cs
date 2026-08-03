using System.Buffers;
using System.Text;
using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Deserializers;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Deserializer for a leaf/scalar type (e.g. string) - reads directly from whatever's
    /// buffered, with no further nested properties. Test-only simplification: tracks "already
    /// produced the one value" on the instance itself, not via ReaderPath/ReaderPosition, so a
    /// fresh instance is needed per enumeration (fine here - TestingDeserializerProvider builds
    /// a fresh one every time).
    /// </summary>
    internal sealed class TestingValueDeserializer<T>(Func<byte[], T> parse) : Deserializer<T>
    {
        private bool _hasRead;

        public override bool CanReadValues => true;

        public override bool CanDeserialize(Type type) => type == Type;

        protected internal override DeserializerKind GetDeserializerKind() => DeserializerKind.Value;

        public override TypeDefinition InitializeTypeDefinition(DeserializeOptions options) =>
            new TypeDefinition<T>(this, options);

        public override ReadingStatus TryReadNullableValue<TBuffering, TStream>(
            ref TBuffering buffering,
            scoped ref ReaderPath readerPath,
            Type type,
            out Value<T> value,
            DeserializeOptions options
        )
        {
            if (_hasRead)
            {
                value = null!;
                return ReadingStatus.NoValue;
            }

            if (buffering.Bytes.IsEmpty)
            {
                value = null!;
                return ReadingStatus.Partial;
            }

            byte[] raw = buffering.Bytes.ToArray();
            T deserialized = parse(raw);

            value = new DeserializedValue<T>(new TestingValue(type.Name, Encoding.UTF8.GetString(raw)), deserialized);
            buffering.Advance(raw.Length);
            _hasRead = true;

            return ReadingStatus.Value;
        }
    }
}
