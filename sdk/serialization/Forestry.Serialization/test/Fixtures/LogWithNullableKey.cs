namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A telemetry class with a nullable member, whose converter wraps the one resolved for its underlying
    /// type (flow 3, R14)
    /// </summary>
    public sealed class LogWithNullableKey : Telemetry
    {
        public int? LogKey { get; set; }
    }
}
