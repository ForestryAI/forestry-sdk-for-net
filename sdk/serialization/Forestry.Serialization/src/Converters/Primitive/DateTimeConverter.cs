using System.Globalization;
using System.Text;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Converters.Primitive
{
    /// <summary>
    /// Primitive converter for <see cref="DateTime"/>, invariant culture, round-trip kind; writes the
    /// round-trip format ("O")
    /// </summary>
    /// <remarks>Decodes to a string first; allocation-free parsing is performance work (C6).</remarks>
    internal sealed class DateTimeConverter : Converter<DateTime>
    {
        public override DateTime Read(ReadOnlySpan<byte> utf8)
        {
            if (DateTime.TryParse(Encoding.UTF8.GetString(utf8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime value))
            {
                return value;
            }

            throw new FormatException(Formatting.Format(Formatting.NotParsable, nameof(DateTime)));
        }

        public override void Write(DateTime value, TelemetryWriter writer) => writer.WriteFormattable(value, "O");
    }

    /// <summary>
    /// Primitive converter for <see cref="DateTimeOffset"/>, invariant culture, round-trip kind; writes
    /// the round-trip format ("O")
    /// </summary>
    internal sealed class DateTimeOffsetConverter : Converter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ReadOnlySpan<byte> utf8)
        {
            if (DateTimeOffset.TryParse(Encoding.UTF8.GetString(utf8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset value))
            {
                return value;
            }

            throw new FormatException(Formatting.Format(Formatting.NotParsable, nameof(DateTimeOffset)));
        }

        public override void Write(DateTimeOffset value, TelemetryWriter writer) => writer.WriteFormattable(value, "O");
    }
}
