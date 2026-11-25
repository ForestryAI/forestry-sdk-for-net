namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A telemetry class used as a constructor parameter of <see cref="LogWithParameters"/>: a parameter
    /// type that is itself a telemetry, as later slices require (research R5)
    /// </summary>
    public sealed class Stem : Telemetry
    {
        public int StemKey { get; set; }
    }
}
