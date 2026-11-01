namespace Forestry.Deserialize.Tests.Fixtures
{
    /// <summary>
    /// The walking skeleton's flat row class (spec 001, US1), for core tests
    /// </summary>
    public sealed class Log : Row
    {
        public int LogKey { get; set; }

        public decimal LogVolume { get; set; }
    }
}
