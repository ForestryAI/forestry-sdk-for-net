using System.Buffers;
using System.Runtime.CompilerServices;

namespace Forestry.Xml.Reading
{
    /// <summary>
    /// Stack of open element names, used to assert the Element Type Match WFC (an ending tag's
    /// name must match its starting tag's) in full (spec 001 research R4). The names are laid end
    /// to end in <see cref="_names"/> with the end offset of each in <see cref="_ends"/>, all
    /// inline - no allocation per push, and about 2 KB in the reader state. Inside
    /// <c>&lt;ab&gt;&lt;Log&gt;&lt;LogDiameter&gt;</c>:
    /// <code>
    /// _names: "ab" "Log" "LogDiameter"
    /// _ends:   2    5     16
    /// _depth:  3
    /// </code>
    /// </summary>
    internal struct ElementStack
    {
        /// <summary>
        /// Open elements the stack holds
        /// </summary>
        internal const int NonAllocatingMaxDepth = 64;

        /// <summary>
        /// Bytes of open names the stack holds, the same 2 KB as the 32-byte packed names before
        /// </summary>
        internal const int NameBytesCapacity = NonAllocatingMaxDepth * 32;

        /// <summary>
        /// The open names' bytes laid end to end
        /// </summary>
        [InlineArray(NameBytesCapacity)]
        internal struct Names
        {
            private byte _element;
        }

        /// <summary>
        /// The end offset of each open name in <see cref="_names"/>
        /// </summary>
        [InlineArray(NonAllocatingMaxDepth)]
        internal struct Ends
        {
            private ushort _element;
        }

        private Names _names;

        private Ends _ends;

        private bool _rootElement;

        /// <summary>
        /// Root element in the XML document
        /// </summary>
        public readonly bool RootElement => _rootElement;

        private int _depth;

        public readonly int Depth => _depth;

        private bool _contentReady;

        /// <summary>
        /// After an element non-terminal start tag is a content non-terminal
        /// </summary>
        internal readonly bool ContentReady => _contentReady;

        /// <summary>
        /// Where the top name starts in <see cref="_names"/>, 0 when one or none is open
        /// </summary>
        private readonly int TopStart => _depth > 1 ? _ends[_depth - 2] : 0;

        /// <summary>
        /// Where the next name starts in <see cref="_names"/>
        /// </summary>
        private readonly int End => _depth > 0 ? _ends[_depth - 1] : 0;

        /// <summary>
        /// Length of the top name, the end tag's budget (FR-011); 0 when nothing is open
        /// </summary>
        public readonly int TopLength => End - TopStart;

        /// <summary>
        /// A name of <paramref name="length"/> bytes fits: fewer than
        /// <see cref="NonAllocatingMaxDepth"/> names are open and the bytes stay within
        /// <see cref="NameBytesCapacity"/>
        /// </summary>
        /// <param name="length"></param>
        public readonly bool CanPush(long length) =>
            _depth < NonAllocatingMaxDepth && End + length <= NameBytesCapacity;

        /// <summary>
        /// Push a whole element name, copied straight into the pool; false and nothing changed
        /// when it does not fit (the bytes would pass <see cref="NameBytesCapacity"/>, or
        /// <see cref="NonAllocatingMaxDepth"/> names are open)
        /// </summary>
        /// <param name="name"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPush(ReadOnlySpan<byte> name)
        {
            int end = End;
            if (_depth == NonAllocatingMaxDepth || end + name.Length > NameBytesCapacity)
            {
                return false;
            }

            name.CopyTo(((Span<byte>)_names)[end..]);
            Pushed(end + name.Length);
            return true;
        }

        /// <summary>
        /// Push a whole element name straddling segments, copied straight into the pool; false and
        /// nothing changed when it does not fit
        /// </summary>
        /// <param name="name"></param>
        public bool TryPush(ReadOnlySequence<byte> name)
        {
            int end = End;
            if (_depth == NonAllocatingMaxDepth || end + name.Length > NameBytesCapacity)
            {
                return false;
            }

            name.CopyTo(((Span<byte>)_names)[end..]);
            Pushed(end + (int)name.Length);
            return true;
        }

        private void Pushed(int end)
        {
            if (_depth == 0 && !_rootElement)
            {
                _rootElement = true;
            }

            _ends[_depth] = (ushort)end;
            _contentReady = false;  // unreliable if a content non-terminal exists after a start tag
            _depth++;
        }

        /// <summary>
        /// The top name equals <paramref name="name"/> in full: lengths first, so an end tag
        /// never compares past the start-tag name, then the bytes
        /// </summary>
        /// <param name="name"></param>
        /// <param name="tailIndex"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryPeek(ReadOnlySpan<byte> name, out int tailIndex)
        {
            tailIndex = _depth - 1;

            if (_depth == 0 || name.Length != TopLength)
            {
                return false;
            }

            ReadOnlySpan<byte> names = _names;
            return names[TopStart..End].SequenceEqual(name);
        }

        /// <summary>
        /// The top name equals <paramref name="name"/>, straddling segments, in full and without
        /// copying it
        /// </summary>
        /// <param name="name"></param>
        /// <param name="tailIndex"></param>
        /// <returns></returns>
        public readonly bool TryPeek(ReadOnlySequence<byte> name, out int tailIndex)
        {
            tailIndex = _depth - 1;

            if (_depth == 0 || name.Length != TopLength)
            {
                return false;
            }

            ReadOnlySpan<byte> names = _names;
            SequenceReader<byte> reader = new(name);
            return reader.IsNext(names[TopStart..End], advancePast: false);
        }

        /// <summary>
        /// Try pop the most recently pushed name, matching it against <paramref name="name"/> -
        /// the Element Type Match WFC (an ending tag's name must match its starting tag's).
        /// Peeks before mutating anything, only decrementing <see cref="Depth"/> on an actual
        /// match - same "peek, don't mutate on failure" contract as <see cref="Utf8Reader.TryMatch"/>/
        /// <see cref="Utf8Reader.TrySkip"/>. Returns <see langword="false"/>, never throws, for
        /// either failure case: nothing to pop, or a name that doesn't match - the caller decides
        /// what a mismatch means (a thrown malformed-document error), not this method.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPop(ReadOnlySpan<byte> name)
        {
            if (!TryPeek(name, out int tailIndex))
            {
                return false;
            }

            _contentReady = true;  // unreliable if a content non-terminal exists before a end tag e.g. character data
            _depth = tailIndex;
            return true;
        }

        /// <summary>
        /// Try pop the most recently pushed name, matching it against a name straddling segments
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool TryPop(ReadOnlySequence<byte> name)
        {
            if (!TryPeek(name, out int tailIndex))
            {
                return false;
            }

            _contentReady = true;
            _depth = tailIndex;
            return true;
        }

        /// <summary>
        /// Pop the most recently pushed name unconditionally - no comparison, unlike
        /// <see cref="TryPop(ReadOnlySpan{byte})"/>. For a case where the caller already knows
        /// something is open and is closing exactly that (e.g. an empty element's own '/&gt;'
        /// immediately after its name was pushed). Calling this with nothing open is a caller bug,
        /// not a document condition, so it throws rather than making the depth negative and
        /// corrupting the top name (R4).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pop()
        {
            if (_depth == 0)
            {
                throw new InvalidOperationException(); // TODO: formatting - caller responsibility violated
            }

            _contentReady = true;  // unreliable if a content non-terminal exists before a end tag e.g. character data
            _depth--;
        }

        /// <summary>
        /// Negates (flips) the content ready flag
        /// </summary>
        internal void NegateContentReady()
        {
            _contentReady = !_contentReady;
        }
    }
}
