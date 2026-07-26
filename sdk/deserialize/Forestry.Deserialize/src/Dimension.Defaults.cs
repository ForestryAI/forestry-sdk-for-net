namespace Forestry.Deserialize
{
    /// <summary>
    /// Dimension defaults generic to the media being deserialized
    /// </summary>
    public readonly partial struct Dimension : IEquatable<Dimension>
    {
        public static class Names
        {
            public static string Date => "Date";

            public static string RawValueType => "Raw-Value-Type";

            public static string RawValueLength => "Raw-Value-Length";

            /// <summary>
            /// Nesting depth of the value within the source media (e.g. XML element depth)
            /// </summary>
            public static string Depth => "Depth";

            /// <summary>
            /// Namespace the value was read under, when the media has one (e.g. XML namespace URI)
            /// </summary>
            public static string Namespace => "Namespace";
        }
    }
}