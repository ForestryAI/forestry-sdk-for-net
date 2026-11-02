namespace Forestry.Deserialize.Tests.Fixtures
{
    /// <summary>
    /// A row class with two public constructors and none marked: no constructor can be chosen (FR-008)
    /// </summary>
    public sealed class LogWithTwoPublicConstructors : Row
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
