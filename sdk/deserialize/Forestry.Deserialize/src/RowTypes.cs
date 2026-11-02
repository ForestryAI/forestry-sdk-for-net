using System.Collections;

namespace Forestry.Deserialize
{
    /// <summary>
    /// Row types added explicitly to an options instance; locked with the options (research R12).
    /// Only these types' rows are handed out; there is no assembly scan
    /// </summary>
    public sealed class RowTypes : IReadOnlyCollection<Type>
    {
        internal RowTypes(DeserializeOptions options)
        {
            _options = options;
        }

        private readonly DeserializeOptions _options;

        private readonly List<Type> _types = [];

        /// <summary>
        /// Add a row type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>This collection, for chaining</returns>
        public RowTypes Add<T>() where T : Row => Add(typeof(T));

        /// <summary>
        /// Add row types; a type not extending <see cref="Row"/> throws when added
        /// </summary>
        /// <param name="types"></param>
        /// <returns>This collection, for chaining</returns>
        public RowTypes Add(params Type[] types)
        {
            ArgumentNullException.ThrowIfNull(types);
            _options.ThrowingAssertNotReadOnly();

            foreach (Type type in types)
            {
                ArgumentNullException.ThrowIfNull(type, nameof(types));

                if (!type.IsSubclassOf(typeof(Row)) || type.IsAbstract)
                {
                    Throwing.WhenNotARowType(type);
                }

                if (!_types.Contains(type))
                {
                    _types.Add(type);
                }
            }

            return this;
        }

        /// <summary>
        /// Number of row types added
        /// </summary>
        public int Count => _types.Count;

        /// <summary>
        /// Row types in the order added
        /// </summary>
        /// <returns></returns>
        public IEnumerator<Type> GetEnumerator() => _types.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
