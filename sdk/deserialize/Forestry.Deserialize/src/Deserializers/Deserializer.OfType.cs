using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Deserializers
{
    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract partial class Deserializer<T>: Deserializer
    {

        /// <summary>
        /// Try read value. Only updates the reader path - the Enumerator reads whatever was
        /// captured back off the path on the following MoveNext call, not from this method's
        /// return.
        /// </summary>
        /// <returns></returns>
        internal override bool TryReadValue<TBuffering, TStream>(ref TBuffering buffering, scoped ref ReaderPath readerPath, out Value? value, DeserializeOptions options, CancellationToken cancellationToken = default)
        {
            Value<T> nullableValue = ReadValue<TBuffering, TStream>(ref buffering, typeof(T), options);
            // TODO: Update readerPath

            value = nullableValue.HasValue ? nullableValue.GetValue() : null;
            return nullableValue.HasValue;
        }

        /// <summary>
        /// Create reader state from the buffer then create the reader
        /// </summary>
        /// <typeparam name="TBuffering"></typeparam>
        /// <typeparam name="TStream"></typeparam>
        /// <param name="buffering"></param>
        /// <param name="type"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public abstract Value<T> ReadValue<TBuffering, TStream>(ref TBuffering buffering, Type type,  DeserializeOptions options) where TBuffering : struct, IBuffering<TBuffering, TStream>;

        /// <summary>
        ///
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public override bool CanDeserialize(Type type)
        {
            return type == typeof(T);
        }
    }
}