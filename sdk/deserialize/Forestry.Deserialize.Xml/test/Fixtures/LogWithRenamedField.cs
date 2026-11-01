namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A row class whose <c>LogVolume</c> is named <c>Volume</c> in the document (flow 2, R8)
    /// </summary>
    [Row("Log")]
    public sealed class LogWithRenamedField : Row
    {
        public int LogKey { get; set; }

        [Field("Volume")]
        public decimal LogVolume { get; set; }
    }
}
