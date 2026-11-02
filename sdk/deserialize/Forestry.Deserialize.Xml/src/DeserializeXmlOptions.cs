using Forestry.Deserialize.Converters;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Xml
{
    /// <summary>
    /// Options of the XML media: the row types to shred, converters and settings. Associates elements
    /// with row types and members by name, or by <see cref="RowAttribute"/> and
    /// <see cref="FieldAttribute"/> names (research R6, R8)
    /// </summary>
    public sealed class DeserializeXmlOptions : DeserializeOptions
    {
        private static readonly Converter[] s_mediaConverters = [new XmlBooleanConverter()];

        private XmlAssociation? _association;

        /// <summary>
        /// XML defaults, e.g. XML Schema booleans
        /// </summary>
        protected override IEnumerable<Converter> MediaConverters => s_mediaConverters;

        /// <summary>
        /// Association of elements with row types and members, built once after the options are locked
        /// </summary>
        internal XmlAssociation Association
        {
            get
            {
                MakeReadOnly();
                return LazyInitializer.EnsureInitialized(ref _association, () => XmlAssociation.Create(this));
            }
        }

        /// <summary>
        /// The XML synchronous row enumerator
        /// </summary>
        /// <param name="utf8"></param>
        /// <returns></returns>
        protected internal override RowEnumerator CreateRowEnumerator(ReadOnlyMemory<byte> utf8) => new XmlRowEnumerator(utf8, this);
    }
}
