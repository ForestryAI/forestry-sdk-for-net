using System.Buffers;
using System.Text;

namespace Forestry.Serialization.Converters.Primitive
{
    /// <summary>
    /// Primitive converter for text
    /// </summary>
    internal sealed class StringConverter : Converter<string>
    {
        public override string Read(ReadOnlySpan<byte> utf8) => Encoding.UTF8.GetString(utf8);

        public override void Write(string value, IBufferWriter<byte> writer)
        {
            int written = Encoding.UTF8.GetBytes(value, writer.GetSpan(Encoding.UTF8.GetMaxByteCount(value.Length)));
            writer.Advance(written);
        }
    }
}
