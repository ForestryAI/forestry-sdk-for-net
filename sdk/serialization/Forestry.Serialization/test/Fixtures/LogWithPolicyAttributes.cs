namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A telemetry with a Construct policy attached to the type by attribute (tasks.md T053, research R19)
    /// </summary>
    [Policy<ConstructMarkerPolicy>]
    public sealed class LogWithConstructMarker : Telemetry
    {
        public int LogKey { get; set; }

        public decimal LogVolume { get; set; }
    }

    /// <summary>
    /// A telemetry with a Set policy attached to one member by attribute (tasks.md T053, research R19)
    /// </summary>
    public sealed class LogWithSetMarkerOnKey : Telemetry
    {
        [Policy<SetMarkerPolicy>]
        public int LogKey { get; set; }

        public decimal LogVolume { get; set; }
    }

    /// <summary>
    /// A telemetry with a property and a field a definition policy leaves out (tasks.md T089, research R19)
    /// </summary>
    public sealed class LogWithExcludedMembers : Telemetry
    {
        public int LogKey { get; set; }

        public decimal ExcludedProperty { get; set; }

        public decimal ExcludedField;
    }
}
