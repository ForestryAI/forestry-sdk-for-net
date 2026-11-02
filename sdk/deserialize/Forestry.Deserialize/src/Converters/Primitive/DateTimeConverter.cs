using System.Globalization;
using System.Text;

namespace Forestry.Deserialize.Converters.Primitive
{
    /// <summary>
    /// Primitive converter for <see cref="DateTime"/>, invariant culture, round-trip kind
    /// </summary>
    /// <remarks>Decodes to a string first; allocation-free parsing is performance work (C6).</remarks>
    internal sealed class DateTimeConverter : Converter<DateTime>
    {
        public override DateTime Convert(ReadOnlySpan<byte> utf8)
        {
            if (DateTime.TryParse(Encoding.UTF8.GetString(utf8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime value))
            {
                return value;
            }

            throw new FormatException(Formatting.Format(Formatting.NotParsable, nameof(DateTime)));
        }
    }

    /// <summary>
    /// Primitive converter for <see cref="DateTimeOffset"/>, invariant culture, round-trip kind
    /// </summary>
    internal sealed class DateTimeOffsetConverter : Converter<DateTimeOffset>
    {
        public override DateTimeOffset Convert(ReadOnlySpan<byte> utf8)
        {
            if (DateTimeOffset.TryParse(Encoding.UTF8.GetString(utf8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset value))
            {
                return value;
            }

            throw new FormatException(Formatting.Format(Formatting.NotParsable, nameof(DateTimeOffset)));
        }
    }
}
