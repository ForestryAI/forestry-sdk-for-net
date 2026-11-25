namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A telemetry class with a nested class member (spec 001 FR-008; spec 003 US1-2: a nested structure)
    /// </summary>
    public sealed class LogWithNestedProperty : Telemetry
    {
        public int LogKey { get; set; }

        public Measurement Measurement { get; set; } = new();
    }

    /// <summary>
    /// A nested class: a member of this type is a nested structure, rejected by the flat-telemetry rule
    /// (constitution C7; spec 003 research R34 rejects spec 001's deferred owned members, R16)
    /// </summary>
    public sealed class Measurement
    {
        public decimal Diameter { get; set; }
    }
}
