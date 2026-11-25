namespace Forestry.Serialization.Xml.Tests.Fixtures
{
    /// <summary>
    /// A telemetry class with a <c>bool</c> member (flow 3, the XML default converter)
    /// </summary>
    [XmlElementName("Log")]
    public sealed class LogWithFlag : Telemetry
    {
        public int LogKey { get; set; }

        public bool TopSawing { get; set; }
    }
}
