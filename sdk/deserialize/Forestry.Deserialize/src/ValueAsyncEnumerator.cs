using System.Diagnostics;
using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Reading;
    
namespace Forestry.Deserialize
{
    /// <summary>
    /// Enumerate <see cref="Value"/> starting from <see cref="TType"/> 
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
        /// Current <see cref="Value"/> 
        /// </summary>
        public Value Current { get; private set; } = null!;

        /// <summary>
        /// Move to next <see cref="Value"/> only async buffering with partial reads
        /// </summary>
        public async ValueTask<bool> MoveNextAsync() 
        {
            // TODO: Failing read skipped <see cref="ReadErrorHandling.ShuntAside"/> rather than default <see cref="ReadErrorHandling.ShortCircuit"/>
            while (true)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                ReadingStatus status =_readerPath.Position.TypeDefinition.Deserializer.TryReadValue<TBuffering, TStream>(ref _buffering, ref _readerPath, out Value? value, _typeDefinition.Options, _cancellationToken);

                if (status == ReadingStatus.Value) { Current = value!; return true; }
                if (status == ReadingStatus.NoValue) { Current = null!; return false; }

                _buffering = await _buffering.ReadAsync(_stream, _cancellationToken, maximum: true).ConfigureAwait(false);                
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
