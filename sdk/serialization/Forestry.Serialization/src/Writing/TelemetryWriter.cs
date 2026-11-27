using System.Buffers;
using System.Globalization;
using System.Text;
using Forestry.Serialization.Definitions;

namespace Forestry.Serialization.Writing
{
    /// <summary>
    /// Writes telemetries in a format: the one class a format, or a developer, overrides to decide how a
    /// telemetry is framed and how each value is represented (spec 004 research R49, constitution C0)
    /// </summary>
    /// <remarks>
    /// <para>
    /// Converters delegate to it by the type of the value, not by how one format represents it: a
    /// <see cref="Guid"/> goes to <see cref="WriteValue(Guid)"/> whether JSON quotes it, a test format writes
    /// it bare, or Parquet stores it as sixteen bytes. The typed overloads cover <see cref="string"/> (as
    /// characters), <see cref="bool"/>, <see cref="int"/>, <see cref="long"/>, <see cref="double"/>,
    /// <see cref="decimal"/>, <see cref="Guid"/>, <see cref="DateTime"/>, <see cref="DateTimeOffset"/>,
    /// <see cref="DateOnly"/>, <see cref="TimeOnly"/> and <see cref="TimeSpan"/>; an enum's name goes to
    /// <see cref="WriteEnumName"/> and its number to the integer overloads; every other value that formats to
    /// UTF-8 (e.g. a developer's struct) goes to <see cref="WriteValue{T}(T)"/>. A custom converter can also
    /// call <see cref="WriteRaw"/> or the <see cref="IBufferWriter{T}"/> members. Every method has a default
    /// that writes plain UTF-8 text with the invariant culture; a format overrides what it represents
    /// differently (spec 005 research R60).
    /// </para>
    /// <para>
    /// Overriding <see cref="WriteValue{T}(T)"/> does not change the typed overloads, nor the other way
    /// round: to customize, say, every number, override each typed overload for the numbers you write.
    /// </para>
    /// <para>
    /// The serializer frames each telemetry: <see cref="WriteStartTelemetry"/>, then for each member
    /// <see cref="WriteName"/> and its converter's <c>Write</c>, or <see cref="WriteNull"/> for a missing
    /// value, then <see cref="WriteEndTelemetry"/>. Separators between members are the writer's own state,
    /// as in <c>Utf8JsonWriter</c>. The serializer hands the writer its output for each call
    /// (<see cref="Output"/>): a pooled buffer for a <see cref="Stream"/>, or the caller's
    /// <c>PipeWriter</c>.
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
        /// <remarks>
        /// Overridable, so a format can refuse direct writes (e.g. JSON Lines, whose own writer buffers);
        /// the defaults of this class write through <see cref="Output"/>, never through these members
        /// (spec 005 research R66)
        /// </remarks>
        public virtual Span<byte> GetSpan(int sizeHint = 0) => Output.GetSpan(sizeHint);

        /// <inheritdoc/>
        /// <remarks>Overridable, as <see cref="GetSpan"/></remarks>
        public virtual Memory<byte> GetMemory(int sizeHint = 0) => Output.GetMemory(sizeHint);

        /// <inheritdoc/>
        /// <remarks>Overridable, as <see cref="GetSpan"/></remarks>
        public virtual void Advance(int count) => Output.Advance(count);

        #region Framing (called by the serializer)

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

        #endregion

        #region Values (called by converters)

        /// <summary>
        /// Text. The default writes it as UTF-8, unescaped
        /// </summary>
        /// <param name="value"></param>
        public virtual void WriteValue(ReadOnlySpan<char> value)
        {
            IBufferWriter<byte> output = Output;
            int written = Encoding.UTF8.GetBytes(value, output.GetSpan(Encoding.UTF8.GetMaxByteCount(value.Length)));
            output.Advance(written);
        }

        /// <summary>
        /// A boolean. The default writes <c>true</c> or <c>false</c>
        /// </summary>
        /// <param name="value"></param>
        public virtual void WriteValue(bool value) => WriteRaw(value ? "true"u8 : "false"u8);

        /// <summary>
        /// A 32-bit integer. The default writes its invariant text
        /// </summary>
        /// <param name="value"></param>
        public virtual void WriteValue(int value) => WriteFormatted(value, default);

