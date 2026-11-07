using Forestry.Deserialize.Policies;

namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// What a deserialization holds across read cycles: the stack of open rows (research R2).
    /// Retained rows come with later slices
    /// </summary>
    internal sealed class DeserializationState
    {
        private readonly Stack<OpenRow> _openRows = new();

        /// <summary>
        /// Number of open rows
        /// </summary>
        public int OpenRowCount => _openRows.Count;

        /// <summary>
        /// Push a constructed row
        /// </summary>
        /// <param name="chains"></param>
        /// <param name="row"></param>
        public void Push(TypeChains chains, Row row) => _openRows.Push(new OpenRow(chains, row));

        /// <summary>
        /// Top open row
        /// </summary>
        /// <param name="openRow"></param>
        /// <returns></returns>
        public bool TryPeek(out OpenRow openRow) => _openRows.TryPeek(out openRow);

        /// <summary>
        /// Pop the top open row
        /// </summary>
        /// <param name="openRow"></param>
        /// <returns></returns>
        public bool TryPop(out OpenRow openRow) => _openRows.TryPop(out openRow);

        /// <summary>
        /// Drop everything: rows still open are never handed out
        /// </summary>
        public void Clear() => _openRows.Clear();

        /// <summary>
        /// An open row and its definition with the composed policies
        /// </summary>
        /// <param name="Chains"></param>
        /// <param name="Row"></param>
        internal readonly record struct OpenRow(TypeChains Chains, Row Row);
    }
}
