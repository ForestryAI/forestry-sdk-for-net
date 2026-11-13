using System.Xml.Serialization;

namespace Forestry.StanForD.Metrics
{
    /// <summary>
    /// Unit of a diameter in StanForD 2010 (type <c>DiameterUnit</c>)
    /// </summary>
    [XmlType("DiameterUnit")]
    public enum DiameterUnitType
    {
        /// <summary>
        /// Millimetres (<c>mm</c>)
        /// </summary>
        [XmlEnum("mm")]
        Mm
    }
}