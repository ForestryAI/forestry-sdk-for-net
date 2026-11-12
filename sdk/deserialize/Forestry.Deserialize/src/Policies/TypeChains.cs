using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Policies
{
    /// <summary>
    /// A row type's definition with its enumeration policies composed into one step per method, once when
    /// the options lock (research R19): nothing is created per enumeration or allocated per row
    /// </summary>
    internal sealed class TypeChains(
        TypeDefinition definition,
        Func<DeserializationContext, Row> construct,
        Action<ContextRow> complete,
        Delegate[] setters,
        IReadOnlyList<EnumerationPolicy> applying)
    {
        public TypeDefinition Definition { get; } = definition;

        /// <summary>
        /// Construct a row: the composed chain, or the built-in constructor
        /// </summary>
        public Func<DeserializationContext, Row> Construct { get; } = construct;

        /// <summary>
        /// The completion: the composed chain, or the built-in step that does nothing
        /// </summary>
        public Action<ContextRow> Complete { get; } = complete;

        /// <summary>
        /// One <c>Action&lt;ContextRow, T&gt;</c> per property definition, by its index: the composed
        /// chain, or the built-in setter
        /// </summary>
        public Delegate[] Setters { get; } = setters;

        /// <summary>
        /// Enumeration policies acting on construction or completion, in chain order
        /// </summary>
        public IReadOnlyList<EnumerationPolicy> Applying { get; } = applying;
    }
}
