namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A row class whose occurrences are named <c>Tree</c> in the document (flows 1 and 2, R8)
    /// </summary>
    [Row("Tree")]
    public sealed class TreeLog : Row
    {
        public int LogKey { get; set; }
    }
}
