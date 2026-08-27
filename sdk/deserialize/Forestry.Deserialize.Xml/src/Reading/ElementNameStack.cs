using System.Diagnostics.CodeAnalysis;
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
    internal struct ElementNameStack
    {
        /// <summary>
        /// How many ulongs a single packed name occupies - 32 bytes, per #23's accepted POC
        /// tradeoff (names longer than this are capped, compared only up to this length).
        /// </summary>
        internal const int PackedNameLength = 4;

        internal const int NonAllocatingMaxDepth = 64;

        /// <summary>
        /// Fixed number of raw ulong slots living inline in this struct - no separate heap
        /// allocation for the pool itself, safe even for a `default`-initialized
        /// <see cref="ElementNameStack"/> (unlike a plain array field, which would come back
        /// null from default-initialization and only get allocated through an explicit
        /// constructor nothing currently calls).
        /// </summary>
        [InlineArray(NonAllocatingMaxDepth * PackedNameLength)]
        internal struct NonAllocatingPool
        {
            private ulong _element;
        }

        private int _depth;

        private NonAllocatingPool _nonAllocatingArray;

        public readonly int Depth => _depth;

        /// <summary>
        /// Push a raw element name onto the stack. Packs directly into the pool slot for the
        /// current depth (or the allocating fallback beyond <see cref="NonAllocatingMaxDepth"/>) -
        /// no caller-side allocation in the common path.
        /// </summary>
        /// <param name="name"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(ReadOnlySpan<byte> name)
        {
            if (_depth < NonAllocatingMaxDepth)
            {
                Span<ulong> pool = _nonAllocatingArray;
                name.Pack(pool.Slice(_depth * PackedNameLength, PackedNameLength));
            }
            else
            {
                PushAllocating(name);
            }

            _depth++;
        }

        /// <summary>
        /// Pop the most recently pushed name. Returns a view into the pool slot, not a copy -
        /// the caller only ever needs to compare it against a freshly packed closing-tag name,
        /// never keep it around past that one comparison.
        /// </summary>
        /// <returns></returns>
        [UnscopedRef]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<ulong> Pop()
        {
            _depth--;

            if (_depth < NonAllocatingMaxDepth)
            {
                Span<ulong> pool = _nonAllocatingArray;
                return pool.Slice(_depth * PackedNameLength, PackedNameLength);
            }

            return PopAllocating();
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
        /// Fallback for depth beyond <see cref="NonAllocatingMaxDepth"/> - not yet built.
        /// </summary>
        /// <returns></returns>
        private readonly ReadOnlySpan<ulong> PopAllocating()
        {
            throw new NotImplementedException();
        }
    }
}
