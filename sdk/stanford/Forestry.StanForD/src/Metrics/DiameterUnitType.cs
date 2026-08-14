using System.Xml.Serialization;

namespace Forestry.StanForD.Metrics
{
    [XmlType("DiameterUnit")]
    public enum DiameterUnitType
    {
        [XmlEnum("mm")]
        Mm
    }
}