        /// <summary>
        /// A 64-bit integer. The default writes its invariant text
        /// </summary>
        /// <param name="value"></param>
        public virtual void WriteValue(long value) => WriteFormatted(value, default);

        /// <summary>
        /// A double. The default writes its shortest invariant text that reads back the same
        /// </summary>
        /// <param name="value"></param>
        public virtual void WriteValue(double value) => WriteFormatted(value, default);

        /// <summary>
        /// A decimal. The default writes its invariant text
        /// </summary>
        /// <param name="value"></param>
        public virtual void WriteValue(decimal value) => WriteFormatted(value, default);

        /// <summary>
        /// A <see cref="Guid"/>. The default writes it as <c>D</c> (32 digits with hyphens)
        /// </summary>
        /// <param name="value"></param>
        public virtual void WriteValue(Guid value) => WriteFormatted(value, default);

        /// <summary>
        /// A <see cref="DateTime"/>. The default writes the round-trip format (<c>O</c>)
        /// </summary>
        /// <param name="value"></param>
        public virtual void WriteValue(DateTime value) => WriteFormatted(value, "O");

        /// <summary>
        /// A <see cref="DateTimeOffset"/>. The default writes the round-trip format (<c>O</c>)
        /// </summary>
        /// <param name="value"></param>
        public virtual void WriteValue(DateTimeOffset value) => WriteFormatted(value, "O");

        /// <summary>
        /// A <see cref="DateOnly"/>. The default writes the round-trip format (<c>O</c>, <c>yyyy-MM-dd</c>)
        /// </summary>
        /// <param name="value"></param>
        public virtual void WriteValue(DateOnly value) => WriteFormatted(value, "O");

        /// <summary>
        /// A <see cref="TimeOnly"/>. The default writes the round-trip format (<c>O</c>, <c>HH:mm:ss.fffffff</c>)
        /// </summary>
        /// <param name="value"></param>
        public virtual void WriteValue(TimeOnly value) => WriteFormatted(value, "O");

        /// <summary>
        /// A <see cref="TimeSpan"/>. The default writes the constant format (<c>c</c>,
        /// <c>[-][d.]hh:mm:ss[.fffffff]</c>)
        /// </summary>
        /// <param name="value"></param>
        public virtual void WriteValue(TimeSpan value) => WriteFormatted(value, "c");

        /// <summary>
        /// An enum member's name, as the enum converter resolved it (its C# name, or the name declared with
        /// <see cref="EnumMemberNameAttribute"/>). The default writes the bytes as they are; numbers go to the
        /// integer overloads (spec 005 research R60)
        /// </summary>
        /// <param name="utf8Name"></param>
        public virtual void WriteEnumName(ReadOnlySpan<byte> utf8Name) => WriteRaw(utf8Name);

        /// <summary>
        /// Any other value that formats itself to UTF-8 (e.g. a developer's struct). The default writes its
        /// invariant text
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        public virtual void WriteValue<T>(T value) where T : IUtf8SpanFormattable => WriteFormatted(value, default);

        /// <summary>
        /// UTF-8 bytes written as they are. The default copies them
        /// </summary>
        /// <param name="utf8"></param>
        public virtual void WriteRaw(ReadOnlySpan<byte> utf8)
        {
            IBufferWriter<byte> output = Output;
            utf8.CopyTo(output.GetSpan(utf8.Length));
            output.Advance(utf8.Length);
        }

        /// <summary>
        /// Format <paramref name="value"/> with <paramref name="format"/> and the invariant culture into the
        /// output, doubling the size hint while the room offered is too small: no "did not fit" result
        /// reaches the caller (research R45)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="format">A standard or custom format, e.g. <c>O</c> for round-trip dates</param>
        protected void WriteFormatted<T>(T value, ReadOnlySpan<char> format) where T : IUtf8SpanFormattable
        {
            IBufferWriter<byte> output = Output;
            int hint = FirstHint;

            while (true)
            {
                Span<byte> span = output.GetSpan(hint);

                if (value.TryFormat(span, out int written, format, CultureInfo.InvariantCulture))
                {
                    output.Advance(written);
                    return;
                }

                hint = checked(Math.Max(hint, span.Length) * 2);
            }
        }

        #endregion
    }
}
