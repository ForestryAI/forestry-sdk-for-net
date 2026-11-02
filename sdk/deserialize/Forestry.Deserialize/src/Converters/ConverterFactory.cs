namespace Forestry.Deserialize.Converters
{
    /// <summary>
    /// Creates converters for a family of types (e.g. <see cref="Nullable{T}"/>), once per type when
    /// definitions are built (research R14)
    /// </summary>
    public abstract class ConverterFactory : Converter
    {
        /// <summary>
        /// Converter factory
        /// </summary>
        protected ConverterFactory()
        {
        }

        /// <summary>
        /// Create the converter for <paramref name="type"/>, a <see cref="Converter{T}"/> with T == type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="options">Options, to resolve the converters a created converter depends on</param>
        /// <returns></returns>
        public abstract Converter CreateConverter(Type type, DeserializeOptions options);
    }
}
