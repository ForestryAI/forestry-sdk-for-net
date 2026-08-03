namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// Reader state (reader position) to recreate a reader.
    /// </summary>
    public interface IReaderState<TState> where TState : struct, IReaderState<TState>
    {
        /// <summary>
        /// Reader position line number
        /// </summary>
        public long ReaderPositionLineNumber { get; }

        /// <summary>
        /// Reader position name
        /// </summary>
        public string ReaderPositionName { get; }

        /// <summary>
        /// Reader position (byte) in line
        /// </summary>
        public long ReaderPosition { get; }

        /// <summary>
        /// Is reader position in an object
        /// </summary>
        public bool IsObject { get; }
    }
}