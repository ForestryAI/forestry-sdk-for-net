namespace Forestry.Serialization.Xml.Tests.Fixtures
{
    /// <summary>
    /// A <c>Log</c> telemetry read from <c>&lt;Log&gt;</c> with the recording policy attached by attribute
    /// (tasks.md T049, T051, research R19)
    /// </summary>
    [XmlElementName("Log")]
    [Policy<RecordingPolicy>]
    public sealed class RecordedLog : Telemetry
    {
        public int LogKey { get; set; }

        public decimal LogVolume { get; set; }
    }
}
