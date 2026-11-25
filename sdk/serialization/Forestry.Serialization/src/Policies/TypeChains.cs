using Forestry.Serialization.Definitions;
using Forestry.Serialization.Reading;

namespace Forestry.Serialization.Policies
{
    /// <summary>
    /// A telemetry type's definition with its enumeration policies composed into one step per method, once when
    /// the options lock (research R19): nothing is created per enumeration or allocated per telemetry
    /// </summary>
    internal sealed class TypeChains(
        TypeDefinition definition,
        Func<DeserializeContext, Telemetry> construct,
        Action<ContextTelemetry> complete,
        Delegate[] setters,
        IReadOnlyList<EnumerationPolicy> applying)
    {
        public TypeDefinition Definition { get; } = definition;

        /// <summary>
        /// Construct a telemetry: the composed chain, or the built-in constructor
        /// </summary>
        public Func<DeserializeContext, Telemetry> Construct { get; } = construct;

        /// <summary>
        /// The completion: the composed chain, or the built-in step that does nothing
        /// </summary>
        public Action<ContextTelemetry> Complete { get; } = complete;

        /// <summary>
        /// One <c>Action&lt;ContextTelemetry, T&gt;</c> per property definition, by its index: the composed
        /// chain, or the built-in setter
        /// </summary>
        public Delegate[] Setters { get; } = setters;

        /// <summary>
        /// Enumeration policies acting on construction or completion, in chain order
        /// </summary>
        public IReadOnlyList<EnumerationPolicy> Applying { get; } = applying;
    }
}
