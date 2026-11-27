using System.Text;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Xml.Converters
{
    /// <summary>
    /// XML default converter for <see cref="TimeSpan"/>: an XML Schema duration without years or months, e.g.
    /// <c>PT1H30M</c>, <c>P2DT3H</c>, <c>-PT0.5S</c> (spec 005 research R64, FR-003a)
    /// </summary>
    /// <remarks>
    /// <para>
    /// Grammar: an optional <c>-</c>, <c>P</c>, an optional <c>nD</c>, then an optional <c>T</c> followed by
    /// at least one of <c>nH</c>, <c>nM</c>, <c>n[.n]S</c> in that order; at least one part. Years and months
    /// (<c>Y</c>, or <c>M</c> before <c>T</c>) have no exact length and fail, where
    /// <c>XmlConvert.ToTimeSpan</c> approximates them.
    /// </para>
    /// <para>
    /// Exact to the tick: a fraction of a second finer than 100 nanoseconds fails rather than being rounded,
    /// and so does a duration beyond <see cref="TimeSpan"/>'s range. Writing uses the writer's form for
    /// <see cref="TimeSpan"/>.
    /// </para>
    /// </remarks>
    internal sealed class XmlTimeSpanConverter : Converter<TimeSpan>
    {
        /// <summary>
        /// The longest text shown in a refusal
        /// </summary>
        private const int MaxShownBytes = 64;

        public override TimeSpan Read(ReadOnlySpan<byte> utf8)
        {
            if (TryParse(utf8, out TimeSpan value))
            {
                return value;
            }

            throw new FormatException(Serialization.Formatting.Format(Formatting.DurationNotParsable, Encoding.UTF8.GetString(utf8[..Math.Min(utf8.Length, MaxShownBytes)])));
        }

        public override void Write(TimeSpan value, TelemetryWriter writer) => writer.WriteValue(value);

        /// <summary>
        /// Parse an XML Schema duration without years or months into ticks
        /// </summary>
        internal static bool TryParse(ReadOnlySpan<byte> utf8, out TimeSpan value)
        {
            value = default;
            int position = 0;
            bool negative = utf8.Length > 0 && utf8[0] == (byte)'-';

            if (negative)
            {
                position++;
            }

            if (position == utf8.Length || utf8[position++] != (byte)'P')
            {
                return false;
            }

            long ticks = 0;
            bool anyPart = false;
            bool time = false;
            bool partAfterTime = false;

            // Designators in their only order: D, then T, then H, M, S; each at most once
            int next = 0;
            ReadOnlySpan<byte> order = "DHMS"u8;

            try
            {
                while (position < utf8.Length)
                {
                    if (utf8[position] == (byte)'T')
                    {
                        if (time)
                        {
                            return false;
                        }

                        time = true;
                        next = Math.Max(next, 1);
                        position++;
                        continue;
                    }

                    if (!TryReadNumber(utf8, ref position, out long whole, out long fractionTicks, out bool hasFraction) || position == utf8.Length)
                    {
                        return false;
                    }

                    byte designator = utf8[position++];
                    int index = order.IndexOf(designator);

                    // Y anywhere, M before T: years and months; D after T or H/M/S before T: out of place
                    if (index < next || (index == 0) == time || (hasFraction && designator != (byte)'S'))
                    {
                        return false;
                    }

                    next = index + 1;
                    anyPart = true;
                    partAfterTime |= time;

                    ticks = checked(ticks + designator switch
                    {
                        (byte)'D' => whole * TimeSpan.TicksPerDay,
                        (byte)'H' => whole * TimeSpan.TicksPerHour,
                        (byte)'M' => whole * TimeSpan.TicksPerMinute,
                        _ => whole * TimeSpan.TicksPerSecond + fractionTicks
                    });
                }
            }
            catch (OverflowException)
            {
                return false;
            }

            if (!anyPart || (time && !partAfterTime))
            {
                return false;
            }

            value = TimeSpan.FromTicks(negative ? -ticks : ticks);
            return true;
        }

        /// <summary>
        /// Digits, and for seconds an optional <c>.</c> with at least one digit, exact to the tick
        /// </summary>
        private static bool TryReadNumber(ReadOnlySpan<byte> utf8, ref int position, out long whole, out long fractionTicks, out bool hasFraction)
        {
            whole = 0;
            fractionTicks = 0;
            hasFraction = false;
            int start = position;

            while (position < utf8.Length && char.IsAsciiDigit((char)utf8[position]))
            {
                whole = checked(whole * 10 + (utf8[position++] - (byte)'0'));
            }

            if (position == start)
            {
                return false;
            }

            if (position < utf8.Length && utf8[position] == (byte)'.')
            {
                hasFraction = true;
                int fractionStart = ++position;
                long scale = TimeSpan.TicksPerSecond;

                while (position < utf8.Length && char.IsAsciiDigit((char)utf8[position]))
                {
                    int digit = utf8[position++] - (byte)'0';
                    scale /= 10;

                    if (scale == 0)
                    {
                        // Finer than a tick: only zeros keep the duration exact
                        if (digit != 0)
                        {
                            return false;
                        }

                        continue;
                    }

                    fractionTicks += digit * scale;
                }

                if (position == fractionStart)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
