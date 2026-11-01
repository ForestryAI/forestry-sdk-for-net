namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A row class whose <c>LogVolume</c> allows null (spec edge case: empty element)
    /// </summary>
    public sealed class LogWithNullableVolume : Row
    {
        public int LogKey { get; set; }

        public decimal? LogVolume { get; set; }
    }
}
