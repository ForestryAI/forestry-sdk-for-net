using System.Text;
using Forestry.Serialization.Converters;

namespace Forestry.StanForD.Converters {
    /// <summary>
    /// Converts a StanForD text to an enum member: the text without spaces, ignoring case, e.g.
    /// "Other manual" to <see cref="CuttingReason.OtherManual"/>
    /// </summary>
    /// <remarks>
    /// Forestry.Serialization has no enum converter yet; until it does, the StanForD enums the telemetries use
    /// are converted here.
    /// </remarks>
    /// <typeparam name="TEnum"></typeparam>
    internal sealed class StanForDEnumConverter<TEnum> : Converter<TEnum> where TEnum : struct, Enum
    {
        /// <summary>
        /// The enum's member names without spaces, ignoring case
        /// </summary>
        private static readonly Dictionary<string, TEnum> s_members =
            Enum.GetValues<TEnum>().ToDictionary(member => member.ToString(), StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc/>
        /// <exception cref="FormatException">The text names no member of <typeparamref name="TEnum"/></exception>
        public override TEnum Read(ReadOnlySpan<byte> utf8)
        {
            string text = Encoding.UTF8.GetString(utf8).Replace(" ", string.Empty, StringComparison.Ordinal);

            if (s_members.TryGetValue(text, out TEnum member))
            {
                return member;
            }

            throw new FormatException($"'{Encoding.UTF8.GetString(utf8)}' is not a StanForD {typeof(TEnum).Name}.");
        }
    }
}
