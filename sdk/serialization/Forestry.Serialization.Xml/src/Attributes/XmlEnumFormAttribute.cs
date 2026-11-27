using Forestry.Serialization.Converters;

namespace Forestry.Serialization.Xml
{
    /// <summary>
    /// How an XML document writes this telemetry member's enum: as its name (the default, ignoring case when
    /// read) or as its underlying number, e.g. <c>[XmlEnumForm(EnumForm.Number)]</c> (spec 005 research R64)
    /// </summary>
    /// <remarks>
    /// XML text has no string or number kind, so the member says which one to expect, as
    /// <c>XmlBooleanConverter</c> knows XML Schema's booleans. Per member, so two members of one enum type can
    /// differ. A converter registered in the options for the enum type wins over it (e.g. StanForD's). Not
    /// named <c>XmlEnumAttribute</c>, which <c>System.Xml.Serialization</c> already uses.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class XmlEnumFormAttribute : Attribute
    {
        /// <summary>
        /// Read this member's enum in <paramref name="form"/>
        /// </summary>
        /// <param name="form"></param>
        public XmlEnumFormAttribute(EnumForm form) => Form = form;

        /// <summary>
        /// The form this member's enum is read in
        /// </summary>
        public EnumForm Form { get; }
    }
}
