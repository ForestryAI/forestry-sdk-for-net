using System.Collections;

namespace Forestry.Deserialize
{
    /// <summary>
    /// A list that lets a derived class act before and after each change, e.g. refuse changes once the
    /// options are read-only (research R6, R10). Items are never null
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class ObservableList<T> : IList<T>
    {
        /// <summary>
        /// List starting with a copy of <paramref name="items"/>
        /// </summary>
        /// <param name="items"></param>
        public ObservableList(
            IEnumerable<T>? items = null
        ) { 
            _items = items is null ? [] : [.. items]; 
        }

        /// <summary>
        /// The items
        /// </summary>
        protected readonly List<T> _items;

        /// <summary>
        /// Item at <paramref name="index"/>; setting it is a change
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public T this[int index] { 
            get => _items[index];
            set {
                ArgumentNullException.ThrowIfNull(value);

                Before();
                Before(value);

                _items[index] = value;

                After();
            } 
        }

        /// <summary>
        /// Number of items
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// Add an item (a change)
        /// </summary>
        /// <param name="item"></param>
        public void Add(T item)
        {
            ArgumentNullException.ThrowIfNull(item);

            Before();
            Before(item);

            _items.Add(item);

            After();
        }

        /// <summary>
        /// Remove every item (a change)
        /// </summary>
        public void Clear()
        {
            Before();

            _items.Clear();

            After();
        }

        /// <summary>
        /// Whether the list contains <paramref name="item"/>
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(T item) => _items.Contains(item);

        /// <summary>
        /// Copy the items into <paramref name="array"/> from <paramref name="arrayIndex"/>
        /// </summary>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

        /// <summary>
        /// Enumerate the items
        /// </summary>
        /// <returns></returns>
        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

        /// <summary>
        /// Index of <paramref name="item"/>, or -1
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public int IndexOf(T item) => _items.IndexOf(item);

        /// <summary>
        /// Insert an item at <paramref name="index"/> (a change)
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        public void Insert(int index, T item)
        {
            ArgumentNullException.ThrowIfNull(item);

            Before();
            Before(item);

            _items.Insert(index, item);

            After();
        }

        /// <summary>
        /// Remove <paramref name="item"/> (a change)
        /// </summary>
        /// <param name="item"></param>
        /// <returns>Whether it was removed; <see cref="After"/> is called only then</returns>
        public bool Remove(T item)
        {
            Before();

            bool flag = _items.Remove(item);
            if (flag)
            {
                After();
            }

            return flag;
        }

        /// <summary>
        /// Remove the item at <paramref name="index"/> (a change)
        /// </summary>
        /// <param name="index"></param>
        public void RemoveAt(int index)
        {
            Before();

            _items.RemoveAt(index);

            After();
        }

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        /// <summary>
        /// Whether changes are refused
        /// </summary>
        public abstract bool IsReadOnly { get; }

        /// <summary>
        /// Before every operation that may change the list (set, add, insert, remove, clear), even one
        /// that turns out to change nothing, e.g. removing an item that is not there; e.g. throw when
        /// read-only
        /// </summary>
        protected abstract void Before();

        /// <summary>
        /// Before <paramref name="item"/> is set, added or inserted, after <see cref="Before()"/>
        /// </summary>
        /// <param name="item"></param>
        protected virtual void Before(T item) { }

        /// <summary>
        /// After an operation went through. Not called when <see cref="Remove"/> did not find the item
        /// (it returns false); still called after <see cref="Clear"/> on an empty list
        /// </summary>
        protected virtual void After() { }
    }
}