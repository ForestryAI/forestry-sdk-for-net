namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// US2's <see cref="Measurement"/> with its unit as an attribute: a wrapper row type telling values
    /// apart by an attribute on its own element (spec 002, tasks.md T047, T052, research R29)
    /// </summary>
    [Row("Measurement")]
    public sealed class MeasurementByAttribute : Row
    {
        [XmlAttributeValue("unit")]
        public string? Unit { get; set; }

        public decimal Amount { get; set; }
    }
}
