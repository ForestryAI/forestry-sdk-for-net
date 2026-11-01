namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// The walking skeleton's flat row class (spec 001, US1): one row per <c>Log</c> occurrence
    /// </summary>
    public sealed class Log : Row
    {
        public int LogKey { get; set; }

        public decimal LogVolume { get; set; }
    }
}
