using System.Buffers;
using System.Diagnostics;

namespace Forestry.Deserialize.Xml.Reading
{
    /// <summary>
    /// Multiple segment reading
    /// </summary>
    public ref partial struct Utf8XmlReader
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="isReadingCompleted"></param>
        /// <param name="readerState"></param>
        public partial Utf8XmlReader(
            ReadOnlySequence<byte> segments,
            bool isReadingCompleted,
            ReaderState readerState
        ): this(segments.FirstSpan, isReadingCompleted, readerState)
        {}
    }
}