namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// An order inside a site, around its lines (spec 002 fixtures, tasks.md T001)
    /// </summary>
    public sealed class Order : Row
    {
        public int OrderNumber { get; set; }

        public int SiteKey { get; set; }

        public string? Note { get; set; }
    }
}
