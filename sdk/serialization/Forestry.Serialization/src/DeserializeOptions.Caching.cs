using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Converters.Primitive;
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
        /// Telemetry type lookup, fixed once locked
        /// </summary>
        private FrozenDictionary<Type, TypeDefinition>? _typeDefinitions;

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
        /// Converters resolved per type while definitions are built
        /// </summary>
        private Dictionary<Type, Converter?>? _resolvedConverters;

        /// <summary>
        /// Primitive converters, last in the precedence
        /// </summary>
        private static readonly Converter[] s_primitiveConverters =
        [
            new StringConverter(),
            new BooleanConverter(),
            new DateTimeConverter(),
            new DateTimeOffsetConverter(),
            new NullableConverterFactory(),
            new Utf8SpanParsableConverterFactory()
        ];

        /// <summary>
        /// Type definition of an added telemetry type once the options are read-only
        /// </summary>
        /// <param name="type"></param>
        /// <param name="typeDefinition"></param>
        /// <returns></returns>
        internal bool TryGetTypeDefinition(Type type, [NotNullWhen(true)] out TypeDefinition? typeDefinition)
        {
            if (_typeDefinitions is { } typeDefinitions)
            {
                return typeDefinitions.TryGetValue(type, out typeDefinition);
            }

            typeDefinition = null;
            return false;
        }

        /// <summary>
        /// Type definition of an added telemetry type; any other type throws (S0)
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal TypeDefinition ThrowingGetTypeDefinition(Type type)
        {
            if (!TryGetTypeDefinition(type, out TypeDefinition? typeDefinition))
            {
                Throwing.WhenTelemetryTypeNotAdded(type);
            }

            return typeDefinition;
        }

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
        /// Resolved converter of a type: the developer's converters, then the format's defaults, then
        /// the primitives; null when none handles it (research R14)
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal Converter? ResolveConverter(Type type)
        {
            _resolvedConverters ??= [];

            if (_resolvedConverters.TryGetValue(type, out Converter? resolved))
            {
                return resolved;
            }

            resolved = FindConverter(_converters, type) ?? FindConverter(FormatConverters, type) ?? FindConverter(s_primitiveConverters, type);
            _resolvedConverters[type] = resolved;

            return resolved;
        }

        private Converter? FindConverter(IEnumerable<Converter>? converters, Type type)
        {
            if (converters is null)
            {
                return null;
            }

            foreach (Converter converter in converters)
            {
                if (converter.CanConvert(type))
                {
                    return converter is ConverterFactory factory ? factory.CreateConverter(type, this) : converter;
                }
            }

            return null;
        }

        /// <summary>
        /// Build the type definitions of the added telemetry types, and the set of enumerated ones, under the
        /// options' lock
        /// </summary>
        /// <returns></returns>
        private FrozenDictionary<Type, TypeDefinition> BuildTypeDefinitions(PolicyResolver resolver)
        {
            if (Telemetries.Count == 0)
            {
                Throwing.WhenNoTelemetryTypes();
            }

            // A failed build leaves the options unlocked: forget what it resolved (a converter may have been added since)
            _resolvedConverters = null;

            Dictionary<Type, TypeDefinition> typeDefinitions = [];

            foreach (Type type in Telemetries)
            {
                typeDefinitions[type] = TypeDefinition.Create(type, this, resolver.IncludeMember(type));
            }

            _enumeratedTypes = Telemetries.ToFrozenSet();
            _resolvedConverters = null;
            return typeDefinitions.ToFrozenDictionary();
        }

        /// <summary>
        /// Resolve and compose the policies once, after the definitions are built (research R19)
        /// </summary>
        /// <param name="resolver"></param>
        /// <param name="typeDefinitions"></param>
        private void ResolvePolicies(PolicyResolver resolver, FrozenDictionary<Type, TypeDefinition> typeDefinitions)
        {
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
        }
    }
}
