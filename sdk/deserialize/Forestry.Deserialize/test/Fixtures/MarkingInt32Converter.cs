using Forestry.Deserialize.Converters;

namespace Forestry.Deserialize.Tests.Fixtures
{
    /// <summary>
    /// A developer converter for <see cref="int"/>, recognisable as the effective converter (flow 3, R14)
    /// </summary>
    public sealed class MarkingInt32Converter : Converter<int>
    {
        public override int Convert(ReadOnlySpan<byte> utf8) => -1;
    }
}
