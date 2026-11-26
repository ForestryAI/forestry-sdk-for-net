using System.Buffers;

namespace Forestry.Serialization.Writing
{
    /// <summary>
    /// A buffer writer over arrays rented from <see cref="ArrayPool{T}.Shared"/>, living for one serialize
    /// call to a <see cref="Stream"/> (spec 004 research R48): the writer formats into it, and the
    /// serializer copies what was written to the stream, as <c>Utf8JsonWriter</c> does over a stream
    /// </summary>
    internal sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
    {
        /// <summary>
        /// The first array rented
        /// </summary>
        private const int InitialCapacity = 4096;

        private byte[] _buffer = ArrayPool<byte>.Shared.Rent(InitialCapacity);

        private int _written;

        /// <summary>
        /// Bytes written since the last <see cref="Clear"/>
        /// </summary>
        public int WrittenCount => _written;

        public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

        public ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(0, _written);

        /// <summary>
        /// Forget what was written, keeping the rented array
        /// </summary>
        public void Clear() => _written = 0;

        public void Advance(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);

            if (_written + count > _buffer.Length)
            {
                throw new InvalidOperationException("Advanced past the buffer.");
            }

            _written += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return _buffer.AsMemory(_written);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return _buffer.AsSpan(_written);
        }

        public void Dispose()
        {
            byte[] buffer = _buffer;
            _buffer = [];
            _written = 0;

            if (buffer.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Rent a larger array, copying what was written, when fewer than <paramref name="sizeHint"/> bytes
        /// (at least one) are free
        /// </summary>
        /// <param name="sizeHint"></param>
        private void Ensure(int sizeHint)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
            int needed = Math.Max(sizeHint, 1);

            if (_buffer.Length - _written >= needed)
            {
                return;
            }

            int capacity = Math.Max(_buffer.Length * 2, _written + needed);
            byte[] larger = ArrayPool<byte>.Shared.Rent(capacity);
            _buffer.AsSpan(0, _written).CopyTo(larger);

            if (_buffer.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
            }

            _buffer = larger;
        }
    }
}
