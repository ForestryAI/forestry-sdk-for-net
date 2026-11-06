using System.Reflection;

namespace Forestry.Deserialize
{
    /// <summary>
    /// Policy deciding whether a property is left out, from the design before spec 001
    /// </summary>
    /// <remarks>
    /// Not used yet: it is folded into the policy model with user story 4 (tasks.md T060, research R7, R10).
    /// </remarks>
    public interface IIgnorePropertyPolicy
    {
        /// <summary>
        /// Whether <paramref name="property"/> is ignored
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        public bool TryEnforce(PropertyInfo property);
    }
}