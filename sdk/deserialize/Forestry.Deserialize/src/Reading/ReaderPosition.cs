using Forestry.Deserialize.Definitions;

namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// One level within a <see cref="ReaderPath"/>: a <see cref="TypeDefinition"/> together with
    /// which of its properties the deserialization will act on next.
    /// </summary>
    public struct ReaderPosition
    {
        public TypeDefinition TypeDefinition;
    }
}