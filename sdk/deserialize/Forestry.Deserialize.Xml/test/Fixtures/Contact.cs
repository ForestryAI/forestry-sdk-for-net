namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A contact inside a site whose policy copies its name onto the open site (spec 002, US1-3)
    /// </summary>
    public sealed class Contact : Row
    {
        public string? Name { get; set; }
    }
}
