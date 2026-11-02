using Forestry.Deserialize.Converters;

namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A developer converter for <see cref="bool"/>, recognisable as the effective converter over the
    /// XML default (flow 3, R14): it gives <c>false</c> where the XML default reads <c>1</c> as <c>true</c>
    /// </summary>
    public sealed class MarkingBooleanConverter : Converter<bool>
    {
        public override bool Convert(ReadOnlySpan<byte> utf8) => false;
    }
}
