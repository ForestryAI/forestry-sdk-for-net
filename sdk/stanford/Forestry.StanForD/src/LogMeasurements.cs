using Forestry.Deserialize;
using Forestry.Deserialize.Xml;

namespace Forestry.StanForD {
    /// <summary>
    /// One of a log's volumes, from StanForD <c>Log/LogVolume</c>; never comes out, its value lands in
    /// the <see cref="Log"/> member its categories match
    /// </summary>
    public sealed class LogVolume : Row
    {
        /// <summary>
        /// Volume category, e.g. "m3sob", "m3sub" or "m3 (price)", from the <c>logVolumeCategory</c>
        /// attribute
        /// </summary>
        [XmlAttributeValue("logVolumeCategory")]
        public string LogVolumeCategory { get; set; } = string.Empty;

        /// <summary>
        /// Who measured, e.g. "Machine", from the <c>logMeasurementCategory</c> attribute
        /// </summary>
        [XmlAttributeValue("logMeasurementCategory")]
        public string LogMeasurementCategory { get; set; } = string.Empty;

        /// <summary>
        /// The volume, in the document's volume unit
        /// </summary>
        [XmlTextValue]
        public decimal Value { get; set; }
    }

    /// <summary>
    /// A log's measurement, from StanForD <c>Log/LogMeasurement</c>; never comes out, its category
    /// tells which <see cref="Log"/> members its diameters and length fill
    /// </summary>
    public sealed class LogMeasurement : Row
    {
        /// <summary>
        /// Who measured, e.g. "Machine", from the <c>logMeasurementCategory</c> attribute
        /// </summary>
        [XmlAttributeValue("logMeasurementCategory")]
        public string LogMeasurementCategory { get; set; } = string.Empty;

        /// <summary>
        /// The log's length, in the document's length unit, from StanForD <c>LogMeasurement/LogLength</c>
        /// </summary>
        public int LogLength { get; set; }
    }

    /// <summary>
    /// One of a log's diameters, from StanForD <c>LogMeasurement/LogDiameter</c>; never comes out, its
    /// value lands in the <see cref="Log"/> member its category (and its measurement's) match
    /// </summary>
    public sealed class LogDiameter : Row
    {
        /// <summary>
        /// Diameter category, e.g. "Butt ob" or "Mid HKS ub", from the <c>logDiameterCategory</c> attribute
        /// </summary>
        [XmlAttributeValue("logDiameterCategory")]
        public string LogDiameterCategory { get; set; } = string.Empty;

        /// <summary>
        /// The diameter, in the document's diameter unit
        /// </summary>
        [XmlTextValue]
        public int Value { get; set; }
    }
}
