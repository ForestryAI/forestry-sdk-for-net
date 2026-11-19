namespace Forestry.Deserialize.Xml
{
    /// <summary>
    /// Marks a property or field of a row as set only from an attribute of the row's own element (spec
    /// 002 US3, research R30; named in spec 003, R37): <c>[XmlAttributeName("currency")]</c> on
    /// <c>Price.Currency</c> reads <c>&lt;Price currency="EUR"&gt;</c>. A child element of the same name
    /// never sets it
    /// </summary>
    /// <remarks>
    /// Where a value comes from is the XML format's, so the attribute lives in this package, not in the
    /// deserialization package (constitution A5). Two members with the same attribute name fail when the
    /// options lock, and so does a member with another source too (<see cref="XmlElementNameAttribute"/>,
    /// <see cref="XmlElementTextAttribute"/>). Spec 002 named it <c>XmlAttributeValue</c>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class XmlAttributeNameAttribute : Attribute
    {
        /// <summary>
        /// The member is set from the row element's attribute <paramref name="name"/>
        /// </summary>
        /// <param name="name">The attribute's name, as written in the document (a prefix included)</param>
        public XmlAttributeNameAttribute(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            Name = name;
        }

        /// <summary>
        /// The attribute's name
        /// </summary>
        public string Name { get; }
    }
}
