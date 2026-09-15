using System.Buffers;

namespace Forestry.Deserialize.Xml.Reading
{
    /// <summary>
    /// Single segment reading
    /// </summary>
    public ref partial struct Utf8XmlReader
    {
        /// <summary>
        /// Reading segment from a byte span
        /// </summary>
        /// <param name="segment"></param>
        /// <param name="isReadingCompleted"></param>
        /// <param name="readerState"></param>
        public partial Utf8XmlReader(
            ReadOnlySpan<byte> segment,
            bool isReadingCompleted,
            ReaderState readerState
        ) {}
    }
}