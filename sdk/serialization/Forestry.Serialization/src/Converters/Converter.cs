namespace Forestry.Serialization.Converters
{
    /// <summary>
    /// Converts a value the format hands over as UTF-8 text into a member's type, independent of any
    /// reader (research R14). Derive from <see cref="Converter{T}"/> for one type or from
    /// <see cref="ConverterFactory"/> for a family of types
    /// </summary>
    public abstract class Converter
    {
        private protected Converter()
        {
        }

        /// <summary>
        /// Whether this converter (or factory) handles <paramref name="type"/>
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public abstract bool CanConvert(Type type);

        /// <summary>
        /// Whether the converter writes, i.e. overrides <c>Write</c> (spec 004 research R45): serialize options
        /// refuse one that does not when they lock
        /// </summary>
        internal virtual bool CanWrite => false;
    }
}
