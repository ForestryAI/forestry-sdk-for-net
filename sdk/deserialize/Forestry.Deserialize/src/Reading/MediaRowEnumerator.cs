using System.Buffers;
using System.Collections;
using System.Reflection;

namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// Synchronous row enumerator over UTF-8 bytes in memory. The core writes the moves; a media derives
    /// from it and overrides <see cref="Read"/>, driving its own reader and the core's row builder
    /// (research R2). One row per move
    /// </summary>
    public abstract class MediaRowEnumerator : IEnumerator<Row>
    {
        /// <summary>
        /// Row enumerator over a whole document in memory
        /// </summary>
        /// <param name="utf8"></param>
        /// <param name="options"></param>
        protected MediaRowEnumerator(ReadOnlyMemory<byte> utf8, DeserializeOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _remaining = new ReadOnlySequence<byte>(utf8);
            Builder = new RowBuilder(options);
        }

        private ReadOnlySequence<byte> _remaining;

        private bool _isDone;

        /// <summary>
        /// The core's row builder for this deserialization, for a media's shared reading class
        /// </summary>
        protected RowBuilder Builder { get; }

        /// <summary>
        /// Current row
        /// </summary>
        public Row Current { get; private set; } = null!;

        object IEnumerator.Current => Current;

        /// <summary>
        /// Read from the unconsumed bytes until the core completed a row, or nothing is left
        /// </summary>
        /// <param name="bytes">Unconsumed bytes</param>
        /// <param name="isFinalBlock">No more bytes follow</param>
        /// <param name="row">The row completed during the call</param>
        /// <param name="consumed">Bytes consumed, reported even when 0</param>
        /// <returns></returns>
        protected abstract ReadStatus Read(ReadOnlySequence<byte> bytes, bool isFinalBlock, out Row? row, out long consumed);

        /// <summary>
        /// A row of type <typeparamref name="T"/> starts
        /// </summary>
        /// <typeparam name="T"></typeparam>
        protected void Start<T>() where T : Row => Builder.Start<T>();

        /// <summary>
        /// A property of the top open row has this UTF-8 value
        /// </summary>
        /// <param name="property"></param>
        /// <param name="utf8"></param>
        /// <param name="context"></param>
        protected void Set(PropertyInfo property, ReadOnlySpan<byte> utf8, in FieldContext context) =>
            Builder.Set(property, utf8, in context);

        /// <summary>
        /// A field of the top open row has this UTF-8 value
        /// </summary>
        /// <param name="field"></param>
        /// <param name="utf8"></param>
        /// <param name="context"></param>
        protected void Set(FieldInfo field, ReadOnlySpan<byte> utf8, in FieldContext context) =>
            Builder.Set(field, utf8, in context);

        /// <summary>
        /// The top open row is complete
        /// </summary>
        /// <returns>The row to hand out, or null</returns>
        protected Row? Complete() => Builder.Complete();

        /// <summary>
        /// Move to the next row
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            while (!_isDone)
            {
                ReadStatus state = Read(_remaining, isFinalBlock: true, out Row? row, out long consumed);
                _remaining = _remaining.Slice(consumed);

                switch (state)
                {
                    case ReadStatus.Complete when row is not null:
                        Current = row;
                        return true;
                    case ReadStatus.Complete:
                        continue;  // a row only a constructor needs: not handed out
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
        /// Release the state; rows still open are never handed out
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
