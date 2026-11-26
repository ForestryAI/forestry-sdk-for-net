using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.Policies;

namespace Forestry.Serialization
{
    /// <summary>
    /// Definitions built once when the options lock, kept per options and never shared between
    /// options instances (research R5, S0)
    /// </summary>
    public abstract partial class DeserializeOptions
    {
        /// <summary>
        /// Telemetry types whose telemetries are enumerated: the explicitly added ones, fixed once locked (research R12)
        /// </summary>
        private FrozenSet<Type>? _enumeratedTypes;

        /// <summary>
        /// Each telemetry type's definition with its enumeration policies composed, fixed once locked (research R19)
        /// </summary>
        private FrozenDictionary<Type, TypeChains>? _typeChains;

        /// <summary>
        /// The enumeration policies acting on each member's set, fixed once locked
        /// </summary>
        private Dictionary<PropertyDefinition, IReadOnlyList<EnumerationPolicy>>? _memberApplying;

        /// <summary>
        /// Every enumeration policy of the options, each once: cleared when an enumeration ends
        /// </summary>
        private EnumerationPolicy[] _enumerationPolicies = [];

        /// <summary>
        /// 1 while an enumeration runs on options with enumeration policies
        /// </summary>
        private int _enumerating;

        /// <summary>
        /// Telemetries of this type are enumerated: it was added explicitly, not only needed by a constructor.
        /// Decided by the options, not the type definition (research R12)
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal bool Enumerates(Type type) => _enumeratedTypes?.Contains(type) ?? false;

        /// <summary>
        /// A telemetry type's definition with its composed enumeration policies, once locked
        /// </summary>
        /// <param name="type"></param>
        /// <param name="chains"></param>
        /// <returns></returns>
        internal bool TryGetTypeChains(Type type, [NotNullWhen(true)] out TypeChains? chains)
        {
            if (_typeChains is { } typeChains)
            {
                return typeChains.TryGetValue(type, out chains);
            }

            chains = null;
            return false;
        }

        /// <summary>
        /// A telemetry type's definition with its composed enumeration policies; any other type throws (S0)
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal TypeChains ThrowingGetTypeChains(Type type)
        {
            if (!TryGetTypeChains(type, out TypeChains? chains))
            {
                Throwing.WhenTelemetryTypeNotAdded(type);
            }

            return chains;
        }

        /// <summary>
        /// The enumeration policies acting on a member's set, once locked
        /// </summary>
        /// <param name="property"></param>
        /// <param name="applying"></param>
        /// <returns></returns>
        internal bool TryGetMemberApplying(PropertyDefinition property, [NotNullWhen(true)] out IReadOnlyList<EnumerationPolicy>? applying)
        {
            if (_memberApplying is { } memberApplying)
            {
                return memberApplying.TryGetValue(property, out applying);
            }

            applying = null;
            return false;
        }

        /// <summary>
        /// An enumeration starts: with enumeration policies, one at a time per options (research R19)
        /// </summary>
        internal void BeginEnumeration()
        {
            if (_enumerationPolicies.Length > 0 && Interlocked.Exchange(ref _enumerating, 1) == 1)
            {
                Throwing.WhenEnumerationRunning();
            }
        }

        /// <summary>
        /// An enumeration ended: clear every enumeration policy once, with the context still holding its
        /// telemetries, then let the next one start
        /// </summary>
        /// <param name="context">The ending enumeration's deserialization context</param>
        internal void EndEnumeration(Reading.DeserializeContext context)
        {
            if (_enumerationPolicies.Length == 0)
            {
                return;
            }

            try
            {
                foreach (EnumerationPolicy policy in _enumerationPolicies)
                {
                    policy.Clear(context);
                }
            }
            finally
            {
                Volatile.Write(ref _enumerating, 0);
            }
        }

        /// <summary>
        /// The policies resolved while the options lock
        /// </summary>
        private PolicyResolver? _resolver;

        private protected override void OnBuildingDefinitions() => _resolver = new PolicyResolver(_policies);

        /// <summary>
        /// Members chosen by the definition policies (research R7)
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private protected override Func<MemberInfo, bool> IncludeMember(Type type) => _resolver!.IncludeMember(type);

        /// <summary>
        /// Resolve and compose the policies once, after the definitions are built, and fix the enumerated
        /// types (research R12, R19)
        /// </summary>
        /// <param name="typeDefinitions"></param>
        private protected override void OnDefinitionsBuilt(FrozenDictionary<Type, TypeDefinition> typeDefinitions)
        {
            PolicyResolver resolver = _resolver!;
            _enumeratedTypes = Telemetries.ToFrozenSet();
            resolver.ValidateTargets(typeDefinitions);

            Dictionary<PropertyDefinition, IReadOnlyList<EnumerationPolicy>> memberApplying = new(ReferenceEqualityComparer.Instance);
            Dictionary<Type, TypeChains> typeChains = [];

            foreach ((Type type, TypeDefinition definition) in typeDefinitions)
            {
                typeChains[type] = resolver.Resolve(definition, memberApplying);
            }

            _typeChains = typeChains.ToFrozenDictionary();
            _memberApplying = memberApplying;
            _enumerationPolicies = resolver.EnumerationPolicies;
            _resolver = null;
        }
    }
}
