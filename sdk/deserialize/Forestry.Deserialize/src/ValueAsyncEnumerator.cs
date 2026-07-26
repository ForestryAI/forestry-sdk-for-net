namespace Forestry.Deserialize
{
    /// <summary>
    /// Pulls <see cref="Value"/> instances one at a time from an <see cref="IReader"/>,
    /// applying a <see cref="ReadErrorHandling"/> policy when reading fails.
    /// </summary>
    public sealed class ValueAsyncEnumerator: IAsyncEnumerator<Value>
    {
        internal ValueAsyncEnumerator(
            IReader reader,
            ReadErrorHandling errorHandling = ReadErrorHandling.ShortCircuit,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(reader);

            _reader = reader;
            _errorHandling = errorHandling;
            _cancellationToken = cancellationToken;
        }

        private readonly IReader _reader;

        private readonly ReadErrorHandling _errorHandling;

        private readonly CancellationToken _cancellationToken;

        /// <summary>
        /// Value produced by the most recent successful <see cref="MoveNextAsync"/>
        /// </summary>
        public Value Current { get; private set; } = null!;

        /// <summary>
        /// Advance to the next value. Under <see cref="ReadErrorHandling.ShuntAside"/> a failing
        /// read is skipped in favor of the next one; under <see cref="ReadErrorHandling.ShortCircuit"/>
        /// (the default) the first failure propagates and enumeration stops.
        /// </summary>
        public ValueTask<bool> MoveNextAsync()
        {
            while (true)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                bool hasNext;
                try
                {
                    hasNext = _reader.Read();
                }
                catch when (_errorHandling == ReadErrorHandling.ShuntAside)
                {
                    continue;
                }

                if (!hasNext)
                {
                    Current = null!;
                    return ValueTask.FromResult(false);
                }

                Current = _reader.Current;
                return ValueTask.FromResult(true);
            }
        }

        public ValueTask DisposeAsync()
        {
            Current?.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
