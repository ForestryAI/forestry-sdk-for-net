namespace Forestry.Deserialize
{
    /// <summary>
    /// Reader implementations walk forward through a specific media (e.g. XML) one
    /// <see cref="Value"/> at a time, without loading the whole source into memory.
    /// </summary>
    public interface IReader
    {
        /// <summary>
        /// Value produced by the most recent successful <see cref="Read"/>
        /// </summary>
        Value Current { get; }

        /// <summary>
        /// Advance to the next value, making it available as <see cref="Current"/>.
        /// Returns false once the media is exhausted.
        /// </summary>
        /// <remarks>
        /// When a value is malformed a reader may throw instead of returning. To support the
        /// shunt-aside <see cref="ReadErrorHandling"/> policy, a reader that throws must leave
        /// itself positioned so that the next call to <see cref="Read"/> continues with whatever
        /// comes after the offending value, rather than reproducing the same failure forever.
        /// </remarks>
        bool Read();
    }
}
