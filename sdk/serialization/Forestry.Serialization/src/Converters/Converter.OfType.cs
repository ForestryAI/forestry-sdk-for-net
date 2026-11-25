using System.Buffers;

namespace Forestry.Serialization.Converters
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
        /// Read a complete, non-empty value; throw <see cref="FormatException"/> when the text is not a
        /// <typeparamref name="T"/>
        /// </summary>
        /// <param name="utf8"></param>
        /// <returns></returns>
        public abstract T Read(ReadOnlySpan<byte> utf8);

        /// <summary>
        /// Write <paramref name="value"/> as UTF-8 into <paramref name="utf8"/>: ask for room with
        /// <see cref="IBufferWriter{T}.GetSpan(int)"/>, format into it and call
        /// <see cref="IBufferWriter{T}.Advance(int)"/>; when the room offered is too small, ask again with a
        /// larger size hint instead of failing (spec 004 research R45)
        /// </summary>
        /// <remarks>
        /// Never called with null: the format writes a missing value itself (research R50). The default
        /// throws <see cref="NotSupportedException"/>, so a converter that only reads keeps compiling;
        /// serialize options refuse such a converter when they lock, so the default is never reached while
        /// writing telemetries.
        /// </remarks>
        /// <param name="value">A value that is there, never null</param>
        /// <param name="utf8">Where the UTF-8 text goes</param>
        /// <exception cref="NotSupportedException">The converter only reads</exception>
        public virtual void Write(T value, IBufferWriter<byte> utf8)
        {
            Throwing.WhenConverterCannotWrite(GetType(), typeof(T));
        }
    }
}
