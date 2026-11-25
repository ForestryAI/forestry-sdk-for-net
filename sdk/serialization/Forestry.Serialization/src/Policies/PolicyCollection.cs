using Forestry.Serialization.Definitions;

namespace Forestry.Serialization.Policies
{
    /// <summary>
    /// The policies added to an options instance, locked with the options (research R19, FR-013), and,
    /// once locked, which enumeration policies apply to each definition
    /// </summary>
    /// <remarks>
    /// <para>
    /// Instances: a policy lives as long as the options. Several added for the same target chain in the
    /// order added, the first outermost, per method they override.
    /// </para>
    /// <para>
    /// Precedence per method, most specific first: option for the member, option for the type,
    /// attribute on the member, attribute on the type, options-wide, built-in (research R7). Only the
    /// most specific level that has a policy for a method acts; levels never chain.
    /// </para>
    /// </remarks>
    public sealed class PolicyCollection
    {
        internal PolicyCollection(DeserializeOptions options)
        {
            _options = options;
        }

        private readonly DeserializeOptions _options;

        internal List<Policy> OptionsWide { get; } = [];

        internal List<(Type Type, Policy Policy)> ForTypes { get; } = [];

        internal List<(Type Type, string Member, EnumerationPolicy Policy)> ForMembers { get; } = [];

        /// <summary>
        /// Add a policy for every telemetry type of the options
        /// </summary>
        /// <param name="policy"></param>
        /// <returns>This collection, for chaining</returns>
        public PolicyCollection Add(Policy policy)
        {
            ArgumentNullException.ThrowIfNull(policy);
            _options.ThrowingAssertNotReadOnly();

            OptionsWide.Add(policy);
            return this;
        }

        /// <summary>
        /// Add a policy for the telemetry type <typeparamref name="TTelemetry"/>; an enumeration policy overriding
        /// <c>Set</c> acts on every member of the type
        /// </summary>
        /// <typeparam name="TTelemetry"></typeparam>
        /// <param name="policy"></param>
        /// <returns>This collection, for chaining</returns>
        public PolicyCollection Add<TTelemetry>(Policy policy) where TTelemetry : Telemetry
        {
            ArgumentNullException.ThrowIfNull(policy);
            _options.ThrowingAssertNotReadOnly();

            ForTypes.Add((typeof(TTelemetry), policy));
            return this;
        }

        /// <summary>
        /// Add an enumeration policy for one member of <typeparamref name="TTelemetry"/>, by its C# name; it may
        /// override only <c>Set</c>
        /// </summary>
        /// <typeparam name="TTelemetry"></typeparam>
        /// <param name="memberName">The property or field name in C#, e.g. <c>nameof(Log.LogKey)</c></param>
        /// <param name="policy"></param>
        /// <returns>This collection, for chaining</returns>
        public PolicyCollection Add<TTelemetry>(string memberName, EnumerationPolicy policy) where TTelemetry : Telemetry
        {
            ArgumentException.ThrowIfNullOrEmpty(memberName);
            ArgumentNullException.ThrowIfNull(policy);
            _options.ThrowingAssertNotReadOnly();

            ForMembers.Add((typeof(TTelemetry), memberName, policy));
            return this;
        }

        /// <summary>
        /// The enumeration policies acting on a telemetry type's construction or enumeration, in chain order;
        /// empty when only the built-in steps apply or the options are not locked yet
        /// </summary>
        /// <param name="definition"></param>
        /// <returns></returns>
        public IReadOnlyList<EnumerationPolicy> Applying(TypeDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            return _options.TryGetTypeChains(definition.Type, out TypeChains? chains) ? chains.Applying : [];
        }

        /// <summary>
        /// The enumeration policies acting on a member's set, in chain order; empty when only the built-in
        /// step applies or the options are not locked yet
        /// </summary>
        /// <param name="definition"></param>
        /// <returns></returns>
        public IReadOnlyList<EnumerationPolicy> Applying(PropertyDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            return _options.TryGetMemberApplying(definition, out IReadOnlyList<EnumerationPolicy>? applying) ? applying : [];
        }
    }
}
