using System.Buffers;

namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// Synchronous or asynchronous buffering of streaming bytes
    /// </summary>
    public interface IBuffering<TBuffering, TStream>: IDisposable where TBuffering : struct, IBuffering<TBuffering, TStream>
    {
        /// <summary>
        /// Buffered bytes
        /// </summary>
        public abstract ReadOnlySequence<byte> Bytes { get; }

        /// <summary>
        /// Asynchronously read the next buffered bytes from the stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="maximum">Maximum bytes with internal buffers</param>
        /// <returns></returns>
        public abstract ValueTask<TBuffering> ReadAsync(
            TStream stream,
            CancellationToken cancellationToken,
            bool maximum = true
        );

        /// <summary>
        /// Synchronously read the next buffered bytes from the stream
        /// </summary>
        /// <param name="stream"></param>
        public abstract void Read(TStream stream);

        /// <summary>
        /// Advance buffering by the number of bytes used
        /// </summary>
        /// <param name="bytesUsed"></param>
        public abstract void Advance(long bytesUsed);
    }
}