namespace Forestry.Deserialize.Xml.Reading
{
    public struct ReaderOptions
    {
        internal const int DefaultMaxDepth = 64;

        private int _maxDepth;

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