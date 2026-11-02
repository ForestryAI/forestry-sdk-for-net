namespace Forestry.Deserialize.Tests.Fixtures
{
    /// <summary>
    /// A row class with a nullable member, whose converter wraps the one resolved for its underlying
    /// type (flow 3, R14)
    /// </summary>
    public sealed class LogWithNullableKey : Row
    {
        public int? LogKey { get; set; }
    }
}
