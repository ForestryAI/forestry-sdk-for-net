using System.Runtime.CompilerServices;

namespace Forestry.Deserialize.Xml.Reading
{
    /// <summary>
    /// Stack of packed element names, used to assert the Element Type Match WFC (an ending tag's
    /// name must match its starting tag's) once content is more than one level deep. Up to
    /// <see cref="NonAllocatingMaxDepth"/> levels are stored inline, in <see cref="_nonAllocatingArray"/> -
    /// no allocation per push. Deeper nesting falls back to <see cref="PushAllocating"/> (not yet
    /// built - depth this deep is not expected for real StanForD data).
    /// </summary>
    internal struct ElementStack
    {
        /// <summary>
        /// How many ulongs a single packed name occupies - 32 bytes, per #23's accepted POC
        /// tradeoff (names longer than this are capped, compared only up to this length).
        /// </summary>
        internal const int PackedNameLength = 4;

        internal const int NonAllocatingMaxDepth = 64;

        private NonAllocatingPool _nonAllocatingArray;

        /// <summary>
        /// Fixed number of raw ulong slots living inline in this struct - no separate heap
        /// allocation for the pool itself, safe even for a `default`-initialized
        /// <see cref="ElementStack"/> (unlike a plain array field, which would come back
        /// null from default-initialization and only get allocated through an explicit
        /// constructor nothing currently calls).
        /// </summary>
        [InlineArray(NonAllocatingMaxDepth * PackedNameLength)]
        internal struct NonAllocatingPool
        {
            private ulong _element;
        }

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
        /// Push a raw element name onto the stack. Packs directly into the pool slot for the
        /// current depth (or the allocating fallback beyond <see cref="NonAllocatingMaxDepth"/>) -
        /// no caller-side allocation in the common path.
        /// </summary>
        /// <param name="name"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(ReadOnlySpan<byte> name)
        {
            if (_depth == 0 && !_rootElement)
            {
                _rootElement = true;
            }

            if (_depth < NonAllocatingMaxDepth)
            {
                Span<ulong> pool = _nonAllocatingArray;
                name.Pack(pool.Slice(_depth * PackedNameLength, PackedNameLength));
            }
            else
            {
                PushAllocating(name);
            }

            _contentReady = false;  // unreliable if a content non-terminal exists after a start tag
            _depth++;
        }

        /// <summary>
        /// Try pop the most recently pushed name, matching it against <paramref name="name"/> -
        /// the Element Type Match WFC (an ending tag's name must match its starting tag's).
        /// Peeks before mutating anything: packs <paramref name="name"/> and compares it against
        /// the tail slot first, only decrementing <see cref="Depth"/> on an actual match - same
        /// "peek, don't mutate on failure" contract as <see cref="Utf8Reader.TryMatch"/>/
        /// <see cref="Utf8Reader.TrySkip"/>. Returns <see langword="false"/>, never throws, for
        /// either failure case: nothing to pop, or a name that doesn't match - the caller decides
        /// what a mismatch means (a thrown malformed-document error), not this method.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPop(ReadOnlySpan<byte> name)
        {
            if (_depth == 0)
            {
                return false;
            }

            int tailIndex = _depth - 1;

            Span<ulong> packedName = stackalloc ulong[PackedNameLength];
            name.Pack(packedName);

            ReadOnlySpan<ulong> tail = tailIndex < NonAllocatingMaxDepth
                ? ((Span<ulong>)_nonAllocatingArray).Slice(tailIndex * PackedNameLength, PackedNameLength)
                : PeekAllocating();

            if (!tail.SequenceEqual(packedName))
            {
                return false;
            }

            _contentReady = true;  // unreliable if a content non-terminal exists before a end tag e.g. character data
            _depth = tailIndex;
            return true;
        }

        /// <summary>
        /// Pop the most recently pushed name unconditionally - no comparison, unlike
        /// <see cref="TryPop"/>. For a case where the caller already knows something is open and
        /// is closing exactly that (e.g. an empty element's own '/&gt;' immediately after its
        /// name was pushed) - there's nothing to validate, so nothing to fail gracefully on.
        /// Calling this with nothing open is a caller bug, not a document condition, so unlike
        /// every other method on this type it throws rather than returning something misleading -
        /// this method comes with that responsibility. Unpacks the popped name back into raw
        /// bytes (see <see cref="Extensions.Unpack"/>) into caller-owned <paramref name="destination"/>
        /// (needs room for <see cref="PackedNameLength"/> * 8 bytes) and returns its real,
        /// trimmed length - a plain value copy into storage the caller already owns, rather than
        /// returning a span into this stack's own storage, which a caller elsewhere (e.g.
        /// <c>Utf8XmlReader.Value</c>) couldn't safely hold onto past the call.
        /// </summary>
        /// <param name="destination"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Pop(Span<byte> destination)
        {
            if (_depth == 0)
            {
                throw new InvalidOperationException(); // TODO: formatting - caller responsibility violated
            }

            _contentReady = true;  // unreliable if a content non-terminal exists before a end tag e.g. character data
            _depth--;

            ReadOnlySpan<ulong> tail = _depth < NonAllocatingMaxDepth
                ? ((Span<ulong>)_nonAllocatingArray).Slice(_depth * PackedNameLength, PackedNameLength)
                : PeekAllocating();

            return tail.Unpack(destination);
        }

        /// <summary>
        /// Fallback for depth beyond <see cref="NonAllocatingMaxDepth"/> - not yet built.
        /// Deliberately throws rather than silently doing nothing, since nesting this deep
        /// wouldn't be a caller mistake, it just isn't supported yet.
        /// </summary>
        /// <param name="name"></param>
        private readonly void PushAllocating(ReadOnlySpan<byte> name)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Fallback for depth beyond <see cref="NonAllocatingMaxDepth"/> - not yet built. Peeks
        /// (does not remove) the tail slot, matching <see cref="TryPop"/>'s own peek-before-mutate
        /// contract for the non-allocating path.
        /// </summary>
        /// <returns></returns>
        private readonly ReadOnlySpan<ulong> PeekAllocating()
        {
            throw new NotImplementedException();
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
