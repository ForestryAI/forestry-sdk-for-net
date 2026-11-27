using Forestry.Serialization.Converters;

namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A developer converter for <see cref="int"/>, recognizable as the effective converter (flow 3, R14)
    /// </summary>
    public sealed class MarkingInt32Converter : Converter<int>
    {
        public override int Read(ReadOnlySpan<byte> utf8) => -1;
    }
}
