namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A telemetry class whose only constructor is private and not marked: no constructor can be chosen
    /// (FR-008, research R5)
    /// </summary>
    /// <remarks>
    /// The constructor is parameterless, so the rejection must be for "no constructor can be chosen",
    /// not for a parameter (that is T023's reason).
    /// </remarks>
    public sealed class LogWithPrivateConstructor : Telemetry
    {
        private LogWithPrivateConstructor()
        {
        }

        public int LogKey { get; set; }
    }
}
