namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// How long a row stays in the deserialization context after its completion (spec 002 FR-003,
    /// research R23). A policy sets it at most once, during the row's own completion; a row no policy
    /// keeps leaves (<see cref="Leave"/>)
    /// </summary>
    public enum Lifetime : byte
    {
        /// <summary>
        /// Leaves the context at its completion, before it is handed out if it is enumerated (the
        /// default)
        /// </summary>
        Leave = 0,

        /// <summary>
        /// Stays until a policy releases it, at the latest until the enumeration ends
        /// </summary>
        UntilReleased = 1,

        /// <summary>
        /// Stays until the enumeration ends; releasing it throws
        /// </summary>
        UntilEnd = 2
    }
}
