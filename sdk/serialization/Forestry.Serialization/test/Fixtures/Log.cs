namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// The walking skeleton's flat telemetry class (spec 001, US1), for core tests
    /// </summary>
    public sealed class Log : Telemetry
    {
        public int LogKey { get; set; }

        public decimal LogVolume { get; set; }
    }
}
