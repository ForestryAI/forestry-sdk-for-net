namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// Reading status when deserializing
    /// </summary>
    public enum ReadingStatus : byte
    {
        /// <summary>
        /// Read no value when deserializing
        /// </summary>
        NoValue,

        /// <summary>
        /// Read value when deserializing
        /// </summary>
        Value,

        /// <summary>
        /// Partial read needing more buffering when deserializing
        /// </summary>
        Partial
    }
}