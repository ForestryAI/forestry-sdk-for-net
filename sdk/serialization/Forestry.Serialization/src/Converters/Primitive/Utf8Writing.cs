using System.Buffers;
using System.Globalization;

namespace Forestry.Serialization.Converters.Primitive
{
    /// <summary>
    /// Writes a value that formats itself to UTF-8 into a buffer writer, asking for more room until it
    /// fits (spec 004 research R45): no "did not fit" result reaches the caller
    /// </summary>
    internal static class Utf8Writing
    {
        /// <summary>
        /// The first size hint; most single values (numbers, <see cref="Guid"/>, round-trip dates) fit
        /// </summary>
        private const int FirstHint = 64;

        /// <summary>
        /// Format <paramref name="value"/> with <paramref name="format"/> and the invariant culture,
        /// doubling the size hint while the room offered is too small
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="format"></param>
        /// <param name="writer"></param>
        public static void Write<T>(T value, ReadOnlySpan<char> format, IBufferWriter<byte> writer) where T : IUtf8SpanFormattable
        {
            int hint = FirstHint;

            while (true)
            {
                Span<byte> span = writer.GetSpan(hint);

                if (value.TryFormat(span, out int written, format, CultureInfo.InvariantCulture))
                {
                    writer.Advance(written);
                    return;
                }

                hint = checked(Math.Max(hint, span.Length) * 2);
            }
        }
    }
}
