namespace Forestry.Serialization.Reading
{
    /// <summary>
    /// Policies kept more rows in the deserialization context than
    /// <see cref="DeserializeOptions.MaxContextRows"/> allows (spec 002 FR-011a, S1). The message gives the
    /// limit, the kept rows per row type, and how to raise the limit
    /// </summary>
    public sealed class DeserializeContextLimitException : InvalidOperationException
    {
        /// <summary>
        /// Limit reached
        /// </summary>
        /// <param name="message"></param>
        public DeserializeContextLimitException(string message) : base(message)
        {
        }
    }
}
