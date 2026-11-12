namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A reference with an attribute and its own text, often empty (spec 002 US3-4, tasks.md T047):
    /// <c>&lt;Reference type="external" /&gt;</c>
    /// </summary>
    public sealed class Reference : Row
    {
        [XmlAttributeValue("type")]
        public string? Type { get; set; }

        [XmlTextValue]
        public string? Value { get; set; }
    }
}
