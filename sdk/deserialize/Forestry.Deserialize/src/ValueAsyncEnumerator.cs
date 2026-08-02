using System.Diagnostics;
using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Reading;
    
namespace Forestry.Deserialize
{
    /// <summary>
    /// Enumerates value <see cref="Value"/> from the type <see cref="TType"/> 
    /// </summary>
    public sealed class ValueAsyncEnumerator<TType, TBuffering, TStream>: IAsyncEnumerator<Value> where TBuffering : struct, IBuffering<TBuffering, TStream>
    {
        internal ValueAsyncEnumerator(
            TypeDefinition<TType> typeDefinition,
            TBuffering buffering,
            TStream stream,
            ReadErrorHandling errorHandling = ReadErrorHandling.ShortCircuit,
            CancellationToken cancellationToken = default
        )
        {
            Throwing.ArguementIsNull(typeDefinition, nameof(typeDefinition));
            Throwing.ArguementIsNull(buffering, nameof(buffering));
            Throwing.ArguementIsNull(stream, nameof(stream));

            _typeDefinition = typeDefinition;
            _buffering = buffering;
            _stream = stream;
            _errorHandling = errorHandling;
            _cancellationToken = cancellationToken;

            Debug.Assert(_typeDefinition.IsConfigured, "Type definition must be configured before enumerating values");
            _readerPath = default;
            _readerPath.SetPosition(_typeDefinition, useContinuation: false);
        }

        private readonly TypeDefinition<TType> _typeDefinition;

        private TBuffering _buffering;

        private TStream _stream;

        private readonly ReadErrorHandling _errorHandling;

        private readonly CancellationToken _cancellationToken;

        private ReaderPath _readerPath = default;

        /// <summary>
        /// Value produced by the most recent successful <see cref="MoveNextAsync"/>
        /// </summary>
        public Value Current { get; private set; } = null!;

        /// <summary>
        /// Advance to the next value. Under <see cref="ReadErrorHandling.ShuntAside"/> a failing
        /// read is skipped in favor of the next one; under <see cref="ReadErrorHandling.ShortCircuit"/>
        /// (the default) the first failure propagates and enumeration stops.
        /// </summary>
        public async ValueTask<bool> MoveNextAsync()
        {
            while (true)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                _buffering = await _buffering.ReadAsync(_stream, _cancellationToken, maximum: true).ConfigureAwait(false);
                bool readablePosition = TryReadPosition(ref _buffering, ref _stream);

                if (readablePosition)
                {
                    bool hasValue =_readerPath.Position.TypeDefinition.Deserializer.TryReadValue<TBuffering, TStream>(ref _buffering, ref _readerPath, out Value? value, _typeDefinition.Options, _cancellationToken);
                    Current = hasValue ? value! : default!;

                    // TODO: adjust reader path
                    return hasValue;
                }
                {
                    continue;
                }
            }
        }

        private bool TryReadPosition(ref TBuffering buffering, ref TStream stream)
        {
            try {
                return false;
            } finally
            {
                // TODO: advance buffering by the number of bytes used
            }
        }

        public ValueTask DisposeAsync()
        {
            Current?.Dispose();
            _buffering.Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
