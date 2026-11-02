namespace Forestry.Deserialize
{
    public static partial class Deserialization
    {
        /// <summary>
        /// Rows from a document as UTF-8 bytes in memory, lazily and synchronously. A UTF-8 byte order
        /// mark is skipped; the options are locked and their definitions built before this returns, so
        /// a row type that cannot be defined throws here (FR-008)
        /// </summary>
        /// <param name="utf8"></param>
        /// <param name="options">A media's options with the row types added</param>
        /// <returns>The rows in document order, enumerable once</returns>
        public static IEnumerable<Row> Deserialize(ReadOnlyMemory<byte> utf8, DeserializeOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.MakeReadOnly();

            if (utf8.Span.StartsWith(Constants.Utf8Bom))
            {
                utf8 = utf8[Constants.Utf8Bom.Length..];
            }

            return new RowEnumerable(utf8, options);
        }
    }
}
