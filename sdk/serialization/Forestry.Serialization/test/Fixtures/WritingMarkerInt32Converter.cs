using System.Buffers;
using Forestry.Serialization.Converters;

namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A developer converter for <see cref="int"/> that writes <c>x</c> whatever the value, recognisable
    /// as the converter used for writing (spec 004 US1-3, FR-004)
    /// </summary>
    public sealed class WritingMarkerInt32Converter : Converter<int>
    {
        public override int Read(ReadOnlySpan<byte> utf8) => -1;

        public override void Write(int value, IBufferWriter<byte> utf8)
        {
            Span<byte> span = utf8.GetSpan(1);
            span[0] = (byte)'x';
            utf8.Advance(1);
        }
    }
}
