using System.Globalization;
using System.Text;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A telemetry for writing tests (spec 004 tasks.md T012): every member has a built-in converter that
    /// writes, and <see cref="Name"/> can be null
    /// </summary>
    public sealed class Sample : Telemetry
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public decimal Value { get; set; }

        public Guid Key { get; set; }
    }

    /// <summary>
    /// A telemetry whose first member can be null, to show an omitted first member leaves no stray
    /// separator (T016, research R49)
    /// </summary>
    public sealed class OptionalSample : Telemetry
    {
        public string? Note { get; set; }

        public int Id { get; set; }
    }

    /// <summary>
    /// A telemetry with a member whose converter only reads (T017, FR-008)
    /// </summary>
    public sealed class TreeWithSpecies : Telemetry
    {
        public int Id { get; set; }

        public Species Species { get; set; }
    }

    /// <summary>
    /// A single-value struct (research R32) whose only converter reads: <see cref="SpeciesReadingConverter"/>
    /// </summary>
    public readonly struct Species(int code) : IUtf8SpanFormattable, ISpanParsable<Species>
    {
        public int Code { get; } = code;

        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
            Code.TryFormat(utf8Destination, out bytesWritten, format, provider ?? CultureInfo.InvariantCulture);

        public string ToString(string? format, IFormatProvider? formatProvider) => Code.ToString(format, formatProvider);

        public static Species Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => new(int.Parse(s, provider ?? CultureInfo.InvariantCulture));

        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Species result)
        {
            bool parsed = int.TryParse(s, NumberStyles.Integer, provider ?? CultureInfo.InvariantCulture, out int code);
            result = new Species(code);
            return parsed;
        }

        public static Species Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

        public static bool TryParse(string? s, IFormatProvider? provider, out Species result) => TryParse(s.AsSpan(), provider, out result);
    }

    /// <summary>
    /// Reads <see cref="Species"/> and does not override <c>Write</c> (T017)
    /// </summary>
    public sealed class SpeciesReadingConverter : Converter<Species>
    {
        public override Species Read(ReadOnlySpan<byte> utf8) => new(int.Parse(Encoding.UTF8.GetString(utf8), CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// A <see cref="string"/> converter that fails if it is ever asked to write null (T016, research R50)
    /// </summary>
    public sealed class NullRefusingStringConverter : Converter<string>
    {
        public override string Read(ReadOnlySpan<byte> utf8) => Encoding.UTF8.GetString(utf8);

        public override void Write(string value, TelemetryWriter writer)
        {
            if (value is null)
            {
                throw new InvalidOperationException("A converter is never asked to write null (R50).");
            }

            writer.WriteValue(value.AsSpan());
        }
    }
}
