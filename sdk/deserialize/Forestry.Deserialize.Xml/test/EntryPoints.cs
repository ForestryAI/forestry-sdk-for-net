using System.Text;

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
    /// The async branch reads the document as a stream through <c>Deserialization.DeserializeAsync</c>
    /// (US3, T066), blocking until every row is read so a theory stays synchronous.
    /// </remarks>
    public static class EntryPoints
    {
        public static List<Row> Read(EntryPoint entryPoint, string xml, DeserializeOptions options) =>
            entryPoint switch
            {
                EntryPoint.Sync => Deserialization.Deserialize(xml, options).ToList(),
                EntryPoint.Async => Deserialization.DeserializeAsync(new MemoryStream(Encoding.UTF8.GetBytes(xml)), options).ToBlockingEnumerable().ToList(),
                _ => throw new ArgumentOutOfRangeException(nameof(entryPoint))
            };
    }
}
