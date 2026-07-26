namespace Forestry.Deserialize
{
    /// <summary>
    /// Governs how a <see cref="ValueAsyncEnumerator"/> reacts when <see cref="IReader.Read"/> throws.
    /// Ordered media makes both options meaningful: fail fast, or set the bad value aside and
    /// keep reading the rest.
    /// </summary>
    public enum ReadErrorHandling
    {
        /// <summary>
        /// Stop enumerating and rethrow the first failure (the default)
        /// </summary>
        ShortCircuit,

        /// <summary>
        /// Skip the value that failed to read and continue with the next one
        /// </summary>
        ShuntAside
    }
}
