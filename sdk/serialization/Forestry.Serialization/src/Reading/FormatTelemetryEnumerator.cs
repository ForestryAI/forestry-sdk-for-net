using System.Buffers;
using System.Collections;
using System.Reflection;

namespace Forestry.Serialization.Reading
{
    /// <summary>
    /// Synchronous telemetry enumerator over UTF-8 bytes in memory. The core writes the moves; a format derives
    /// from it and overrides <see cref="Read"/>, driving its own reader and the core's telemetry builder
    /// (research R2). One telemetry per move
    /// </summary>
    public abstract class FormatTelemetryEnumerator : IEnumerator<Telemetry>
    {
        /// <summary>
        /// Telemetry enumerator over a whole document in memory
        /// </summary>
        /// <param name="utf8"></param>
        /// <param name="options"></param>
        protected FormatTelemetryEnumerator(ReadOnlyMemory<byte> utf8, DeserializeOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _remaining = new ReadOnlySequence<byte>(utf8);
            Builder = new TelemetryBuilder(options);
        }

        private ReadOnlySequence<byte> _remaining;

        private bool _isDone;

        /// <summary>
        /// The core's telemetry builder for this deserialization, for a format's shared reading class
        /// </summary>
        protected TelemetryBuilder Builder { get; }

        /// <summary>
        /// Current telemetry
        /// </summary>
        public Telemetry Current { get; private set; } = null!;

        object IEnumerator.Current => Current;

        /// <summary>
        /// Read from the unconsumed bytes until the core completed a telemetry, or nothing is left
        /// </summary>
        /// <param name="bytes">Unconsumed bytes</param>
        /// <param name="isFinalBlock">No more bytes follow</param>
        /// <param name="telemetry">The telemetry completed during the call</param>
        /// <param name="consumed">Bytes consumed, reported even when 0</param>
        /// <returns></returns>
        protected abstract ReadStatus Read(ReadOnlySequence<byte> bytes, bool isFinalBlock, out Telemetry? telemetry, out long consumed);

        /// <summary>
        /// A telemetry of type <typeparamref name="T"/> starts
        /// </summary>
        /// <typeparam name="T"></typeparam>
        protected void Start<T>() where T : Telemetry => Builder.Start<T>();

        /// <summary>
        /// A property of the top open telemetry has this UTF-8 value
        /// </summary>
        /// <param name="property"></param>
        /// <param name="utf8"></param>
        /// <param name="information"></param>
        protected void Set(PropertyInfo property, ReadOnlySpan<byte> utf8, in ReadingInformation information) =>
            Builder.Set(property, utf8, in information);

        /// <summary>
        /// A field of the top open telemetry has this UTF-8 value
        /// </summary>
        /// <param name="field"></param>
        /// <param name="utf8"></param>
        /// <param name="information"></param>
        protected void Set(FieldInfo field, ReadOnlySpan<byte> utf8, in ReadingInformation information) =>
            Builder.Set(field, utf8, in information);

        /// <summary>
        /// The top open telemetry is complete
        /// </summary>
        /// <returns>The telemetry to hand out, or null</returns>
        protected Telemetry? Complete() => Builder.Complete();

        /// <summary>
        /// Move to the next telemetry
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            while (!_isDone)
            {
                ReadStatus state = Read(_remaining, isFinalBlock: true, out Telemetry? telemetry, out long consumed);
                _remaining = _remaining.Slice(consumed);

                switch (state)
                {
                    case ReadStatus.Complete when telemetry is not null:
                        Current = telemetry;
                        return true;
                    case ReadStatus.Complete:
                        continue;  // a telemetry only a constructor needs: not handed out
                    case ReadStatus.None:
                        _isDone = true;
                        Builder.Clear();  // the enumeration ended: its policies are cleared now
                        break;
                    default:
                        Throwing.WhenPartialAtFinalBlock();
                        break;
                }
            }

            Current = null!;
            return false;
        }

        /// <summary>
        /// Not supported: the input is read forward only (C2)
        /// </summary>
        public void Reset() => throw new NotSupportedException();

        /// <summary>
        /// Release the state; telemetries still open are never handed out
        /// </summary>
        public void Dispose()
        {
            _isDone = true;
            _remaining = default;
            Builder.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
