using System.Buffers.Binary;

namespace Forestry.Deserialize.Xml
{
    /// <summary>
    /// Extensions regioned by type
    /// </summary>
    public static class Extensions
    {
        #region ReadOnlySpan<byte>
        /// <summary>
        /// Pack a raw element name into <paramref name="destination"/>, 8 bytes per ulong,
        /// writing into caller-owned storage rather than allocating - the number of ulongs
        /// packed is however many <paramref name="destination"/> has room for, not a fixed
        /// constant here; #23's "Name packing" cap (32 bytes / 4 ulongs) lives on
        /// <see cref="ElementNameStack"/>'s own pool sizing, not duplicated in this method.
        ///
        /// Names longer than <paramref name="destination"/>'s capacity are truncated - only the
        /// leading bytes are packed and therefore compared later. Per #23's accepted POC
        /// tradeoff, this can wrongly treat two different full names as equal if they are
        /// identical through the cap and only diverge after it; judged unlikely for real
        /// StanForD element names, not eliminated.
        ///
        /// The final chunk, whether it is a genuine partial name-length remainder or padding
        /// past a truncated/short name, is zero-padded - safe because XML names can never
        /// legally contain a NUL byte (excluded from the <c>Char</c> production entirely), so a
        /// padding zero can never be mistaken for real name content.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="destination"></param>
        public static void Pack(this ReadOnlySpan<byte> value, Span<ulong> destination)
        {
            int cappedLength = Math.Min(value.Length, destination.Length * 8);
            ReadOnlySpan<byte> capped = value[..cappedLength];

            Span<byte> lastChunk = stackalloc byte[8]; // hoisted out of the loop - CA2014

            for (int i = 0; i < destination.Length; i++)
            {
                int offset = i * 8;
                int remaining = capped.Length - offset;

                if (remaining >= 8)
                {
                    destination[i] = BinaryPrimitives.ReadUInt64LittleEndian(capped.Slice(offset, 8));
                }
                else if (remaining > 0)
                {
                    capped.Slice(offset, remaining).CopyTo(lastChunk);
                    destination[i] = BinaryPrimitives.ReadUInt64LittleEndian(lastChunk);
                    lastChunk.Clear(); // don't leak this chunk's tail into a later, shorter slot
                }
                else
                {
                    destination[i] = 0; // past the (possibly capped) name entirely - zero padding
                }
            }
        }
        #endregion

        #region ReadOnlySpan<ulong>
        /// <summary>
        /// Reverse of <see cref="Pack"/>: writes <paramref name="packed"/> back out as raw bytes
        /// into <paramref name="destination"/> (which must have room for
        /// <c>packed.Length * 8</c> bytes) and returns the real, trimmed length - not
        /// <paramref name="destination"/>'s full capacity. Trimming works by scanning back past
        /// trailing zero bytes, which is safe for the same reason <see cref="Pack"/>'s own
        /// padding is: XML names can never legally contain a NUL byte, so every trailing zero is
        /// padding (from a short name, or from <see cref="Pack"/>'s cap), never real content.
        /// </summary>
        /// <param name="packed"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        public static int Unpack(this ReadOnlySpan<ulong> packed, Span<byte> destination)
        {
            for (int i = 0; i < packed.Length; i++)
            {
                BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(i * 8, 8), packed[i]);
            }

            ReadOnlySpan<byte> written = destination[..(packed.Length * 8)];
            int length = written.Length;

            while (length > 0 && written[length - 1] == 0)
            {
                length--;
            }

            return length;
        }
        #endregion
    }
}
