namespace Forestry.Deserialize
{
    /// <summary>
    /// Marks a property or field of a row with the name its value has in the document; without it
    /// the member name is used. Data only: the media interprets it (research R8)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class FieldAttribute : Attribute
    {
        /// <summary>
        /// Field attribute with the name the member's value has in the document
        /// </summary>
        /// <param name="name">Media name; null or empty keeps the member name</param>
        public FieldAttribute(string? name = null)
        {
            Name = name;
        }

        /// <summary>
        /// Media name of the member's value, or null for the member name
        /// </summary>
        public string? Name { get; }
    }
}
