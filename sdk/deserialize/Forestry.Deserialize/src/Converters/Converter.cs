namespace Forestry.Deserialize.Converters
{
    /// <summary>
    /// Converts a value the media hands over as UTF-8 text into a member's type, independent of any
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
    }
}
