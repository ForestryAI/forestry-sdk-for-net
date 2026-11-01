namespace Forestry.Deserialize.Xml.Tests
{
    /// <summary>
    /// Entry point an acceptance theory runs a document through (SC-001: sync and async agree)
    /// </summary>
    public enum EntryPoint
    {
        Sync,

        Async
    }

    /// <summary>
    /// Runs a document through an entry point and collects the rows (tasks.md T008)
    /// </summary>
    /// <remarks>
    /// The async branch throws until US3 (T066) so that no test references
    /// <c>Deserialization.DeserializeAsync</c> before it exists; the theories' async rows are
    /// skipped with <c>Skip = "US3"</c> until then.
    /// </remarks>
    public static class EntryPoints
    {
        public static List<Row> Read(EntryPoint entryPoint, string xml, DeserializeOptions options) =>
            entryPoint switch
            {
                EntryPoint.Sync => Deserialization.Deserialize(xml, options).ToList(),
                EntryPoint.Async => throw new NotSupportedException("The async entry point comes with US3 (T066)"),
                _ => throw new ArgumentOutOfRangeException(nameof(entryPoint))
            };
    }
}
