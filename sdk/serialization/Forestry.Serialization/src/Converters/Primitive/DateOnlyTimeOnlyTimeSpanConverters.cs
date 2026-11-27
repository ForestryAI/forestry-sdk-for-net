using System.Globalization;
using System.Text;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Converters.Primitive
{
    /// <summary>
    /// Decodes short UTF-8 texts (dates, times, durations) into a stack buffer for the BCL's char parsers
    /// (spec 005 research R61)
    /// </summary>
    internal static class ShortText
    {
        /// <summary>
        /// Longer than any date, time or duration text; a longer text cannot be one
        /// </summary>
        public const int MaxChars = 64;

        /// <summary>
        /// Decode <paramref name="utf8"/> into <paramref name="buffer"/>, or fail as not a <paramref name="typeName"/>
        /// </summary>
        public static ReadOnlySpan<char> Decode(ReadOnlySpan<byte> utf8, Span<char> buffer, string typeName)
        {
            if (utf8.Length > MaxChars || !Encoding.UTF8.TryGetChars(utf8, buffer, out int written))
            {
                throw new FormatException(Formatting.Format(Formatting.NotParsable, typeName));
            }

            return buffer[..written];
        }
    }

    /// <summary>
    /// Built-in converter for <see cref="DateOnly"/>: reads invariant text (e.g. <c>2026-11-27</c>, as XML
    /// Schema's <c>xs:date</c>), writes through <see cref="TelemetryWriter.WriteValue(DateOnly)"/> (R61)
    /// </summary>
    internal sealed class DateOnlyConverter : Converter<DateOnly>
    {
        public override DateOnly Read(ReadOnlySpan<byte> utf8)
        {
            ReadOnlySpan<char> text = ShortText.Decode(utf8, stackalloc char[ShortText.MaxChars], nameof(DateOnly));

            if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly value))
            {
                return value;
            }

            throw new FormatException(Formatting.Format(Formatting.NotParsable, nameof(DateOnly)));
        }

        public override void Write(DateOnly value, TelemetryWriter writer) => writer.WriteValue(value);
    }

    /// <summary>
    /// Built-in converter for <see cref="TimeOnly"/>: reads invariant text with or without fractions (e.g.
    /// <c>13:20:00</c>, <c>13:20:00.5</c>, as XML Schema's <c>xs:time</c>), writes through
    /// <see cref="TelemetryWriter.WriteValue(TimeOnly)"/> (R61)
    /// </summary>
    internal sealed class TimeOnlyConverter : Converter<TimeOnly>
    {
        public override TimeOnly Read(ReadOnlySpan<byte> utf8)
        {
            ReadOnlySpan<char> text = ShortText.Decode(utf8, stackalloc char[ShortText.MaxChars], nameof(TimeOnly));

            if (TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly value))
            {
                return value;
            }

            throw new FormatException(Formatting.Format(Formatting.NotParsable, nameof(TimeOnly)));
        }

        public override void Write(TimeOnly value, TelemetryWriter writer) => writer.WriteValue(value);
    }

    /// <summary>
    /// Built-in converter for <see cref="TimeSpan"/>: reads the invariant constant and general forms (e.g.
    /// <c>-1.02:03:04.5</c>), writes through <see cref="TelemetryWriter.WriteValue(TimeSpan)"/> (R61)
    /// </summary>
    internal sealed class TimeSpanConverter : Converter<TimeSpan>
    {
        public override TimeSpan Read(ReadOnlySpan<byte> utf8)
        {
            ReadOnlySpan<char> text = ShortText.Decode(utf8, stackalloc char[ShortText.MaxChars], nameof(TimeSpan));

            if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out TimeSpan value))
            {
                return value;
            }

            throw new FormatException(Formatting.Format(Formatting.NotParsable, nameof(TimeSpan)));
        }

        public override void Write(TimeSpan value, TelemetryWriter writer) => writer.WriteValue(value);
    }
}
