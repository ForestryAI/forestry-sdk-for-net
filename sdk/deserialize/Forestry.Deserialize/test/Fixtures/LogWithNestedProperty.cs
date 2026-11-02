namespace Forestry.Deserialize.Tests.Fixtures
{
    /// <summary>
    /// A row class with a property whose type no converter handles (FR-008, research R14)
    /// </summary>
    public sealed class LogWithNestedProperty : Row
    {
        public int LogKey { get; set; }

        public Measurement Measurement { get; set; } = new();
    }

    /// <summary>
    /// A nested class with no converter: rejected in this slice; read from a structure in the
    /// document it would be a complex (owned) member, a later slice (research R16)
    /// </summary>
    public sealed class Measurement
    {
        public decimal Diameter { get; set; }
    }
}
