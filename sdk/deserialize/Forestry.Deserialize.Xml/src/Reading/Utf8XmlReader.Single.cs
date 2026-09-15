using System.Buffers;
using System.Runtime.CompilerServices;

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

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        internal bool ReadSingleSegment()
        {
            bool advancement = false;

            Value = default;
            
            if (!IsSegmentDrained())
            {
                goto Completed;
            }

            Completed:
                return advancement;
        }

        /// <summary>
        /// Current segment is not drained when the segment position is 
        /// less than the size of the segment
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsSegmentDrained()
        {
            if (_segmentPosition >= (uint)_segment.Length)
            {
                if (IsLastSegment)
                {
                    // TODO: Throw when no root element
                }

                return false;
            }

            return true;
        }
    }
}