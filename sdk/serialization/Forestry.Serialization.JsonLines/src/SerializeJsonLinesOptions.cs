using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.JsonLines.Converters;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.JsonLines
{
    /// <summary>
    /// Options to serialize telemetries as <see href="https://jsonlines.org/">JSON Lines</see>: each telemetry
    /// one JSON object on its own line (spec 005, #138)
    /// </summary>
    /// <remarks>
    /// <para>
    /// Members are written by their C# names, in definition order. Text, <see cref="Guid"/>, dates, times,
    /// durations and enum names are JSON strings; numbers and booleans are bare; a member without a value is
    /// <c>null</c>. Strings are escaped as JSON requires and no more: non-ASCII text stays plain UTF-8. Enums
    /// are written as names unless a member says otherwise with <see cref="JsonLinesEnumFormAttribute"/>.
    /// </para>
    /// <para>
    /// A <see cref="double"/> that is NaN or infinite has no JSON form and is refused with a
    /// <see cref="NotSupportedException"/>. Converters write through the writer's value methods
    /// (<c>WriteValue</c>, <c>WriteRaw</c> with a complete JSON value); writing bytes directly through its
    /// <see cref="System.Buffers.IBufferWriter{T}"/> members is refused.
    /// </para>
    /// </remarks>
    public sealed class SerializeJsonLinesOptions : SerializeOptions
    {
        /// <summary>
        /// The encoder for names and strings: only what JSON requires is escaped (output for blob storage and
        /// Azure Data Explorer, never embedded in HTML)
        /// </summary>
        internal static JavaScriptEncoder Encoder => JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

        private readonly Converter[] _formatConverters = [new JsonLinesEnumConverterFactory()];

        /// <summary>
        /// Member names, JSON-encoded once per type definition, by <see cref="PropertyDefinition.Index"/>
        /// </summary>
        private readonly ConcurrentDictionary<TypeDefinition, JsonEncodedText[]> _names = new();

        /// <summary>
        /// JSON Lines options with no telemetry type added yet
        /// </summary>
        public SerializeJsonLinesOptions()
        {
        }

        /// <inheritdoc/>
        protected override IEnumerable<Converter> FormatConverters => _formatConverters;

        /// <inheritdoc/>
        protected override TelemetryWriter CreateWriter() => new JsonLinesTelemetryWriter(this);

        /// <summary>
        /// The JSON-encoded member names of <paramref name="definition"/>
        /// </summary>
        internal JsonEncodedText[] NamesOf(TypeDefinition definition) => _names.GetOrAdd(definition, static definition =>
        {
            JsonEncodedText[] names = new JsonEncodedText[definition.Properties.Count];

            foreach (PropertyDefinition property in definition.Properties)
            {
                names[property.Index] = JsonEncodedText.Encode(property.Utf8Name.Span, Encoder);
            }

            return names;
        });
    }
}
