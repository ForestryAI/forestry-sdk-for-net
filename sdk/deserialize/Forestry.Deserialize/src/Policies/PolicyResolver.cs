using System.Collections.Frozen;
using System.Reflection;
using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Policies
{
    /// <summary>
    /// Resolves the policies of an options instance once, when it locks (research R7, R19): per method
    /// and target the most specific level that has policies for it, chains in the order added, composed
    /// into one step each
    /// </summary>
    /// <remarks>
    /// Levels, most specific first: option for the member, option for the type, attribute on the member,
    /// attribute on the type, options-wide, built-in. A policy takes part only in the methods it
    /// overrides. Attribute policies are created once here and owned by the options.
    /// </remarks>
    internal sealed class PolicyResolver(PolicyCollection? policies)
    {
        private const string Construct = nameof(EnumerationPolicy.Construct);

        private const string Set = nameof(EnumerationPolicy.Set);

        private const string Complete = nameof(EnumerationPolicy.Complete);

        private readonly Dictionary<MemberInfo, Policy[]> _attributePolicies = [];

        private readonly HashSet<EnumerationPolicy> _enumerationPolicies = new(ReferenceEqualityComparer.Instance);

        private IEnumerable<Policy> OptionsWide => policies?.OptionsWide ?? [];

        private IEnumerable<Policy> ForType(Type type) =>
            policies?.ForTypes.Where(entry => entry.Type == type).Select(entry => entry.Policy) ?? [];

        private IEnumerable<EnumerationPolicy> ForMember(Type type, string member) =>
            policies?.ForMembers.Where(entry => entry.Type == type && entry.Member == member).Select(entry => entry.Policy) ?? [];

        /// <summary>
        /// Every enumeration policy that applies to the options' row types, each once: cleared when an
        /// enumeration ends
        /// </summary>
        public EnumerationPolicy[] EnumerationPolicies => [.. _enumerationPolicies];

        /// <summary>
        /// Whether a candidate property or field of <paramref name="type"/> is a member: the definition
        /// policies' chain, or the built-in step including it
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public Func<MemberInfo, bool> IncludeMember(Type type)
        {
            DefinitionPolicy[] chain = Pick<DefinitionPolicy>(
                type,
                nameof(DefinitionPolicy.IncludeMember),
                DefinitionPolicy.OverridesIncludeMember,
                ForType(type).OfType<DefinitionPolicy>(),
                AttributePolicies(type).OfType<DefinitionPolicy>(),
                OptionsWide.OfType<DefinitionPolicy>());

            Func<MemberInfo, bool> step = static _ => true;

            for (int i = chain.Length - 1; i >= 0; i--)
            {
                DefinitionPolicy policy = chain[i];
                Func<MemberInfo, bool> next = step;
                step = member => policy.IncludeMember(type, member, next);
            }

            return step;
        }

        /// <summary>
        /// Compose a type definition's enumeration policies, and record which act on each member
        /// </summary>
        /// <param name="definition"></param>
        /// <param name="memberApplying">Filled with each member's applying policies</param>
        /// <returns></returns>
        public TypeChains Resolve(TypeDefinition definition, Dictionary<PropertyDefinition, IReadOnlyList<EnumerationPolicy>> memberApplying)
        {
            Type type = definition.Type;
            EnumerationPolicy[] forType = [.. ForType(type).OfType<EnumerationPolicy>()];
            EnumerationPolicy[] onType = [.. AttributePolicies(type).OfType<EnumerationPolicy>()];
            EnumerationPolicy[] optionsWide = [.. OptionsWide.OfType<EnumerationPolicy>()];

            EnumerationPolicy[] constructChain = PickEnumeration(type, Construct, [], forType, [], onType, optionsWide);
            EnumerationPolicy[] completeChain = PickEnumeration(type, Complete, [], forType, [], onType, optionsWide);

            Func<DeserializationContext, Row> construct = _ => definition.Construct();

            for (int i = constructChain.Length - 1; i >= 0; i--)
            {
                EnumerationPolicy policy = constructChain[i];
                Func<DeserializationContext, Row> next = construct;
                construct = context => policy.Construct(definition, context, next);
            }

            Action<ContextRow> complete = static _ => { };

            for (int i = completeChain.Length - 1; i >= 0; i--)
            {
                EnumerationPolicy policy = completeChain[i];
                Action<ContextRow> next = complete;
                complete = row => policy.Complete(row, row.Context, next);
            }

            Delegate[] setters = new Delegate[definition.Properties.Count];

            foreach (PropertyDefinition property in definition.Properties)
            {
                EnumerationPolicy[] forMember = [.. ForMember(type, property.Name)];
                EnumerationPolicy[] onMember = MemberAttributePolicies(type, property.Member);

                foreach (EnumerationPolicy policy in forMember.Concat(onMember))
                {
                    if (EnumerationPolicy.Overrides(policy.GetType(), Construct) || EnumerationPolicy.Overrides(policy.GetType(), Complete))
                    {
                        Throwing.WhenMemberPolicyNotSetOnly(policy.GetType(), type, property.Member);
                    }
                }

                EnumerationPolicy[] setChain = PickEnumeration(type, Set, forMember, forType, onMember, onType, optionsWide, property.Member);
                setters[property.Index] = property.ComposeSet(setChain);
                memberApplying[property] = setChain;
                _enumerationPolicies.UnionWith(forMember);
                _enumerationPolicies.UnionWith(onMember);
            }

            _enumerationPolicies.UnionWith(forType);
            _enumerationPolicies.UnionWith(onType);
            _enumerationPolicies.UnionWith(optionsWide);

            EnumerationPolicy[] applying = [.. constructChain.Concat(completeChain).Distinct(ReferenceEqualityComparer.Instance).Cast<EnumerationPolicy>()];

            return new TypeChains(definition, construct, complete, setters, applying);
        }

        /// <summary>
        /// Policies registered for a type or member that the options do not have throw (FR-008)
        /// </summary>
        /// <param name="definitions"></param>
        public void ValidateTargets(FrozenDictionary<Type, TypeDefinition> definitions)
        {
            if (policies is null)
            {
                return;
            }

            foreach ((Type type, Policy _) in policies.ForTypes)
            {
                if (!definitions.ContainsKey(type))
                {
                    Throwing.WhenPolicyTypeNotAdded(type);
                }
            }

            foreach ((Type type, string member, EnumerationPolicy _) in policies.ForMembers)
            {
                if (!definitions.TryGetValue(type, out TypeDefinition? definition))
                {
                    Throwing.WhenPolicyTypeNotAdded(type);
                }

                if (!definition.Properties.Any(property => property.Name == member))
                {
                    Throwing.WhenPolicyMemberNotFound(type, member);
                }
            }
        }

        private EnumerationPolicy[] PickEnumeration(
            Type type,
            string method,
            EnumerationPolicy[] forMember,
            EnumerationPolicy[] forType,
            EnumerationPolicy[] onMember,
            EnumerationPolicy[] onType,
            EnumerationPolicy[] optionsWide,
            MemberInfo? member = null) =>
            Pick(member ?? type, method, policyType => EnumerationPolicy.Overrides(policyType, method), forMember, forType, onMember, onType, optionsWide, attributeLevels: [2, 3]);

        /// <summary>
        /// A type-level decision: option for the type, attribute on the type, options-wide
        /// </summary>
        private static T[] Pick<T>(MemberInfo target, string method, Func<Type, bool> overrides, IEnumerable<T> forType, IEnumerable<T> onType, IEnumerable<T> optionsWide) where T : Policy =>
            Pick(target, method, overrides, [], forType, [], onType, optionsWide, attributeLevels: [2, 3]);

        /// <summary>
        /// The most specific level with policies overriding <paramref name="method"/>, in the order added;
        /// more than one attribute on a target overriding the same method throws (attributes have no order)
        /// </summary>
        private static T[] Pick<T>(
            MemberInfo target,
            string method,
            Func<Type, bool> overrides,
            IEnumerable<T> level1,
            IEnumerable<T> level2,
            IEnumerable<T> level3,
            IEnumerable<T> level4,
            IEnumerable<T> level5,
            int[] attributeLevels) where T : Policy
        {
            IEnumerable<T>[] levels = [level1, level2, level3, level4, level5];

            for (int level = 0; level < levels.Length; level++)
            {
                T[] chain = [.. levels[level].Where(policy => overrides(policy.GetType()))];

                if (chain.Length == 0)
                {
                    continue;
                }

                if (chain.Length > 1 && attributeLevels.Contains(level))
                {
                    Throwing.WhenPolicyAttributesAmbiguous(target, method);
                }

                return chain;
            }

            return [];
        }

        private Policy[] AttributePolicies(MemberInfo target)
        {
            if (!_attributePolicies.TryGetValue(target, out Policy[]? created))
            {
                created = [.. target.GetCustomAttributes<PolicyAttribute>(inherit: false).Select(attribute => attribute.CreatePolicy())];
                _attributePolicies[target] = created;
            }

            return created;
        }

        private EnumerationPolicy[] MemberAttributePolicies(Type type, MemberInfo member)
        {
            Policy[] onMember = AttributePolicies(member);

            foreach (Policy policy in onMember)
            {
                if (policy is not EnumerationPolicy)
                {
                    Throwing.WhenDefinitionPolicyOnMember(policy.GetType(), type, member);
                }
            }

            return [.. onMember.Cast<EnumerationPolicy>()];
        }
    }
}
