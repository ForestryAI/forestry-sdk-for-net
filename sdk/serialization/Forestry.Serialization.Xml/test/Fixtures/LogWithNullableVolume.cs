namespace Forestry.Serialization.Xml.Tests.Fixtures
{
    /// <summary>
    /// A telemetry class whose <c>LogVolume</c> allows null (spec edge case: empty element)
    /// </summary>
    public sealed class LogWithNullableVolume : Telemetry
    {
        public int LogKey { get; set; }

        public decimal? LogVolume { get; set; }
    }
}
