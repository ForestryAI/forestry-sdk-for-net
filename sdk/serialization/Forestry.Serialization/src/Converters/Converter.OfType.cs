using System.Reflection;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Converters
{
    /// <summary>
    /// Converts UTF-8 text into a <typeparamref name="T"/> (<see cref="Read"/>) and a
    /// <typeparamref name="T"/> into UTF-8 text (<see cref="Write"/>)
    /// </summary>
    /// <remarks>
    /// One converter serves both directions; the options it is added to decide which method is called:
    /// in <see cref="DeserializeOptions"/>, which read, its <see cref="Read"/>; in serialize options, which
    /// write, its <see cref="Write"/> (spec 004 research R44).
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    public abstract class Converter<T> : Converter
    {
        /// <summary>
        /// Converter for <typeparamref name="T"/>
        /// </summary>
        protected Converter()
        {
        }

        /// <summary>
        /// Handles exactly <typeparamref name="T"/>
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public override bool CanConvert(Type type) => type == typeof(T);

        internal override bool CanWrite =>
            GetType().GetMethod(nameof(Write), BindingFlags.Public | BindingFlags.Instance, [typeof(T), typeof(TelemetryWriter)])!.DeclaringType != typeof(Converter<T>);

        /// <summary>
        /// Read a complete, non-empty value; throw <see cref="FormatException"/> when the text is not a
        /// <typeparamref name="T"/>. Called when deserializing, through <see cref="DeserializeOptions"/>
        /// </summary>
        /// <param name="utf8"></param>
        /// <returns></returns>
        public abstract T Read(ReadOnlySpan<byte> utf8);

        /// <summary>
        /// Write <paramref name="value"/> through <paramref name="writer"/>, delegating to its overload for the
        /// value's type (<see cref="TelemetryWriter.WriteValue(ReadOnlySpan{char})"/>,
        /// <see cref="TelemetryWriter.WriteValue(bool)"/>, <see cref="TelemetryWriter.WriteValue(Guid)"/>, ...,
        /// or <see cref="TelemetryWriter.WriteValue{T}(T)"/>) so the format, or a developer's writer, decides how
        /// it is represented; or as raw UTF-8 through the writer's <see cref="System.Buffers.IBufferWriter{T}"/>
        /// members, asking for more room instead of failing (spec 004 research R45). Called when serializing,
        /// through serialize options
        /// </summary>
        /// <remarks>
        /// Never called with null: the format writes a missing value itself (research R50). The default
        /// throws <see cref="NotSupportedException"/>, so a converter that only reads keeps compiling;
        /// serialize options refuse such a converter when they lock, so the default is never reached while
        /// writing telemetries.
        /// </remarks>
        /// <param name="value">A value that is there, never null</param>
        /// <param name="writer">The format's writer</param>
        /// <exception cref="NotSupportedException">The converter only reads</exception>
        public virtual void Write(T value, TelemetryWriter writer)
        {
            Throwing.WhenConverterCannotWrite(GetType(), typeof(T));
        }
    }
}
