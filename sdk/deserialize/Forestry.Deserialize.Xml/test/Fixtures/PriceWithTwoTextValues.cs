namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// Two members marked as the element's text: rejected when the options lock (spec 002 FR-018,
    /// tasks.md T047, T053, research R30)
    /// </summary>
    [Row("Price")]
    public sealed class PriceWithTwoTextValues : Row
    {
        [XmlTextValue]
        public decimal Amount { get; set; }

        [XmlTextValue]
        public decimal Other { get; set; }
    }
}
