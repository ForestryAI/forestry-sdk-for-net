namespace Forestry.Deserialize
{
    /// <summary>
    /// Abstract base of the developer's row classes. A row is an instance of a class extending
    /// <see cref="Row"/> whose typed properties and fields hold the values shredded for one
    /// occurrence in the document (research R4)
    /// </summary>
    /// <remarks>
    /// A row's public surface is only <see cref="Is{T}"/> and <see cref="AsType{T}"/>; both are
    /// non-virtual and need nothing stored on the row, so a row costs only its typed members (C1)
    /// </remarks>
    public abstract class Row
    {
        /// <summary>
        /// Row base for a derived class
        /// </summary>
        protected Row()
        {
        }

        /// <summary>
        /// Whether this row is a <typeparamref name="T"/>, as C#'s <c>is</c>: true for
        /// <typeparamref name="T"/> and for classes deriving from it
        /// </summary>
        /// <typeparam name="T">A row class</typeparam>
        /// <returns></returns>
        public bool Is<T>() where T : Row => this is T;

        /// <summary>
        /// This row as a <typeparamref name="T"/>: the same instance, with its values already typed
        /// </summary>
        /// <typeparam name="T">A row class</typeparam>
        /// <returns></returns>
        /// <exception cref="InvalidCastException">The row is not a <typeparamref name="T"/>; the message
        /// names both classes</exception>
        public T AsType<T>() where T : Row
        {
            T? row = this as T;

            if (row is null)
            {
                Throwing.WhenRowNotOfType(GetType(), typeof(T));
            }

            return row;
        }
    }
}
