using System.Buffers;
using System.Globalization;
using System.Text;
using Forestry.Serialization.Definitions;

namespace Forestry.Serialization.Writing
{
    /// <summary>
    /// Writes telemetries in a format: the one class a format, or a developer, overrides to decide how a
    /// telemetry is framed and how each kind of value is written (spec 004 research R49, constitution C0)
    /// </summary>
    /// <remarks>
    /// <para>
    /// Converters delegate to it: a <c>string</c> converter calls <see cref="WriteString"/>, a
    /// <c>bool</c> converter <see cref="WriteBoolean"/>, numbers, <see cref="Guid"/> and dates
    /// <see cref="WriteFormattable{T}"/>. A custom converter calls whichever fits, <see cref="WriteRaw"/>,
    /// or the <see cref="IBufferWriter{T}"/> members for raw bytes. Every value method has a default that
    /// writes plain UTF-8 text; a format overrides what it writes differently (e.g. JSON quotes and
    /// escapes strings).
    /// </para>
    /// <para>
    /// The serializer frames each telemetry: <see cref="WriteStartTelemetry"/>, then for each member
    /// <see cref="WriteName"/> and its converter's <c>Write</c>, or <see cref="WriteNull"/> for a missing
    /// value, then <see cref="WriteEndTelemetry"/>. Separators between members are the writer's own state,
    /// as in <c>Utf8JsonWriter</c>. The serializer hands the writer its output for each call
    /// (<see cref="Output"/>): a pooled buffer for a <see cref="Stream"/>, or the caller's
    /// <c>PipeWriter</c>.
    /// </para>
    /// <para>
    /// Typed overloads per kind of number (as <c>Utf8JsonWriter.WriteNumberValue</c> has for
    /// <see cref="int"/>, <see cref="decimal"/>, ...) may be added later; today every UTF-8 formattable
    /// value goes through <see cref="WriteFormattable{T}"/>.
    /// </para>
    /// </remarks>
    public abstract class TelemetryWriter : IBufferWriter<byte>
    {
        /// <summary>
        /// The first size hint for a formatted value; most single values fit
        /// </summary>
        private const int FirstHint = 64;

        private IBufferWriter<byte>? _output;

        /// <summary>
        /// A writer for a format
        /// </summary>
        protected TelemetryWriter()
        {
        }

        /// <summary>
        /// Where the bytes go for the current call
        /// </summary>
        /// <exception cref="InvalidOperationException">No serializer call is writing through this writer</exception>
        protected IBufferWriter<byte> Output => _output ?? Throwing.WhenWriterHasNoOutput();

        /// <summary>
        /// Start writing to <paramref name="output"/>: called by the serializer before the first telemetry of
        /// a call
        /// </summary>
        /// <param name="output"></param>
        internal void Reset(IBufferWriter<byte> output)
        {
            _output = output;
            OnReset();
        }

        /// <summary>
        /// Clear any state kept between telemetries of a previous call (e.g. whether a member was written)
        /// </summary>
        protected virtual void OnReset()
        {
        }

        /// <inheritdoc/>
        public Span<byte> GetSpan(int sizeHint = 0) => Output.GetSpan(sizeHint);

        /// <inheritdoc/>
        public Memory<byte> GetMemory(int sizeHint = 0) => Output.GetMemory(sizeHint);

        /// <inheritdoc/>
        public void Advance(int count) => Output.Advance(count);

        /// <summary>
        /// Before the first member of a telemetry (e.g. <c>{</c> in JSON). The default writes nothing
        /// </summary>
        /// <param name="definition">The telemetry's type definition</param>
        public virtual void WriteStartTelemetry(TypeDefinition definition)
        {
        }

        /// <summary>
        /// After the last member of a telemetry (e.g. <c>}</c> and a new line in JSON Lines). The default
        /// writes nothing
        /// </summary>
        public virtual void WriteEndTelemetry()
        {
        }

        /// <summary>
        /// Before a member's value: its name and any separator from the previous member (e.g. <c>,"LogKey":</c>
        /// in JSON). The default writes nothing
        /// </summary>
        /// <param name="property"></param>
        public virtual void WriteName(PropertyDefinition property)
        {
        }

        /// <summary>
        /// A member whose value is missing (a null <c>string</c>, a nullable without a value), in place of
        /// <see cref="WriteName"/> and the value, so a format can also leave the member out. The default
        /// writes the name and no value
        /// </summary>
        /// <param name="property"></param>
        public virtual void WriteNull(PropertyDefinition property) => WriteName(property);

        /// <summary>
        /// A text value. The default writes it as UTF-8, unescaped
        /// </summary>
        /// <param name="value"></param>
        public virtual void WriteString(ReadOnlySpan<char> value)
        {
            int written = Encoding.UTF8.GetBytes(value, GetSpan(Encoding.UTF8.GetMaxByteCount(value.Length)));
            Advance(written);
        }

        /// <summary>
        /// A boolean value. The default writes <c>true</c> or <c>false</c>
        /// </summary>
        /// <param name="value"></param>
        public virtual void WriteBoolean(bool value) => WriteRaw(value ? "true"u8 : "false"u8);

        /// <summary>
        /// A value that formats itself to UTF-8: numbers, <see cref="Guid"/>, dates, a developer's struct.
        /// The default formats with <paramref name="format"/> and the invariant culture, asking for more
        /// room until it fits
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="format">A standard or custom format, e.g. <c>O</c> for round-trip dates</param>
        public virtual void WriteFormattable<T>(T value, ReadOnlySpan<char> format = default) where T : IUtf8SpanFormattable
        {
            int hint = FirstHint;

            while (true)
            {
                Span<byte> span = GetSpan(hint);

                if (value.TryFormat(span, out int written, format, CultureInfo.InvariantCulture))
                {
                    Advance(written);
                    return;
                }

                hint = checked(Math.Max(hint, span.Length) * 2);
            }
        }

        /// <summary>
        /// UTF-8 bytes written as they are. The default copies them
        /// </summary>
        /// <param name="utf8"></param>
        public virtual void WriteRaw(ReadOnlySpan<byte> utf8)
        {
            utf8.CopyTo(GetSpan(utf8.Length));
            Advance(utf8.Length);
        }
    }
}
