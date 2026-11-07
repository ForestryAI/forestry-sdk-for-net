namespace Forestry.Deserialize
{
    /// <summary>
    /// Kind of deserializer by the shape it reads, from the design before spec 001
    /// </summary>
    /// <remarks>
    /// Not used in spec 001: it mirrors the document's hierarchy instead of shredding into flat rows,
    /// and whether it stays is decided with the document's structure in a later spec (research R0, R10, R13).
    /// </remarks>
    public enum DeserializerKind: byte
    {
        /// <summary>No kind</summary>
        None = 0x0,

        /// <summary>An object with members</summary>
        Object = 0x1,

        /// <summary>A single value</summary>
        Value = 0x2,

        /// <summary>A sequence of items</summary>
        Enumerable = 0x8,

        /// <summary>Keys with values</summary>
        Dictionary = 0x10
    }
}