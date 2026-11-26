using System.Collections;
using Forestry.Serialization.Reading;

namespace Forestry.Serialization
{
    /// <summary>
    /// Entry points: deserialize a document into telemetries of the telemetry types added to a format's options
    /// (research R3). The format is chosen by the options' type
    /// </summary>
    public static partial class TelemetrySerialization
    {
        /// <summary>
        /// Telemetries from a document in memory, enumerated once: the format's telemetry enumerator is created when
        /// enumeration starts, a second enumeration throws (research R2)
        /// </summary>
        private sealed class TelemetryEnumerable(ReadOnlyMemory<byte> utf8, DeserializeOptions options) : IEnumerable<Telemetry>
        {
            private int _isEnumerated;

            public IEnumerator<Telemetry> GetEnumerator()
            {
                if (Interlocked.Exchange(ref _isEnumerated, 1) == 1)
                {
                    Throwing.WhenEnumeratedTwice();
                }

                return options.CreateTelemetryEnumerator(utf8);
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
