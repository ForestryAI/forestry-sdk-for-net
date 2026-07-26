namespace Forestry.Deserialize
{
    public static class ReaderExtensions
    {
        /// <summary>
        /// Wraps this reader so its values can be streamed with <c>await foreach</c>
        /// </summary>
        public static IAsyncEnumerable<Value> AsAsyncEnumerable(
            this IReader reader,
            ReadErrorHandling errorHandling = ReadErrorHandling.ShortCircuit
        ) => new ValueAsyncEnumerable(reader, errorHandling);
    }
}
