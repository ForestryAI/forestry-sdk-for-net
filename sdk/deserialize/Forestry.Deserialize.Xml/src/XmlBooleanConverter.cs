using Forestry.Deserialize.Converters;

namespace Forestry.Deserialize.Xml
{
    /// <summary>
    /// XML default converter for <see cref="bool"/>: XML Schema accepts <c>true</c>, <c>false</c>,
    /// <c>1</c> and <c>0</c> (research R14)
    /// </summary>
    internal sealed class XmlBooleanConverter : Converter<bool>
    {
        public override bool Convert(ReadOnlySpan<byte> utf8)
        {
            if (utf8.SequenceEqual("true"u8) || utf8.SequenceEqual("1"u8))
            {
                return true;
            }

            if (utf8.SequenceEqual("false"u8) || utf8.SequenceEqual("0"u8))
            {
                return false;
            }

            throw new FormatException("The value is not an XML Schema boolean (true, false, 1 or 0).");
        }
    }
}
