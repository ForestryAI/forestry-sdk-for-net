namespace Forestry.Deserialize
{
    /// <summary>
    /// Where a value is read in the document: its depth, name and path, reported by the format with each
    /// value (spec 001 research R15; spec 003 R36). Generic, not specific to one format. Text members are
    /// UTF-8 spans over bytes the format holds, so building one allocates nothing; nothing in it is kept
    /// unless a policy copies it
    /// </summary>
    /// <remarks>
    /// Spec 002 named it <c>FieldContext</c>. After the POC, reading information may be known when the
    /// options lock, as type and property definitions are built (spec 003 R36).
    /// </remarks>
    public readonly ref struct ReadingInformation
    {
        /// <summary>
        /// Reading information reported by the format
        /// </summary>
        /// <param name="depth">Depth of the value in the document's hierarchy</param>
        /// <param name="name">Name of the node the value is read from, UTF-8</param>
        /// <param name="path">Path to that node, UTF-8 (e.g. <c>Root/Log/LogKey</c>)</param>
        public ReadingInformation(int depth, ReadOnlySpan<byte> name, ReadOnlySpan<byte> path)
        {
            Depth = depth;
            Name = name;
            Path = path;
        }

        /// <summary>
        /// Depth of the value in the document's hierarchy
        /// </summary>
        public int Depth { get; }

        /// <summary>
        /// Name of the node the value is read from, UTF-8
        /// </summary>
        public ReadOnlySpan<byte> Name { get; }

        /// <summary>
        /// Path to the node the value is read from, UTF-8
        /// </summary>
        public ReadOnlySpan<byte> Path { get; }
    }
}
