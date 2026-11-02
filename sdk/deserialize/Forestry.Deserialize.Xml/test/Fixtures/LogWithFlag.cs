namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A row class with a <c>bool</c> member (flow 3, the XML default converter)
    /// </summary>
    [Row("Log")]
    public sealed class LogWithFlag : Row
    {
        public int LogKey { get; set; }

        public bool TopSawing { get; set; }
    }
}
