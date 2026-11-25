namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A telemetry class with two public constructors and none marked: no constructor can be chosen (FR-008)
    /// </summary>
    public sealed class LogWithTwoPublicConstructors : Telemetry
    {
        public LogWithTwoPublicConstructors()
        {
        }

        public LogWithTwoPublicConstructors(int logKey)
        {
            LogKey = logKey;
        }

        public int LogKey { get; set; }
    }
}
