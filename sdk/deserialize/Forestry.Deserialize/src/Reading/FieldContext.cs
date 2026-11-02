namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// Where a value sits in the document, as the media reports it with each value (research R15).
    /// Text members are UTF-8 spans over bytes the media holds, so building one allocates nothing;
    /// nothing in it is kept unless a policy copies it
    /// </summary>
    /// <remarks>Media dimensions are deferred to the slice that needs them.</remarks>
    public readonly ref struct FieldContext
    {
        /// <summary>
        /// Field context reported by the media
        /// </summary>
        /// <param name="depth">Depth of the value in the media's hierarchy</param>
        /// <param name="name">Media name of the value, UTF-8</param>
        /// <param name="path">Media path of the value, UTF-8 (e.g. <c>Root/Log/LogKey</c>)</param>
        public FieldContext(int depth, ReadOnlySpan<byte> name, ReadOnlySpan<byte> path)
        {
            Depth = depth;
            Name = name;
            Path = path;
        }

        /// <summary>
        /// Depth of the value in the media's hierarchy
        /// </summary>
        public int Depth { get; }

        /// <summary>
        /// Media name of the value, UTF-8
        /// </summary>
        public ReadOnlySpan<byte> Name { get; }

        /// <summary>
        /// Media path of the value, UTF-8
        /// </summary>
        public ReadOnlySpan<byte> Path { get; }
    }
}
