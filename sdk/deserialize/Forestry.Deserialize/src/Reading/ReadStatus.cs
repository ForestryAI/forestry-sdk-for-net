namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// What a media's read call achieved (research R2)
    /// </summary>
    public enum ReadStatus : byte
    {
        /// <summary>
        /// No row and nothing more to read: the move returns false
        /// </summary>
        None = 0,

        /// <summary>
        /// No row yet: more bytes are needed
        /// </summary>
        Partial = 1,

        /// <summary>
        /// A row was completed during the call; it becomes the enumerator's current row
        /// </summary>
        Complete = 2
    }
}
