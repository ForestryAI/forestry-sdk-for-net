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
        /// Try read <see cref="Value"/> - a thin pass-through to
        /// <see cref="TryReadNullableValue{TBuffering, TStream}"/>. The reader path/position
        /// update does not happen here - it can't, generically, since only the concrete media
        /// reader for <typeparamref name="T"/> knows, from the raw tokens it reads, which
        /// property was just consumed and when one is complete
        /// </summary>
        /// <returns></returns>
        internal override ReadingStatus TryReadValue<TBuffering, TStream>(ref TBuffering buffering, scoped ref ReaderPath readerPath, out Value? value, DeserializeOptions options, CancellationToken cancellationToken = default)
        {
            ReadingStatus status = TryReadNullableValue<TBuffering, TStream>(ref buffering, ref readerPath, typeof(T), out Value<T> nullableValue, options);
            value = status == ReadingStatus.Value ? nullableValue.GetValue() : null;

            return status;
        }

        /// <summary>
        /// Media-specific: reads from the buffer at the current <paramref name="readerPath"/>
        /// position and is itself responsible for updating that path/position - e.g. for an
        /// object, matching the next raw token against a property, marking it read, and moving
        /// the position to reflect that. Only the concrete reader for a given media can do this;
        /// it can't be done generically, since it depends on reading real tokens (property order
        /// in the media need not match declaration order, attributes vs. elements are visited
        /// differently, etc.). Returns <see cref="ReadingStatus.Partial"/> (<paramref
        /// name="value"/> unset) when there isn't enough buffered yet to make progress,
        /// <see cref="ReadingStatus.NoValue"/> when nothing further remains to read, or
        /// <see cref="ReadingStatus.Value"/> with <paramref name="value"/> set.
        /// </summary>
        /// <typeparam name="TBuffering"></typeparam>
        /// <typeparam name="TStream"></typeparam>
        /// <param name="buffering"></param>
        /// <param name="readerPath"></param>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public abstract ReadingStatus TryReadNullableValue<TBuffering, TStream>(ref TBuffering buffering, scoped ref ReaderPath readerPath, Type type, out Value<T> value, DeserializeOptions options) where TBuffering : struct, IBuffering<TBuffering, TStream>;

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