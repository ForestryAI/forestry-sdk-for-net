using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Converters
{
    /// <summary>
    /// Writes a whole telemetry of type <typeparamref name="T"/>: a converter for a telemetry type, resolved like
    /// any converter (the developer's <c>Converters</c>, the format's <c>FormatConverters</c>, then the default,
    /// which writes each property in turn). Register one to replace how a telemetry type is walked, e.g. a
    /// columnar format routing members to columns (spec 004 research R49; System.Text.Json's
    /// <c>JsonConverter&lt;T&gt;</c> for an object)
    /// </summary>
    /// <remarks>
    /// The writer still frames telemetries and represents values; an object converter decides what is
    /// written for one telemetry, through the writer. The read side is deferred: <see cref="Read"/> throws,
    /// and reading keeps its enumerators.
    /// </remarks>
    /// <typeparam name="T">A telemetry type</typeparam>
    public abstract class ObjectConverter<T> : Converter<T> where T : Telemetry
    {
        /// <summary>
        /// An object converter for <typeparamref name="T"/>
        /// </summary>
        protected ObjectConverter()
        {
        }

        /// <summary>
        /// Not supported yet: telemetries are read through the format's enumerators (spec 004 research R49)
        /// </summary>
        /// <param name="utf8"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">Always</exception>
        public sealed override T Read(ReadOnlySpan<byte> utf8) =>
            throw new NotSupportedException(Formatting.Format(Formatting.ObjectConverterCannotRead, Throwing.DisplayName(GetType())));

        /// <summary>
        /// Write <paramref name="value"/>, a whole telemetry, through <paramref name="writer"/>
        /// </summary>
        /// <param name="value"></param>
        /// <param name="writer"></param>
        public abstract override void Write(T value, TelemetryWriter writer);
    }
}
