namespace Forestry.Deserialize
{
    /// <summary>
    /// Marks a class extending <see cref="Row"/> with the name its occurrences have in the document;
    /// without it the class name is used. Data only: the media interprets it (research R8)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class RowAttribute : Attribute
    {
        /// <summary>
        /// Row attribute with the name occurrences have in the document
        /// </summary>
        /// <param name="name">Media name; null or empty keeps the class name</param>
        public RowAttribute(string? name = null)
        {
            Name = name;
        }

        /// <summary>
        /// Media name of the row's occurrences, or null for the class name
        /// </summary>
        public string? Name { get; }
    }
}
