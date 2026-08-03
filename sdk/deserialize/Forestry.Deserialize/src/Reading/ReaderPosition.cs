using Forestry.Deserialize.Definitions;

namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// One level within a <see cref="ReaderPath"/>: a <see cref="TypeDefinition"/> together with
    /// which of its properties the deserialization will act on next.
    /// </summary>
    public struct ReaderPosition
    {
        /// <summary>
        /// <see cref="TypeDefinition"/> being deserialized
        /// </summary>
        public TypeDefinition TypeDefinition;

        /// <summary>
        /// <see cref="PropertyDefinition"/> being deserialized
        /// </summary>
        public PropertyDefinition PropertyDefinition;

        /// <summary>
        /// Deserialization property index
        /// </summary>
        public int PropertyIndex;

        /// <summary>
        /// Utf8 property name i.e. useful when need both key and name for dictionaries
        /// </summary>
        public byte[] PropertyUtf8Name;

        /// <summary>
        /// Unescaped property name
        /// </summary>
        public string ProopertyName;
    }
}