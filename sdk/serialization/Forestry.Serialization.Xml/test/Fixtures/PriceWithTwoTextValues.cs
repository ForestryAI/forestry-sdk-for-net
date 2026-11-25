namespace Forestry.Serialization.Xml.Tests.Fixtures
{
    /// <summary>
    /// Two members marked as the element's text: rejected when the options lock (spec 002 FR-018,
    /// tasks.md T047, T053, research R30)
    /// </summary>
    [XmlElementName("Price")]
    public sealed class PriceWithTwoTextValues : Row
    {
        [XmlElementText]
        public decimal Amount { get; set; }

        [XmlElementText]
        public decimal Other { get; set; }
    }
}
