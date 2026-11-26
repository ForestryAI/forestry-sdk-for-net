using Forestry.Serialization.Converters;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A developer converter for <see cref="int"/> that writes <c>x</c> whatever the value, recognisable
    /// as the converter used for writing (spec 004 US1-3, FR-004)
    /// </summary>
    public sealed class WritingMarkerInt32Converter : Converter<int>
    {
        public override int Read(ReadOnlySpan<byte> utf8) => -1;

        public override void Write(int value, TelemetryWriter writer) => writer.WriteRaw("x"u8);
    }
}
