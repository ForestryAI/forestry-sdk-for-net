namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A row class with two members read from the same element, <c>LogVolume</c> (R8)
    /// </summary>
    public sealed class LogWithDuplicateMember : Row
    {
        public decimal LogVolume { get; set; }

        [XmlElementName("LogVolume")]
        public decimal Volume { get; set; }
    }
}
