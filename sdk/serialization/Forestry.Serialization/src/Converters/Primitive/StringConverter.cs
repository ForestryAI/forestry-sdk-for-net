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

        public override void Write(string value, IBufferWriter<byte> utf8)
        {
            int written = Encoding.UTF8.GetBytes(value, utf8.GetSpan(Encoding.UTF8.GetMaxByteCount(value.Length)));
            utf8.Advance(written);
        }
    }
}
