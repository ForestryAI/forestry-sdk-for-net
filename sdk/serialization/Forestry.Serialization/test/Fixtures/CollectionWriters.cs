using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A collection stream that records how it was completed, as a monitoring formatter would hand the
    /// collection on or abandon it (spec 004 tasks.md T034, research R51)
    /// </summary>
    public sealed class RecordingCollectionStream(Stream inner, MonitoringCollectionOptions? options = null) : MonitoringCollectionStream(inner, options)
    {
        /// <summary>
        /// The inner stream, to read its length when completed
        /// </summary>
        private readonly Stream _inner = inner;

        /// <summary>
        /// Whether <see cref="OnCompleted"/> ran
        /// </summary>
        public bool Completed { get; private set; }

        /// <summary>
        /// The exception it was completed with; null when the collection is ready
        /// </summary>
        public Exception? CompletedWith { get; private set; }

        /// <summary>
        /// The inner stream's length when <see cref="OnCompleted"/> ran: every byte must be there by then
        /// </summary>
        public long InnerLengthWhenCompleted { get; private set; } = -1;

        protected override void OnCompleted(Exception? exception)
        {
            Completed = true;
            CompletedWith = exception;
            InnerLengthWhenCompleted = _inner.CanSeek ? _inner.Length : -1;
        }
    }

    /// <summary>
    /// A collection stream that counts the calls to its <see cref="Write(ReadOnlySpan{byte})"/> override, so a
    /// test sees a monitoring formatter's override run (T037, FR-018)
    /// </summary>
    public sealed class CountingCollectionStream(Stream inner, MonitoringCollectionOptions? options = null) : MonitoringCollectionStream(inner, options)
    {
        public int Writes { get; private set; }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Writes++;
            base.Write(buffer);
        }
    }
}
