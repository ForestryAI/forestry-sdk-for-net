using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.JsonLines
{
    /// <summary>
    /// Writes each telemetry as one JSON object and a new line, over a <see cref="Utf8JsonWriter"/> that owns
    /// commas, escaping and validation (spec 005 research R65, R66)
    /// </summary>
    /// <remarks>
    /// One per serializer call. The JSON writer is created on the first reset and reset to the call's output
    /// on every later one, and after each line, so every line is a top-level value of its own. It is flushed
    /// after each telemetry, so the new line follows the object's bytes in order.
    /// </remarks>
    /// <param name="options"></param>
    internal sealed class JsonLinesTelemetryWriter(SerializeJsonLinesOptions options) : TelemetryWriter
    {
        /// <summary>
        /// Room for any date, time, duration or small formatted value on the stack
        /// </summary>
        private const int StackBytes = 128;

        private static readonly JsonWriterOptions s_jsonOptions = new()
        {
            Indented = false,
            Encoder = SerializeJsonLinesOptions.Encoder,
            SkipValidation = false
        };

        private Utf8JsonWriter? _json;

        /// <summary>
        /// The JSON-encoded member names of the telemetry being written
        /// </summary>
        private JsonEncodedText[] _names = [];

        /// <summary>
        /// The member being written, for messages
        /// </summary>
        private PropertyDefinition? _member;

        private Utf8JsonWriter Json => _json!;

        protected override void OnReset()
        {
            if (_json is null)
            {
                _json = new Utf8JsonWriter(Output, s_jsonOptions);
            }
            else
            {
                _json.Reset(Output);
            }

            _member = null;
        }

        #region Refused (R66)

        public override Span<byte> GetSpan(int sizeHint = 0) => throw Throwing.DirectBufferUse();

        public override Memory<byte> GetMemory(int sizeHint = 0) => throw Throwing.DirectBufferUse();

        public override void Advance(int count) => throw Throwing.DirectBufferUse();

        #endregion

        #region Framing

        public override void WriteStartTelemetry(TypeDefinition definition)
        {
            _names = options.NamesOf(definition);
            Json.WriteStartObject();
        }

        public override void WriteName(PropertyDefinition property)
        {
            _member = property;
            Json.WritePropertyName(_names[property.Index]);
        }

        public override void WriteNull(PropertyDefinition property)
        {
            _member = property;
            Json.WriteNull(_names[property.Index]);
        }

        public override void WriteEndTelemetry()
        {
            Utf8JsonWriter json = Json;
            json.WriteEndObject();
            json.Flush();

            IBufferWriter<byte> output = Output;
            output.GetSpan(1)[0] = (byte)'\n';
            output.Advance(1);

            json.Reset(output);
            _member = null;
        }

        #endregion

        #region Values

        public override void WriteValue(ReadOnlySpan<char> value) => Json.WriteStringValue(value);

        public override void WriteValue(bool value) => Json.WriteBooleanValue(value);

        public override void WriteValue(int value) => Json.WriteNumberValue(value);

        public override void WriteValue(long value) => Json.WriteNumberValue(value);

        public override void WriteValue(double value)
        {
            if (!double.IsFinite(value))
            {
                throw Throwing.NonFiniteDouble(_member, value);
            }

            Json.WriteNumberValue(value);
        }

        public override void WriteValue(decimal value) => Json.WriteNumberValue(value);

        public override void WriteValue(Guid value) => Json.WriteStringValue(value);

        public override void WriteValue(DateTime value) => Json.WriteStringValue(value);

        public override void WriteValue(DateTimeOffset value) => Json.WriteStringValue(value);

        public override void WriteValue(DateOnly value) => WriteFormattedString(value, "O");

        public override void WriteValue(TimeOnly value) => WriteFormattedString(value, "O");

        public override void WriteValue(TimeSpan value) => WriteFormattedString(value, "c");

        public override void WriteValue<T>(T value) => WriteFormattedString(value, default);

        public override void WriteEnumName(ReadOnlySpan<byte> utf8Name) => Json.WriteStringValue(utf8Name);

        /// <summary>
        /// A complete JSON value, validated: an incomplete one throws instead of writing an invalid line
        /// </summary>
        public override void WriteRaw(ReadOnlySpan<byte> utf8) => Json.WriteRawValue(utf8, skipInputValidation: false);

        #endregion

        /// <summary>
        /// Format <paramref name="value"/> invariantly and write it as a JSON string
        /// </summary>
        private void WriteFormattedString<T>(T value, ReadOnlySpan<char> format) where T : IUtf8SpanFormattable
        {
            Span<byte> buffer = stackalloc byte[StackBytes];

            if (value.TryFormat(buffer, out int written, format, CultureInfo.InvariantCulture))
            {
                Json.WriteStringValue(buffer[..written]);
                return;
            }

            for (int size = StackBytes * 4; ; size *= 2)
            {
                byte[] rented = ArrayPool<byte>.Shared.Rent(size);

                try
                {
                    if (value.TryFormat(rented, out written, format, CultureInfo.InvariantCulture))
                    {
                        Json.WriteStringValue(rented.AsSpan(0, written));
                        return;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }
    }
}
