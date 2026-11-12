namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A site around its orders (spec 002 fixtures, tasks.md T001): kept in the context, often not enumerated
    /// </summary>
    public sealed class Site : Row
    {
        public int SiteKey { get; set; }

        public string? Name { get; set; }

        public string? ContactName { get; set; }
    }
}
