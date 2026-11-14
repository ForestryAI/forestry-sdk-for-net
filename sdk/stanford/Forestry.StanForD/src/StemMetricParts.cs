using Forestry.Deserialize;
using Forestry.Deserialize.Xml;

namespace Forestry.StanForD {
    /// <summary>
    /// Where a stem was felled, from StanForD <c>Stem/StemCoordinates</c>; never comes out, its altitude
    /// and coordinate reference system are copied onto the <see cref="Stem"/>
    /// </summary>
    public sealed class StemCoordinates : Row
    {
        /// <summary>
        /// The coordinate reference system, e.g. "WGS84", from the <c>coordinateReferenceSystem</c> attribute
        /// </summary>
        [XmlAttributeValue("coordinateReferenceSystem")]
        public CoordinateReferenceSystem CoordinateReferenceSystem { get; set; }

        /// <summary>
        /// Height above sea level, from StanForD <c>StemCoordinates/Altitude</c>
        /// </summary>
        public decimal Altitude { get; set; }
    }

    /// <summary>
    /// A stem's latitude, from StanForD <c>StemCoordinates/Latitude</c>; never comes out, its value is
    /// copied onto the <see cref="Stem"/>, signed by its category
    /// </summary>
    public sealed class Latitude : Row
    {
        /// <summary>
        /// "North" or "South", from the <c>latitudeCategory</c> attribute
        /// </summary>
        [XmlAttributeValue("latitudeCategory")]
        public string LatitudeCategory { get; set; } = string.Empty;

        /// <summary>
        /// The latitude's absolute value in decimal degrees
        /// </summary>
        [XmlTextValue]
        public decimal Value { get; set; }
    }

    /// <summary>
    /// A stem's longitude, from StanForD <c>StemCoordinates/Longitude</c>; never comes out, its value is
    /// copied onto the <see cref="Stem"/>, signed by its category
    /// </summary>
    public sealed class Longitude : Row
    {
        /// <summary>
        /// "East" or "West", from the <c>longitudeCategory</c> attribute
        /// </summary>
        [XmlAttributeValue("longitudeCategory")]
        public string LongitudeCategory { get; set; } = string.Empty;

        /// <summary>
        /// The longitude's absolute value in decimal degrees
        /// </summary>
        [XmlTextValue]
        public decimal Value { get; set; }
    }

    /// <summary>
    /// How the boom stood, from StanForD <c>Stem/BoomPositioning</c>; never comes out, its angle and
    /// extension are copied onto the <see cref="Stem"/> members of its category
    /// </summary>
    public sealed class BoomPositioning : Row
    {
        /// <summary>
        /// "Felling" or "Last log cut", from the <c>boomPositioningCategory</c> attribute
        /// </summary>
        [XmlAttributeValue("boomPositioningCategory")]
        public string BoomPositioningCategory { get; set; } = string.Empty;

        /// <summary>
        /// Boom angle (0-360 degrees), from StanForD <c>BoomPositioning/BoomAngle</c>
        /// </summary>
        public int BoomAngle { get; set; }

        /// <summary>
        /// Boom extension, in the document's length unit, from StanForD <c>BoomPositioning/BoomExtension</c>
        /// </summary>
        public int BoomExtension { get; set; }
    }

    /// <summary>
    /// A single-tree processed stem's own measurements, from StanForD <c>Stem/SingleTreeProcessedStem</c>;
    /// never comes out, its DBH is copied onto the <see cref="Stem"/>
    /// </summary>
    public sealed class SingleTreeProcessedStem : Row
    {
        /// <summary>
        /// Diameter at breast height, in the document's diameter unit, from StanForD
        /// <c>SingleTreeProcessedStem/DBH</c>
        /// </summary>
        public int DBH { get; set; }
    }
}
