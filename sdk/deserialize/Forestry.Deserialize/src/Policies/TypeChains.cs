using Forestry.Deserialize.Definitions;

namespace Forestry.Deserialize.Policies
{
    /// <summary>
    /// A row type's definition with its enumeration policies composed into one step per method, once when
    /// the options lock (research R19): nothing is created per enumeration or allocated per row
    /// </summary>
    internal sealed class TypeChains(
        TypeDefinition definition,
        Func<Row> construct,
        Func<Row, Row?> enumerate,
        Delegate[] setters,
        IReadOnlyList<EnumerationPolicy> applying)
    {
        public TypeDefinition Definition { get; } = definition;

        /// <summary>
        /// Construct a row: the composed chain, or the built-in constructor
        /// </summary>
        public Func<Row> Construct { get; } = construct;

        /// <summary>
        /// The row to enumerate, or null: the composed chain, or the built-in decision
        /// </summary>
        public Func<Row, Row?> Enumerate { get; } = enumerate;

        /// <summary>
        /// One <c>Action&lt;Row, T&gt;</c> per property definition, by its index: the composed chain, or
        /// the built-in setter
        /// </summary>
        public Delegate[] Setters { get; } = setters;

        /// <summary>
        /// Enumeration policies acting on construction or enumeration, in chain order
        /// </summary>
        public IReadOnlyList<EnumerationPolicy> Applying { get; } = applying;
    }
}
