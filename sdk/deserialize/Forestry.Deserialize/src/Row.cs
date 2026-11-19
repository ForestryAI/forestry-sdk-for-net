namespace Forestry.Deserialize
{
    /// <summary>
    /// Abstract base of the developer's row classes. A row is an instance of a class extending
    /// <see cref="Row"/> whose typed properties and fields hold the values flattened for one
    /// occurrence in the document (research R4); a row is the unit deserialization enumerates
    /// </summary>
    /// <remarks>
    /// <para>
    /// A row is flat: every member is a single value, a primitive, a <see cref="string"/>, an enum, or a
    /// struct that formats to UTF-8 and parses from text (e.g. <see cref="Guid"/>,
    /// <see cref="DateTimeOffset"/>), or a nullable of one of these. Never a nested object, a collection
    /// or a composite struct; a row type with such a member fails when the options lock (constitution
    /// C7; spec 003, research R32, R33). So each member can become one column in any big data format.
    /// "Row" names the unit enumerated and has nothing to do with row-based vs columnar formats.
    /// </para>
    /// <para>
    /// A row's public surface is only <see cref="Is{T}"/> and <see cref="AsType{T}"/>; both are
    /// non-virtual and need nothing stored on the row, so a row costs only its typed members (C1)
    /// </para>
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
        /// A copy of this row for the consumer when the context keeps the original (spec 002 isolation,
        /// research R25); shallow is enough since members are single values
        /// </summary>
        /// <returns></returns>
        internal Row ShallowCopy() => (Row)MemberwiseClone();

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
