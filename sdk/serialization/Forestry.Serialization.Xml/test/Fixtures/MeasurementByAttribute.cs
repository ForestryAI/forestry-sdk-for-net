namespace Forestry.Serialization.Xml.Tests.Fixtures
{
    /// <summary>
    /// US2's <see cref="Measurement"/> with its unit as an attribute: a wrapper telemetry type telling values
    /// apart by an attribute on its own element (spec 002, tasks.md T047, T052, research R29)
    /// </summary>
    [XmlElementName("Measurement")]
    public sealed class MeasurementByAttribute : Telemetry
    {
        [XmlAttributeName("unit")]
        public string? Unit { get; set; }

        public decimal Amount { get; set; }
    }
}
