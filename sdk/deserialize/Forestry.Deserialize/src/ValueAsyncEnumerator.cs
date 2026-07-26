namespace Forestry.Deserialize
{
    /// <summary>
    /// <see cref="Value"/> enumerator 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ValueAsyncEnumerator<Value>: IAsyncEnumerator<Value>
    {
        internal ValueAsyncEnumerator(
            TypeDefinition typeDefinition,
            CancellationToken cancellationToken = default
        )
        {
            _readStack = default!;
            _cancellationToken = cancellationToken;
        }

        private IReadStack _readStack { get; }

        private CancellationToken _cancellationToken { get; }

        /// <summary>
        /// Current <see cref="Value"/>
        /// </summary>
        /// <value></value>
        public required Value Current { get; set; }

        /// <summary>
        /// Move next with the stack and reader targeting the 
        /// passed type getting help from options
        /// </summary>
        /// <param name="readStack"></param>
        /// <param name="reader"></param>
        /// <param name="type"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public ValueTask<bool> MoveNextAsync()
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                // TODO: Dispose resources
                return ValueTask.FromResult(false);
            }
            
            return ValueTask.FromResult(true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }
    }
}
