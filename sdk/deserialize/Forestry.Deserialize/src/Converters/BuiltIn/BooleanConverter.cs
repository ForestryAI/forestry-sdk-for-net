namespace Forestry.Deserialize.Converters.BuiltIn
{
    /// <summary>
    /// Built-in converter for <see cref="bool"/>: <c>true</c> or <c>false</c>. A media may add its own
    /// default (e.g. XML Schema also accepts <c>1</c> and <c>0</c>)
    /// </summary>
    internal sealed class BooleanConverter : Converter<bool>
    {
        public override bool Convert(ReadOnlySpan<byte> utf8)
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
    }
}
