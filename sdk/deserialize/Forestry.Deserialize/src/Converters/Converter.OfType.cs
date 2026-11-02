namespace Forestry.Deserialize.Converters
{
    /// <summary>
    /// Converts UTF-8 text into a <typeparamref name="T"/>
    /// </summary>
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

        /// <summary>
        /// Convert a complete, non-empty value; throw <see cref="FormatException"/> when the text is not a
        /// <typeparamref name="T"/>
        /// </summary>
        /// <param name="utf8"></param>
        /// <returns></returns>
        public abstract T Convert(ReadOnlySpan<byte> utf8);
    }
}
