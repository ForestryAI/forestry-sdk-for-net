namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A row class whose <c>LogVolume</c> is named <c>Volume</c> in the document (flow 2, R8)
    /// </summary>
    [XmlElementName("Log")]
    public sealed class LogWithRenamedMember : Row
    {
        public int LogKey { get; set; }

        [XmlElementName("Volume")]
        public decimal LogVolume { get; set; }
    }
}
