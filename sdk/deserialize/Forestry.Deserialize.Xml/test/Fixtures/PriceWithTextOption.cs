namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A text member by attribute that the options can replace with another member (spec 002, tasks.md
    /// T047, T053, research R30)
    /// </summary>
    [XmlElementName("Price")]
    public sealed class PriceWithTextOption : Row
    {
        [XmlElementText]
        public decimal Amount { get; set; }

        public decimal Other { get; set; }
    }
}
