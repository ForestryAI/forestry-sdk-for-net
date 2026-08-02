using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize
{
    /// <summary>
    /// Buffering a stream into values <see cref="Value"/> from the type <see cref="TType"/> 
    /// </summary>
    public sealed class ValueAsyncEnumerable<TType, TBuffering, TStream>: IAsyncEnumerable<Value> where TBuffering : struct, IBuffering<TBuffering, TStream> 
    {
        public ValueAsyncEnumerable(
            TypeDefinition<TType> typeDefinition,
            TBuffering buffering,
            TStream stream,
            ReadErrorHandling errorHandling = ReadErrorHandling.ShortCircuit
        ) {
            Throwing.ArguementIsNull(typeDefinition, nameof(typeDefinition));
            Throwing.ArguementIsNull(buffering, nameof(buffering));
            Throwing.ArguementIsNull(stream, nameof(stream));

            _typeDefinition = typeDefinition;
            _buffering = buffering;
            _stream = stream;
            _errorHandling = errorHandling;
        }

        private readonly TypeDefinition<TType> _typeDefinition;

        private readonly TBuffering _buffering;

        private readonly TStream _stream;

        private readonly ReadErrorHandling _errorHandling;

        public IAsyncEnumerator<Value> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new ValueAsyncEnumerator<TType, TBuffering, TStream>(_typeDefinition, _buffering, _stream, _errorHandling, cancellationToken);
    }
}
