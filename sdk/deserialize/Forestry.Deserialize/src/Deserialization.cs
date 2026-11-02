using System.Collections;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize
{
    /// <summary>
    /// Entry points: deserialize a document into rows of the row types added to a media's options
    /// (research R3). The media is chosen by the options' type
    /// </summary>
    public static partial class Deserialization
    {
        /// <summary>
        /// Rows from a document in memory, enumerated once: the media's row enumerator is created when
        /// enumeration starts, a second enumeration throws (research R2)
        /// </summary>
        private sealed class RowEnumerable(ReadOnlyMemory<byte> utf8, DeserializeOptions options) : IEnumerable<Row>
        {
            private int _isEnumerated;

            public IEnumerator<Row> GetEnumerator()
            {
                if (Interlocked.Exchange(ref _isEnumerated, 1) == 1)
                {
                    Throwing.WhenEnumeratedTwice();
                }

                return options.CreateRowEnumerator(utf8);
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
