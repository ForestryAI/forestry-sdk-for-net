namespace Forestry.Xml.Reading
{
    /// <summary>
    /// Options for reading, carried by the <see cref="ReaderState"/> from one reader to the next
    /// </summary>
    /// <remarks>
    /// A struct's field at 0 can only mean "never set": the setters reject an explicit 0, and the
    /// reader constructors give an unset option its default, so <c>default(ReaderOptions)</c>
    /// reads with the defaults. Unlike <c>JsonReaderOptions</c>, an explicit 0 is never
    /// "default": a mistake throws where it is known (spec 001 research R5).
    /// </remarks>
    public struct ReaderOptions
    {
        internal const int DefaultMaxDepth = 64;

        internal const int DefaultMaxNameLength = 128;

        private int _maxDepth;

        private int _maxNameLength;

        /// <summary>
        /// Largest depth of open elements: a start tag at this depth throws a limit error at the
        /// start tag, before anything is committed. 64 when never set; setting 0, a negative
        /// value or more than 64 (the element stack's levels) throws
        /// <see cref="InvalidOperationException"/>.
        /// </summary>
        public int MaxDepth
        {
            readonly get => _maxDepth;
            set
            {
                if (value <= 0)
                {
                    Throwing.WhenValueNotPositive(value, nameof(MaxDepth));
                }

                if (value > ElementStack.NonAllocatingMaxDepth)
                {
                    Throwing.WhenValueAbove(value, nameof(MaxDepth), ElementStack.NonAllocatingMaxDepth);
                }

                _maxDepth = value;
            }
        }

        /// <summary>
        /// Largest name, in bytes, the reader reads: element names in start tags, attribute names,
        /// processing instruction targets, and entity names in references (only reached after a
        /// document type, since the five predefined names are short). A longer name throws where
        /// it is first read, at the first byte beyond the limit, before more of it is buffered; a
        /// processing instruction target is checked once the instruction is delimited. End tags
        /// are not bounded by it: an end tag longer than its start tag is a mismatch. 128 when
        /// never set; setting 0, a negative value or more than 2048 (the element stack's name
        /// bytes) throws <see cref="InvalidOperationException"/>.
        /// </summary>
        public int MaxNameLength
        {
            readonly get => _maxNameLength;
            set
            {
                if (value <= 0)
                {
                    Throwing.WhenValueNotPositive(value, nameof(MaxNameLength));
                }

                if (value > ElementStack.NameBytesCapacity)
                {
                    Throwing.WhenValueAbove(value, nameof(MaxNameLength), ElementStack.NameBytesCapacity);
                }

                _maxNameLength = value;
            }
        }
    }
}
