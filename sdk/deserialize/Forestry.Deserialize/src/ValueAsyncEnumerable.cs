namespace Forestry.Deserialize
{
    /// <summary>
    /// Streams the <see cref="Value"/>s an <see cref="IReader"/> produces, one at a time.
    /// </summary>
    public sealed class ValueAsyncEnumerable: IAsyncEnumerable<Value>
    {
        public ValueAsyncEnumerable(
            IReader reader,
            ReadErrorHandling errorHandling = ReadErrorHandling.ShortCircuit
        ) {
            ArgumentNullException.ThrowIfNull(reader);

            _reader = reader;
            _errorHandling = errorHandling;
        }

        private readonly IReader _reader;

        private readonly ReadErrorHandling _errorHandling;

        public IAsyncEnumerator<Value> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new ValueAsyncEnumerator(_reader, _errorHandling, cancellationToken);
    }
}
