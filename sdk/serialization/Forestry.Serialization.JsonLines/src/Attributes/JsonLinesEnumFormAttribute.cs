using Forestry.Serialization.Converters;

namespace Forestry.Serialization.JsonLines
{
    /// <summary>
    /// How JSON Lines writes this telemetry member's enum: as its name (a JSON string, the default) or as its
    /// underlying number (unquoted), e.g. <c>[JsonLinesEnumForm(EnumForm.Number)]</c> (spec 005 research R65)
    /// </summary>
    /// <remarks>
    /// Per member, so two members of one enum type can differ. A converter registered in the options for
    /// the enum type wins over it (e.g. StanForD's, which always writes StanForD texts). How a document is
    /// read is a different format's choice (e.g. <c>XmlEnumFormAttribute</c>).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class JsonLinesEnumFormAttribute : Attribute
    {
        /// <summary>
        /// Write this member's enum in <paramref name="form"/>
        /// </summary>
        /// <param name="form"></param>
        public JsonLinesEnumFormAttribute(EnumForm form) => Form = form;

        /// <summary>
        /// The form this member's enum is written in
        /// </summary>
        public EnumForm Form { get; }
    }
}
