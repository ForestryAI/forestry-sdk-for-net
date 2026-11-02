namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A row class with public fields instead of properties (flow 2, compiled-expression setter)
    /// </summary>
    public sealed class LogWithFieldMember : Row
    {
        public int LogKey;

        public decimal LogVolume;
    }
}
