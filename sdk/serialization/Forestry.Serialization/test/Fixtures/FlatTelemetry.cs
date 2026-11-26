using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Forestry.Serialization.Converters;

namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A telemetry type with one member of each single-value kind that has a converter (spec 003 US1-1,
    /// research R32): primitives, <see cref="string"/>, a text-form struct, nullables, and
    /// <see cref="Measure"/>, a developer's struct whose converter the test adds
    /// </summary>
    public sealed class FlatTelemetry : Telemetry
    {
        public bool Flag { get; set; }

        public char Code { get; set; }

        public int Key { get; set; }

        public long Count { get; set; }

        public double Ratio { get; set; }

        public string? Name { get; set; }

        public decimal Volume { get; set; }

        public Guid Id { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        public int? OptionalKey { get; set; }

        public Guid? OptionalId { get; set; }

        public Measure Diameter { get; set; }
    }

    /// <summary>
    /// Single values with no built-in converter today (research R32, R33): an enum, <see cref="DateOnly"/>
    /// and <see cref="TimeSpan"/>. They pass the single-value check and fail only for lack of a converter
    /// </summary>
    public sealed class SingleValuesWithoutConverters : Telemetry
    {
        public Grade Grade { get; set; }

        public DateOnly Felled { get; set; }

        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// An enum member (research R32: enums are single values)
    /// </summary>
    public enum Grade
    {
        First,
        Second
    }

    /// <summary>
    /// A developer's single-value struct (research R32): formats to UTF-8 (<see cref="IUtf8SpanFormattable"/>)
    /// and parses from chars (<see cref="ISpanParsable{TSelf}"/>), one <see cref="decimal"/> inside
    /// </summary>
    public readonly struct Measure(decimal value) : IUtf8SpanFormattable, ISpanParsable<Measure>
    {
        public decimal Value { get; } = value;

        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
            Value.TryFormat(utf8Destination, out bytesWritten, format, provider ?? CultureInfo.InvariantCulture);

        public static Measure Parse(ReadOnlySpan<char> s, IFormatProvider? provider) =>
            new(decimal.Parse(s, provider ?? CultureInfo.InvariantCulture));

        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Measure result)
        {
            bool parsed = decimal.TryParse(s, NumberStyles.Number, provider ?? CultureInfo.InvariantCulture, out decimal value);
            result = new Measure(value);
            return parsed;
        }

        public static Measure Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Measure result) =>
            TryParse(s.AsSpan(), provider, out result);
    }

    /// <summary>
    /// The converter the test adds for <see cref="Measure"/>
    /// </summary>
    public sealed class MeasureConverter : Converter<Measure>
    {
        public override Measure Read(ReadOnlySpan<byte> utf8) =>
            new(decimal.Parse(System.Text.Encoding.UTF8.GetString(utf8), CultureInfo.InvariantCulture));

        public override void Write(Measure value, Writing.TelemetryWriter writer) => writer.WriteValue(value);
    }
}
