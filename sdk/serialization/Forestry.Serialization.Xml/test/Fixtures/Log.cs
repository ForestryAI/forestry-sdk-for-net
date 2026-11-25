namespace Forestry.Serialization.Xml.Tests.Fixtures
{
    /// <summary>
    /// The walking skeleton's flat telemetry class (spec 001, US1): one telemetry per <c>Log</c> occurrence
    /// </summary>
    public sealed class Log : Telemetry
    {
        public int LogKey { get; set; }

        public decimal LogVolume { get; set; }
    }
}
