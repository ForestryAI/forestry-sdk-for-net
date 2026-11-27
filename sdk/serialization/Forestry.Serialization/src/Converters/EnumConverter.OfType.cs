using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Converters
{
    /// <summary>
    /// The base of every enum converter: resolves the enum's fields once (values, names, UTF-8 names and
    /// lookups both ways) and reads and writes one <see cref="EnumForm"/>, given when it is created (spec 005
    /// research R59)
    /// </summary>
    /// <remarks>
    /// <para>
    /// The form is never guessed from the text: XML has no string or number token, so a format declares the
    /// form instead (e.g. an attribute on the telemetry member), as System.Text.Json's enum converter is told
    /// by its options which forms to allow.
    /// </para>
    /// <para>
    /// <see cref="EnumForm.Name"/> reads a member's name ignoring case and writes it through
    /// <see cref="TelemetryWriter.WriteEnumName"/>; a <see cref="FlagsAttribute"/> enum also reads and writes
    /// combinations as names joined with <c>", "</c>. A value with no name is refused when written.
    /// <see cref="EnumForm.Number"/> reads the underlying integer and writes it through the writer's integer
    /// overloads. A member's name is its C# name unless <see cref="EnumMemberNameAttribute"/> gives another.
    /// </para>
    /// <para>
    /// Subclasses (a format's enum converter, or a developer's) may override <see cref="Read"/>, e.g. to also
    /// accept texts without spaces, using <see cref="Members"/> and <see cref="TryGetValue"/>.
    /// </para>
    /// </remarks>
    /// <typeparam name="TEnum"></typeparam>
    public abstract class EnumConverter<TEnum> : Converter<TEnum> where TEnum : struct, Enum
    {
        /// <summary>
        /// Names longer than this are decoded into a rented buffer instead of the stack
        /// </summary>
        private const int StackChars = 256;

        private readonly EnumFields<TEnum> _fields;

        /// <summary>
        /// An enum converter reading and writing <paramref name="form"/>
        /// </summary>
        /// <param name="form"></param>
        /// <exception cref="InvalidOperationException">Two members' names are equal ignoring case</exception>
        protected EnumConverter(EnumForm form)
        {
            Form = form;
            _fields = EnumFields<TEnum>.Instance;
        }

        /// <summary>
        /// The form read and written
        /// </summary>
        public EnumForm Form { get; }

        /// <summary>
        /// Every member of <typeparamref name="TEnum"/>, in declaration order
        /// </summary>
        protected static IReadOnlyList<EnumMember<TEnum>> Members => EnumFields<TEnum>.Instance.Members;

        /// <summary>
        /// The member named <paramref name="name"/>, ignoring case
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected static bool TryGetValue(ReadOnlySpan<char> name, out TEnum value) => EnumFields<TEnum>.Instance.TryGetValue(name, out value);

        /// <summary>
        /// The UTF-8 name of a defined value
        /// </summary>
        /// <param name="value"></param>
        /// <param name="utf8Name"></param>
        /// <returns></returns>
        protected static bool TryGetUtf8Name(TEnum value, out ReadOnlySpan<byte> utf8Name) => EnumFields<TEnum>.Instance.TryGetUtf8Name(value, out utf8Name);

        /// <inheritdoc/>
        /// <exception cref="FormatException">The text is not a name (in name form) or not a number of the
        /// underlying type (in number form)</exception>
        public override TEnum Read(ReadOnlySpan<byte> utf8) => Form == EnumForm.Number ? ReadNumber(utf8) : ReadName(utf8);

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">In name form, a value that is neither a member nor (for a
        /// flags enum) a combination of members</exception>
        public override void Write(TEnum value, TelemetryWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);

            if (Form == EnumForm.Number)
            {
                WriteNumber(value, writer);
            }
            else
            {
                WriteName(value, writer);
            }
        }

        private TEnum ReadName(ReadOnlySpan<byte> utf8)
        {
            char[]? rented = null;
            int maxChars = Encoding.UTF8.GetMaxCharCount(utf8.Length);
            Span<char> chars = maxChars <= StackChars ? stackalloc char[StackChars] : (rented = ArrayPool<char>.Shared.Rent(maxChars));

            try
            {
                ReadOnlySpan<char> text = chars[..Encoding.UTF8.GetChars(utf8, chars)];

                if (_fields.TryGetValue(text, out TEnum value))
                {
                    return value;
                }

                if (_fields.IsFlags && text.Contains(',') && TryReadCombination(text, out value))
                {
                    return value;
                }

                throw new FormatException(Formatting.Format(Formatting.EnumNameNotParsable, text.ToString(), typeof(TEnum).Name));
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<char>.Shared.Return(rented);
                }
            }
        }

        private bool TryReadCombination(ReadOnlySpan<char> text, out TEnum value)
        {
            ulong bits = 0;

            foreach (Range range in text.Split(','))
            {
                if (!_fields.TryGetValue(text[range].Trim(), out TEnum part))
                {
                    value = default;
                    return false;
                }

                bits |= EnumFields<TEnum>.ToUInt64(part);
            }

            value = EnumFields<TEnum>.FromUInt64(bits);
            return true;
        }

        private TEnum ReadNumber(ReadOnlySpan<byte> utf8)
        {
            TEnum value;
            bool parsed = _fields.UnderlyingType switch
            {
                TypeCode.SByte => TryParse<sbyte>(utf8, out value),
                TypeCode.Byte => TryParse<byte>(utf8, out value),
                TypeCode.Int16 => TryParse<short>(utf8, out value),
                TypeCode.UInt16 => TryParse<ushort>(utf8, out value),
                TypeCode.Int32 => TryParse<int>(utf8, out value),
                TypeCode.UInt32 => TryParse<uint>(utf8, out value),
                TypeCode.Int64 => TryParse<long>(utf8, out value),
                _ => TryParse<ulong>(utf8, out value)
            };

            if (!parsed)
            {
                throw new FormatException(Formatting.Format(Formatting.EnumNumberNotParsable, Encoding.UTF8.GetString(utf8), typeof(TEnum).Name));
            }

            return value;
        }

        private static bool TryParse<TNumber>(ReadOnlySpan<byte> utf8, out TEnum value) where TNumber : unmanaged, IBinaryInteger<TNumber>
        {
            if (TNumber.TryParse(utf8, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out TNumber number))
            {
                value = Unsafe.As<TNumber, TEnum>(ref number);
                return true;
            }

            value = default;
            return false;
        }

        private void WriteName(TEnum value, TelemetryWriter writer)
        {
            if (_fields.TryGetUtf8Name(value, out ReadOnlySpan<byte> utf8Name))
            {
                writer.WriteEnumName(utf8Name);
                return;
            }

            if (_fields.IsFlags && TryWriteCombination(value, writer))
            {
                return;
            }

            throw new NotSupportedException(Formatting.Format(Formatting.EnumValueHasNoName, NumberText(value), typeof(TEnum).Name));
        }

        private bool TryWriteCombination(TEnum value, TelemetryWriter writer)
        {
            ulong remaining = EnumFields<TEnum>.ToUInt64(value);
            List<EnumMember<TEnum>> parts = [];

            foreach (EnumMember<TEnum> member in _fields.FlagMembers)
            {
                ulong bits = EnumFields<TEnum>.ToUInt64(member.Value);

                if ((remaining & bits) == bits)
                {
                    parts.Add(member);
                    remaining &= ~bits;
                }
            }

            if (remaining != 0 || parts.Count == 0)
            {
                return false;
            }

            parts.Reverse();
            ArrayBufferWriter<byte> joined = new();

            for (int index = 0; index < parts.Count; index++)
            {
                if (index > 0)
                {
                    joined.Write(", "u8);
                }

                _fields.TryGetUtf8Name(parts[index].Value, out ReadOnlySpan<byte> name);
                joined.Write(name);
            }

            writer.WriteEnumName(joined.WrittenSpan);
            return true;
        }

        private void WriteNumber(TEnum value, TelemetryWriter writer)
        {
            switch (_fields.UnderlyingType)
            {
                case TypeCode.SByte:
                    writer.WriteValue((long)Unsafe.As<TEnum, sbyte>(ref value));
                    break;
                case TypeCode.Int16:
                    writer.WriteValue((long)Unsafe.As<TEnum, short>(ref value));
                    break;
                case TypeCode.Int32:
                    writer.WriteValue((long)Unsafe.As<TEnum, int>(ref value));
                    break;
                case TypeCode.Int64:
                    writer.WriteValue(Unsafe.As<TEnum, long>(ref value));
                    break;
                default:
                    ulong unsigned = EnumFields<TEnum>.ToUInt64(value);

                    if (unsigned <= long.MaxValue)
                    {
                        writer.WriteValue((long)unsigned);
                    }
                    else
                    {
                        writer.WriteValue((decimal)unsigned);
                    }

                    break;
            }
        }

        private string NumberText(TEnum value)
        {
            ArrayBufferWriter<byte> output = new();
            WriteNumber(value, new NumberTextWriter(output));
            return Encoding.UTF8.GetString(output.WrittenSpan);
        }

        /// <summary>
        /// The writer's defaults over a buffer, to name an undefined value in a message
        /// </summary>
        private sealed class NumberTextWriter : TelemetryWriter
        {
            public NumberTextWriter(IBufferWriter<byte> output) => Reset(output);
        }
    }
}
