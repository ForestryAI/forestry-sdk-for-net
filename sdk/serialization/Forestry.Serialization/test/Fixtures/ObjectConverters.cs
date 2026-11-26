using System.Globalization;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// An object converter a developer registers for <see cref="Sample"/>, recognisable by what it writes:
    /// <c>sample:{Id}</c> and a new line, in place of the default walk (spec 004 tasks.md T052, research R49)
    /// </summary>
    public sealed class MarkingSampleConverter : ObjectConverter<Sample>
    {
        public override void Write(Sample value, TelemetryWriter writer)
        {
            writer.WriteRaw("sample:"u8);
            writer.WriteValue(value.Id.ToString(CultureInfo.InvariantCulture).AsSpan());
            writer.WriteRaw("\n"u8);
        }
    }

    /// <summary>
    /// A converter for the telemetry type <see cref="Sample"/> that is not an object converter: refused when
    /// the options lock (T055)
    /// </summary>
    public sealed class PlainSampleConverter : Converter<Sample>
    {
        public override Sample Read(ReadOnlySpan<byte> utf8) => new();

        public override void Write(Sample value, TelemetryWriter writer) => writer.WriteRaw("plain\n"u8);
    }
}
