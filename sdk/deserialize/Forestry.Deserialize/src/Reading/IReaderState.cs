namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// Reader state (reader position) to recreate a reader.
    /// </summary>
    public interface IReaderState<TState> where TState : struct, IReaderState<TState>
    {
        /// <summary>
        /// Reader line number
        /// </summary>
        internal long _lineNumber { get; }

        /// <summary>
        /// Reader line position (byte)
        /// </summary>
        internal long _linePosition { get; }

    }
}