namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A class not extending <see cref="Telemetry"/>: adding it with <c>Telemetries.Add(params Type[])</c> throws (FR-008, R12)
    /// </summary>
    public sealed class NotATelemetry
    {
        public int LogKey { get; set; }
    }
}
