using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Deserializers
{
    /// <summary>
    /// Deserialize <see cref="Value"/> when <see cref="Deserializer.DeserializerKind"/> is not 
    /// None or Object
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract partial class Deserializer<T>: Deserializer
    {

        /// <summary>
        /// Try read <see cref="Value"/> - delegates to <see cref="TryReadNullableValue{TBuffering, TStream}"/>,
        /// which checks the buffering itself, short-circuiting with <see cref="ReadingStatus.Partial"/>
        /// when there isn't enough buffered yet rather than being asked in a separate call
        /// </summary>
        /// <returns></returns>
        internal override ReadingStatus TryReadValue<TBuffering, TStream>(ref TBuffering buffering, scoped ref ReaderPath readerPath, out Value? value, DeserializeOptions options, CancellationToken cancellationToken = default)
        {
            ReadingStatus status = TryReadNullableValue<TBuffering, TStream>(ref buffering, typeof(T), out Value<T> nullableValue, options);
            value = status == ReadingStatus.Value ? nullableValue.GetValue() : null;

            // TODO: Update reader path and position

            return status;
        }

        /// <summary>
        /// Checks the buffering, short-circuiting with <see cref="ReadingStatus.Partial"/> (and
        /// <paramref name="value"/> unset) when there isn't enough buffered yet to make progress;
        /// otherwise tries to find a value for <paramref name="type"/>, returning
        /// <see cref="ReadingStatus.Value"/>/<see cref="ReadingStatus.NoValue"/> accordingly
        /// </summary>
        /// <typeparam name="TBuffering"></typeparam>
        /// <typeparam name="TStream"></typeparam>
        /// <param name="buffering"></param>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public abstract ReadingStatus TryReadNullableValue<TBuffering, TStream>(ref TBuffering buffering, Type type, out Value<T> value, DeserializeOptions options) where TBuffering : struct, IBuffering<TBuffering, TStream>;

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