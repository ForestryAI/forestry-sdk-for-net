namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// Two members read from the same name, <c>currency</c>, both from the attribute: rejected
    /// when the options lock (spec 002 FR-018, tasks.md T047, T051)
    /// </summary>
    [Row("Price")]
    public sealed class PriceWithDuplicateName : Row
    {
        [XmlAttributeValue("currency")]
        public string? Currency { get; set; }

        [XmlAttributeValue("currency")]
        public string? Code { get; set; }
    }
}
