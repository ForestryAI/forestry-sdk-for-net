namespace Forestry.Serialization.Xml
{
    /// <summary>
    /// The name of an element in the document (spec 003, research R37):
    /// <list type="bullet">
    /// <item>on a telemetry type, the element its occurrences have: <c>[XmlElementName("Tree")]</c> on <c>Log</c>
    /// reads each <c>&lt;Tree&gt;</c> as a <c>Log</c> telemetry;</item>
    /// <item>on a property or field, the child element its value comes from: <c>[XmlElementName("Volume")]</c>
    /// on <c>Log.LogVolume</c> reads <c>&lt;Volume&gt;0.59&lt;/Volume&gt;</c>.</item>
    /// </list>
    /// Without it the class or member name is used
    /// </summary>
    /// <remarks>
    /// Names in the document are the XML format's, so the attribute lives in this package, not in the
    /// deserialization package (constitution A5); spec 001 had <c>[Telemetry]</c> and <c>[Field]</c> there. Two telemetry
    /// types or two members with the same name fail when the options lock, and so does a member with another
    /// source too (<see cref="XmlAttributeNameAttribute"/>, <see cref="XmlElementTextAttribute"/>)
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class XmlElementNameAttribute : Attribute
    {
        /// <summary>
        /// The element is named <paramref name="name"/> in the document
        /// </summary>
        /// <param name="name">The element's name, as written in the document (a prefix included); required,
        /// since without the attribute the class or member name is used</param>
        public XmlElementNameAttribute(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            Name = name;
        }

        /// <summary>
        /// The element's name
        /// </summary>
        public string Name { get; }
    }
}
