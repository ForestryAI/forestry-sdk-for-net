namespace Forestry.Serialization.Xml.Tests.Fixtures
{
    // Members with more than one source in the document: rejected when the options lock (spec 003
    // US2-2b, FR-006a, tasks.md T020, T021, research R37)

    /// <summary>
    /// A member from a child element and from the telemetry's own text
    /// </summary>
    [XmlElementName("Price")]
    public sealed class PriceWithElementAndText : Telemetry
    {
        [XmlElementName("Value")]
        [XmlElementText]
        public decimal Amount { get; set; }
    }

    /// <summary>
    /// A member from an attribute and from a child element
    /// </summary>
    [XmlElementName("Price")]
    public sealed class PriceWithAttributeAndElement : Telemetry
    {
        [XmlAttributeName("currency")]
        [XmlElementName("Currency")]
        public string? Currency { get; set; }

        public decimal Amount { get; set; }
    }

    /// <summary>
    /// A member from an attribute that the test also makes the telemetry's own text through the options
    /// (<c>ElementText&lt;TTelemetry&gt;</c>)
    /// </summary>
    [XmlElementName("Price")]
    public sealed class PriceWithAttributeAndTextOption : Telemetry
    {
        [XmlAttributeName("currency")]
        public string? Currency { get; set; }

        public decimal Amount { get; set; }
    }
}
