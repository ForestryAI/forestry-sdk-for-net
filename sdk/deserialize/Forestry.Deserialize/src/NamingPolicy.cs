namespace Forestry.Deserialize
{
    /// <summary>
    /// Naming policy: checks and maps a member's name, from the design before spec 001
    /// </summary>
    /// <remarks>
    /// Not used yet: it is folded into the policy model with user story 4 (tasks.md T060, research R7, R10).
    /// </remarks>
    public abstract class NamingPolicy
    {
        /// <summary>
        /// Whether the name is acceptable
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public abstract bool TryEnforce(string name);

        /// <summary>
        /// The name to use for <paramref name="name"/>
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public abstract string Process(string name);

        /// <summary>
        /// Accepts any non-empty name and keeps it as it is
        /// </summary>
        public static NamingPolicy Default { get; } = new DefaultNamingPolicy();

        private sealed class DefaultNamingPolicy : NamingPolicy
        {
            public override bool TryEnforce(string name)
            {
                if (name is null || name == string.Empty)
                {
                    return false;
                }

                return true;
            }

            public override string Process(string name)
            {
                return name; 
            }
        }
    }
}