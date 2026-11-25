using System.Globalization;
using Forestry.Serialization.Converters;

namespace Forestry.Serialization.Tests.Fixtures
{
    // Row types with members that are not single values (spec 003 US1-2 to US1-5, SC-001, research
    // R32, R33). LogWithNestedProperty (a class member) is in its own file from spec 001.

    /// <summary>
    /// A member of another row type (US1-2)
    /// </summary>
    public sealed class LogWithRowMember : Row
    {
        public int LogKey { get; set; }

        public Stem? Stem { get; set; }
    }

    /// <summary>
    /// An array member (US1-3), rejected as a collection even though a byte array could be one binary
    /// column (spec edge cases)
    /// </summary>
    public sealed class LogWithArray : Row
    {
        public int LogKey { get; set; }

        public int[]? Diameters { get; set; }
    }

    /// <summary>
    /// A list member (US1-3)
    /// </summary>
    public sealed class LogWithList : Row
    {
        public int LogKey { get; set; }

        public List<int>? Diameters { get; set; }
    }

    /// <summary>
    /// A dictionary member (US1-3)
    /// </summary>
    public sealed class LogWithDictionary : Row
    {
        public int LogKey { get; set; }

        public Dictionary<string, int>? Diameters { get; set; }
    }

    /// <summary>
    /// A struct with two values and no text form (US1-4)
    /// </summary>
    public sealed class LogWithCompositeStruct : Row
    {
        public int LogKey { get; set; }

        public Coordinate Position { get; set; }
    }

    /// <summary>
    /// Two values in one struct, with neither a text form nor UTF-8 formatting
    /// </summary>
    public struct Coordinate
    {
        public int Latitude;

        public int Longitude;
    }

    /// <summary>
    /// A composite member with a converter the test adds (US1-5, FR-003): the converter does not make
    /// it flat
    /// </summary>
    public sealed class LogWithConvertedComposite : Row
    {
        public int LogKey { get; set; }

        public Measurement Measurement { get; set; } = new();
    }

    /// <summary>
    /// A converter turning text into a composite <see cref="Measurement"/> (FR-003)
    /// </summary>
    public sealed class MeasurementConverter : Converter<Measurement>
    {
        public override Measurement Convert(ReadOnlySpan<byte> utf8) =>
            new() { Diameter = decimal.Parse(System.Text.Encoding.UTF8.GetString(utf8), CultureInfo.InvariantCulture) };
    }

    /// <summary>
    /// A struct member that formats only to chars (research R32, R33): one value, but no UTF-8 formatting
    /// </summary>
    public sealed class LogWithCharsOnlyStruct : Row
    {
        public int LogKey { get; set; }

        public CharsOnlyMeasure Diameter { get; set; }
    }

    /// <summary>
    /// Implements <see cref="ISpanFormattable"/> and <see cref="ISpanParsable{TSelf}"/> but not
    /// <see cref="IUtf8SpanFormattable"/>
    /// </summary>
    public readonly struct CharsOnlyMeasure(decimal value) : ISpanFormattable, ISpanParsable<CharsOnlyMeasure>
    {
        public decimal Value { get; } = value;

        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
            Value.TryFormat(destination, out charsWritten, format, provider);

        public string ToString(string? format, IFormatProvider? formatProvider) => Value.ToString(format, formatProvider);

        public static CharsOnlyMeasure Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => new(decimal.Parse(s, provider));

        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out CharsOnlyMeasure result)
        {
            bool parsed = decimal.TryParse(s, NumberStyles.Number, provider, out decimal value);
            result = new CharsOnlyMeasure(value);
            return parsed;
        }

        public static CharsOnlyMeasure Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

        public static bool TryParse([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? s, IFormatProvider? provider, out CharsOnlyMeasure result) =>
            TryParse(s.AsSpan(), provider, out result);
    }

    /// <summary>
    /// Two failing members in one row type (research R33, one message per type): a nested structure
    /// and a single value without a converter
    /// </summary>
    public sealed class LogWithTwoFailingMembers : Row
    {
        public int LogKey { get; set; }

        public List<int>? Diameters { get; set; }

        public UnconvertedMeasure Length { get; set; }
    }

    /// <summary>
    /// A single value (UTF-8 formatting, char parsing) that no converter handles
    /// </summary>
    public readonly struct UnconvertedMeasure(decimal value) : IUtf8SpanFormattable, ISpanParsable<UnconvertedMeasure>
    {
        public decimal Value { get; } = value;

        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
            Value.TryFormat(utf8Destination, out bytesWritten, format, provider);

        public static UnconvertedMeasure Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => new(decimal.Parse(s, provider));

        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out UnconvertedMeasure result)
        {
            bool parsed = decimal.TryParse(s, NumberStyles.Number, provider, out decimal value);
            result = new UnconvertedMeasure(value);
            return parsed;
        }

        public static UnconvertedMeasure Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

        public static bool TryParse([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? s, IFormatProvider? provider, out UnconvertedMeasure result) =>
            TryParse(s.AsSpan(), provider, out result);
    }

    /// <summary>
    /// A list member that <see cref="ExcludingPolicy"/> leaves out (research R33, ignored members)
    /// </summary>
    public sealed class LogWithExcludedList : Row
    {
        public int LogKey { get; set; }

        public List<int>? ExcludedDiameters { get; set; }
    }
}
