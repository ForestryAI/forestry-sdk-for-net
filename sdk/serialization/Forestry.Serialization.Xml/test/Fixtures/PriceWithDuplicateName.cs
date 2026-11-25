namespace Forestry.Serialization.Xml.Tests.Fixtures
{
    /// <summary>
    /// Two members read from the same name, <c>currency</c>, both from the attribute: rejected
    /// when the options lock (spec 002 FR-018, tasks.md T047, T051)
    /// </summary>
    [XmlElementName("Price")]
    public sealed class PriceWithDuplicateName : Telemetry
    {
        [XmlAttributeName("currency")]
        public string? Currency { get; set; }

        [XmlAttributeName("currency")]
        public string? Code { get; set; }
    }
}
