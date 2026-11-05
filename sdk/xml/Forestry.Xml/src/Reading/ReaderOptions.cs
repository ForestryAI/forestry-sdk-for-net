namespace Forestry.Xml.Reading
{
    /// <summary>
    /// Options for reading, carried by the <see cref="ReaderState"/> from one reader to the next
    /// </summary>
    public struct ReaderOptions
    {
        internal const int DefaultMaxDepth = 64;

        private int _maxDepth;

        /// <summary>
        /// Largest depth of open elements; 64 when never set. A negative value throws
        /// </summary>
        public int MaxDepth
        {
            readonly get => _maxDepth;
            set
            {
                if (value < 0)
                {
                    Throwing.WhenValueNotPositive(value, nameof(MaxDepth));
                }

                _maxDepth = value;
            }
        }
    }
}