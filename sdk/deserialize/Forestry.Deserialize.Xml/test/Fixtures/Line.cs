namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A line inside an order (spec 002 fixtures, tasks.md T001)
    /// </summary>
    public sealed class Line : Row
    {
        public int LineNumber { get; set; }

        public int OrderNumber { get; set; }

        public string? Note { get; set; }
    }
}
