using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Forestry.Turn
{
    /// <summary>
    /// Mutable dimensions used when turning into an answer
    /// </summary>
    public readonly struct IntentionDimensions: IEnumerable<Dimension>
    {
        /// <summary>
        /// Operations on dimensions are delegated to the concrete intention which
        /// will likely get help from the turning e.g. formatting
        /// </summary>
        /// <param name="intention"></param>
        internal IntentionDimensions(
            Intention intention
        ) {
            ArgumentNullException.ThrowIfNull(intention);

            _intention = intention;
        }

        private readonly Intention _intention;

        public IEnumerator<Dimension> GetEnumerator()
        {
            return _intention.EnumerateDimensions().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _intention.EnumerateDimensions().GetEnumerator();
        }

        /// <summary>
        /// Add dimension
        /// </summary>
        /// <param name="dimension"></param>
        public void Add(Dimension dimension)
        {
            _intention.AddDimension(dimension.Name, dimension.Value);
        }

        /// <summary>
        /// Add dimension from a name and value pair
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void Add(string name, string value) {
            _intention.AddDimension(name, value);
        }

        /// <summary>
        /// Try get dimension value by name
        /// </summary>
        /// <remarks>Dimensions with the same name are concatenated with a delimeter</remarks>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetValue(string name, [NotNullWhen(true)] out string? value)
        {
            return _intention.TryGetDimension(name, out value);
        }

        /// <summary>
        /// Try get values by name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public bool TryGetValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values)
        {
            return _intention.TryGetDimensionValues(name, out values);
        }

        /// <summary>
        /// Dimension with name argument exists
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool Contains(string name)
        {
            return _intention.ContainsDimension(name);
        }

        /// <summary>
        /// Set value by update when name exists else add
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetValue(string name, string value)
        {
            _intention.SetDimension(name, value);
        }

        /// <summary>
        /// True when dimension is removed by name otherwise fale if dimension does not exist
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool Remove(string name)
        {
            return _intention.RemoveDimension(name);
        }
    }
}
