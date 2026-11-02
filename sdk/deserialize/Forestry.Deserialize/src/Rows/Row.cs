namespace Forestry.Deserialize
{
    /// <summary>
    /// Abstract base of the developer's row classes. A row is an instance of a class extending
    /// <see cref="Row"/> whose typed properties and fields hold the values shredded for one
    /// occurrence in the document (research R4)
    /// </summary>
    public abstract class Row
    {
        /// <summary>
        /// Row base for a derived class
        /// </summary>
        protected Row()
        {
        }
    }
}
