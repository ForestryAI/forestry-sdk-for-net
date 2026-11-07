using Forestry.Deserialize.Policies;

namespace Forestry.Deserialize
{
    /// <summary>
    /// Attaches a policy to a row type or a member (research R7, R19). On a class: an enumeration or a
    /// definition policy for the type; on a property or field: an enumeration policy for that member,
    /// overriding only <c>Set</c>. The options create the policy once, when they lock
    /// </summary>
    /// <remarks>
    /// Options win over attributes for the same method and target. Attributes have no defined order, so
    /// at most one attribute per target may override the same method; chains come through the options.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public abstract class PolicyAttribute : Attribute
    {
        private protected PolicyAttribute()
        {
        }

        /// <summary>
        /// Create the policy, once per options
        /// </summary>
        /// <returns></returns>
        internal abstract Policy CreatePolicy();
    }

    /// <summary>
    /// Attaches a policy of type <typeparamref name="TPolicy"/>, e.g. <c>[Policy&lt;MyPolicy&gt;]</c>
    /// </summary>
    /// <typeparam name="TPolicy">A policy with a parameterless constructor</typeparam>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class PolicyAttribute<TPolicy> : PolicyAttribute where TPolicy : Policy, new()
    {
        internal override Policy CreatePolicy() => new TPolicy();
    }
}
