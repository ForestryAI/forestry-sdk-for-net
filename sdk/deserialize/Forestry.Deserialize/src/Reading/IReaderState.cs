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
        /// Reader line number's position (byte)
        /// </summary>
        internal long _lineNumberPosition { get; }

        /// <summary>
        /// Is in an object
        /// </summary>
        internal bool _isObject { get; }

        /// <summary>
        /// Is in a none privitive
        /// </summary>
        internal bool _isNotPrimitive { get; }
    }
}