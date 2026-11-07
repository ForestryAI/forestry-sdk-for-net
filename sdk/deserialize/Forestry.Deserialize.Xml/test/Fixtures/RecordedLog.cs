namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A <c>Log</c> row read from <c>&lt;Log&gt;</c> with the recording policy attached by attribute
    /// (tasks.md T049, T051, research R19)
    /// </summary>
    [Row("Log")]
    [Policy<RecordingPolicy>]
    public sealed class RecordedLog : Row
    {
        public int LogKey { get; set; }

        public decimal LogVolume { get; set; }
    }
}
