namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A wrapper with meaning, added as a row type that is not enumerated: its policy sets the right
    /// member on its parent line from its unit (spec 002, tasks.md T032, T035, T040, research R29)
    /// </summary>
    public sealed class Measurement : Row
    {
        public string? Unit { get; set; }

        public decimal Amount { get; set; }
    }
}
