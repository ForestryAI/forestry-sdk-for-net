namespace Forestry.Deserialize.Definitions
{
    /// <summary>
    /// Kind of type definition by its shape, from the design before spec 001
    /// </summary>
    /// <remarks>
    /// Not used in spec 001, where every type definition is a flat row; decided with the document's
    /// structure in a later spec (research R0, R10, R13).
    /// </remarks>
    public enum TypeDefinitionKind
    {
        /// <summary>No kind</summary>
        None = 0,

        /// <summary>An object with members</summary>
        Object = 1,

        /// <summary>A sequence of items</summary>
        Enumerable = 2,

        /// <summary>Keys with values</summary>
        Dictionary = 3
    }
}