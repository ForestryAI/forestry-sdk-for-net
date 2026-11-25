using System.Reflection;

namespace Forestry.Serialization.Policies
{
    /// <summary>
    /// A policy shaping the definitions once, when the options lock (research R19): which properties
    /// and fields of a telemetry type are members. Its result is the definition; it plays no part in reading
    /// </summary>
    /// <remarks>
    /// Attached to a telemetry type (by attribute on the class or through <c>options.Policies</c>) or
    /// options-wide; never to a member. Names in the document are not decided here: they belong to the
    /// format package (constitution A5; spec 003 research R37).
    /// </remarks>
    public abstract class DefinitionPolicy : Policy
    {
        /// <summary>
        /// Definition policy
        /// </summary>
        protected DefinitionPolicy()
        {
        }

        /// <summary>
        /// Whether a public property or field of <paramref name="type"/> is a member, asked once per
        /// candidate, properties and fields alike. The built-in step includes it; a member whose type no
        /// converter handles still fails when the definition is built
        /// </summary>
        /// <param name="type">The telemetry type being defined</param>
        /// <param name="member">The candidate property or field</param>
        /// <param name="next">The next step: the next policy, or the built-in decision</param>
        /// <returns>Whether it is a member</returns>
        public virtual bool IncludeMember(Type type, MemberInfo member, Func<MemberInfo, bool> next) => next(member);

        /// <summary>
        /// Whether a policy type overrides <see cref="IncludeMember"/>
        /// </summary>
        /// <param name="policyType"></param>
        /// <returns></returns>
        internal static bool OverridesIncludeMember(Type policyType) =>
            policyType.GetMethod(nameof(IncludeMember), BindingFlags.Instance | BindingFlags.Public)!.DeclaringType != typeof(DefinitionPolicy);
    }
}
