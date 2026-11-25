namespace Forestry.Serialization.Reading
{
    /// <summary>
    /// What a format's read call achieved (research R2)
    /// </summary>
    public enum ReadStatus : byte
    {
        /// <summary>
        /// No telemetry and nothing more to read: the move returns false
        /// </summary>
        None = 0,

        /// <summary>
        /// No telemetry yet: more bytes are needed
        /// </summary>
        Partial = 1,

        /// <summary>
        /// A telemetry was completed during the call; it becomes the enumerator's current telemetry
        /// </summary>
        Complete = 2
    }
}
