using System.Buffers;

namespace Forestry.Serialization.Converters.Primitive
{
    /// <summary>
    /// Primitive converter for <see cref="bool"/>: <c>true</c> or <c>false</c>. A format may add its own
    /// default (e.g. XML Schema also accepts <c>1</c> and <c>0</c>)
    /// </summary>
    internal sealed class BooleanConverter : Converter<bool>
    {
        public override bool Read(ReadOnlySpan<byte> utf8)
        {
            if (utf8.SequenceEqual("true"u8))
            {
                return true;
            }

            if (utf8.SequenceEqual("false"u8))
            {
                return false;
            }

            throw new FormatException(Formatting.Format(Formatting.NotParsable, nameof(Boolean)));
        }

        public override void Write(bool value, IBufferWriter<byte> writer)
        {
            ReadOnlySpan<byte> text = value ? "true"u8 : "false"u8;
            text.CopyTo(writer.GetSpan(text.Length));
            writer.Advance(text.Length);
        }
    }
}
