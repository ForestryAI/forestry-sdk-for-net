namespace Forestry.Serialization.Xml.Tests.Fixtures
{
    /// <summary>
    /// A telemetry class with two members read from the same element, <c>LogVolume</c> (R8)
    /// </summary>
    public sealed class LogWithDuplicateMember : Telemetry
    {
        public decimal LogVolume { get; set; }

        [XmlElementName("LogVolume")]
        public decimal Volume { get; set; }
    }
}
