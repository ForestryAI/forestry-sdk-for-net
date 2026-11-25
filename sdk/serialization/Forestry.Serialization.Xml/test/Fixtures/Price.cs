namespace Forestry.Serialization.Xml.Tests.Fixtures
{
    /// <summary>
    /// A price told apart by attributes on its own element, its value the element's own text (spec 002
    /// US3, tasks.md T047): <c>&lt;Price currency="EUR" kind="net"&gt;0.59&lt;/Price&gt;</c>
    /// </summary>
    public sealed class Price : Telemetry
    {
        [XmlAttributeName("currency")]
        public string? Currency { get; set; }

        [XmlAttributeName("kind")]
        public string? Kind { get; set; }

        [XmlElementText]
        public decimal Amount { get; set; }
    }
}
