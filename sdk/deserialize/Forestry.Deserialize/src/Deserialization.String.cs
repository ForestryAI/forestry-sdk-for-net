using System.Text;

namespace Forestry.Deserialize
{
    public static partial class Deserialization
    {
        /// <summary>
        /// Rows from a document as text, lazily and synchronously. The text is encoded to UTF-8; the
        /// options are locked and their definitions built before this returns, so a row type that
        /// cannot be defined throws here (FR-008)
        /// </summary>
        /// <param name="document"></param>
        /// <param name="options">A media's options with the row types added</param>
        /// <returns>The rows in document order, enumerable once</returns>
        public static IEnumerable<Row> Deserialize(string document, DeserializeOptions options)
        {
            ArgumentNullException.ThrowIfNull(document);

            return Deserialize(Encoding.UTF8.GetBytes(document), options);
        }
    }
}
