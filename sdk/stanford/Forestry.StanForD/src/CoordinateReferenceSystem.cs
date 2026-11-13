namespace Forestry.StanForD {
    /// <summary>
    /// Many coordinate reference systems exist e.g. UTM, MGRS, NAD83, Web Mercator etc. but 
    /// StanForD only supports WGS85
    /// </summary>
    public enum CoordinateReferenceSystem
    {
        /// <summary>
        /// Global latitude–longitude values defined by the World Geodetic System 1984 
        /// (EPSG:4326), the reference frame used by GPS.
        /// </summary>
        WGS84
    }
}