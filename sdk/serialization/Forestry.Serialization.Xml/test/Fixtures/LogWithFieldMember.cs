namespace Forestry.Serialization.Xml.Tests.Fixtures
{
    /// <summary>
    /// A telemetry class with public fields instead of properties (flow 2, compiled-expression setter)
    /// </summary>
    public sealed class LogWithFieldMember : Telemetry
    {
        public int LogKey;

        public decimal LogVolume;
    }
}
