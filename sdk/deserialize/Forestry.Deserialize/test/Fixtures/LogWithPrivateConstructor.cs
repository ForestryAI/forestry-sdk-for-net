namespace Forestry.Deserialize.Tests.Fixtures
{
    /// <summary>
    /// A row class whose only constructor is private and not marked: no constructor can be chosen
    /// (FR-008, research R5)
    /// </summary>
    /// <remarks>
    /// The constructor is parameterless, so the rejection must be for "no constructor can be chosen",
    /// not for a parameter (that is T023's reason).
    /// </remarks>
    public sealed class LogWithPrivateConstructor : Row
    {
        private LogWithPrivateConstructor()
        {
        }

        public int LogKey { get; set; }
    }
}
