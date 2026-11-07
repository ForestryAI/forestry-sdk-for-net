using System.Reflection;

namespace Forestry.Deserialize
{
    /// <summary>
    /// Policy deciding whether a field is a member, from the design before spec 001
    /// </summary>
    /// <remarks>
    /// Not used yet: it is folded into the policy model with user story 4 (tasks.md T060, research R7, R10).
    /// </remarks>
    public interface IIncludeFieldPolicy
    {
        /// <summary>
        /// Whether <paramref name="field"/> is included
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public bool TryEnforce(FieldInfo field);
    }
}