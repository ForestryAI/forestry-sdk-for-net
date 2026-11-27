using System.Text;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.JsonLines.Tests.Fixtures
{
    /// <summary>
    /// Serializing telemetries as JSON Lines into memory, and reading the lines back (spec 005 tasks.md T036)
    /// </summary>
    internal static class JsonLinesOutput
    {
        /// <summary>
        /// JSON Lines options with <typeparamref name="TTelemetry"/> added
        /// </summary>
        public static SerializeJsonLinesOptions OptionsWith<TTelemetry>(params Converter[] converters) where TTelemetry : Telemetry
        {
            SerializeJsonLinesOptions options = new();
            options.Telemetries.Add<TTelemetry>();

            foreach (Converter converter in converters)
            {
                options.Converters.Add(converter);
            }

            return options;
        }

        /// <summary>
        /// The bytes of <paramref name="telemetries"/> serialized one by one into a <see cref="MemoryStream"/>
        /// </summary>
        public static byte[] Bytes(SerializeJsonLinesOptions options, params Telemetry[] telemetries)
        {
            using MemoryStream memory = new();

            foreach (Telemetry telemetry in telemetries)
            {
                TelemetrySerialization.Serialize(telemetry, memory, options);
            }

            return memory.ToArray();
        }

        /// <summary>
        /// The lines of <paramref name="bytes"/>: each ends with <c>\n</c>, which is not part of the line
        /// </summary>
        public static string[] Lines(byte[] bytes)
        {
            string text = Encoding.UTF8.GetString(bytes);
            Xunit.Assert.EndsWith("\n", text, StringComparison.Ordinal);
            return text[..^1].Split('\n');
        }

        /// <summary>
        /// The lines of <paramref name="telemetries"/> serialized one by one
        /// </summary>
        public static string[] Lines(SerializeJsonLinesOptions options, params Telemetry[] telemetries) => Lines(Bytes(options, telemetries));
    }

    /// <summary>
    /// A converter writing through the writer's <see cref="System.Buffers.IBufferWriter{T}"/> members instead of
    /// its value methods (T044): JSON Lines refuses it
    /// </summary>
    public sealed class DirectBufferInt32Converter : Converter<int>
    {
        public override int Read(ReadOnlySpan<byte> utf8) => throw new NotSupportedException();

        public override void Write(int value, TelemetryWriter writer)
        {
            Span<byte> span = writer.GetSpan(1);
            span[0] = (byte)'1';
            writer.Advance(1);
        }
    }

    /// <summary>
    /// A converter writing a raw JSON value through <see cref="TelemetryWriter.WriteRaw"/> (T044): a complete
    /// value is written, an incomplete one refused
    /// </summary>
    public sealed class RawInt32Converter(string raw) : Converter<int>
    {
        public override int Read(ReadOnlySpan<byte> utf8) => throw new NotSupportedException();

        public override void Write(int value, TelemetryWriter writer) => writer.WriteRaw(Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>
    /// A telemetry with one <see cref="int"/>, for the converters above
    /// </summary>
    public sealed class CountTelemetry : Telemetry
    {
        public int Count { get; set; }
    }
}
