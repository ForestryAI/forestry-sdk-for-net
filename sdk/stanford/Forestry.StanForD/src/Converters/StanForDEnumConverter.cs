using System.Text;
using Forestry.Serialization.Converters;

namespace Forestry.StanForD.Converters {
    /// <summary>
    /// Converts a StanForD text to an enum member and back: the member's declared name (e.g. "Other manual",
    /// with <c>[EnumMemberName]</c>) or C# name, ignoring case; when reading, also the text without its
    /// spaces against the C# names, e.g. "OtherManual" (serialization spec 005, research R63)
    /// </summary>
    /// <remarks>
    /// Registered in the options, so it wins over any format's enum converter: StanForD enums are always
    /// written as their StanForD texts, whatever a format's enum attribute says.
    /// </remarks>
    /// <typeparam name="TEnum"></typeparam>
    internal sealed class StanForDEnumConverter<TEnum> : EnumConverter<TEnum> where TEnum : struct, Enum
    {
        /// <summary>
        /// The members by C# name, ignoring case, for texts written without their spaces
        /// </summary>
        private static readonly Dictionary<string, TEnum> s_byMemberName =
            Members.ToDictionary(member => member.MemberName, member => member.Value, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// A converter reading and writing StanForD texts
        /// </summary>
        public StanForDEnumConverter() : base(EnumForm.Name)
        {
        }

        /// <inheritdoc/>
        /// <exception cref="FormatException">The text names no member of <typeparamref name="TEnum"/></exception>
        public override TEnum Read(ReadOnlySpan<byte> utf8)
        {
            string text = Encoding.UTF8.GetString(utf8);

            if (TryGetValue(text, out TEnum member) || s_byMemberName.TryGetValue(text.Replace(" ", string.Empty, StringComparison.Ordinal), out member))
            {
                return member;
            }

            throw new FormatException($"'{text}' is not a StanForD {typeof(TEnum).Name}.");
        }
    }
}
