using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Forestry.Serialization.Converters
{
    /// <summary>
    /// How an enum value is read and written: by its member's name or by its underlying number
    /// (spec 005 research R59)
    /// </summary>
    public enum EnumForm
    {
        /// <summary>
        /// The member's name: its C# name, or the name declared with <see cref="EnumMemberNameAttribute"/>
        /// </summary>
        Name,

        /// <summary>
        /// The underlying integer
        /// </summary>
        Number
    }

    /// <summary>
    /// One member of an enum, as an enum converter resolved it
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="Value">The member's value</param>
    /// <param name="MemberName">The member's C# name</param>
    /// <param name="Name">The name it is read from and written as: <paramref name="MemberName"/>, or the name
    /// declared with <see cref="EnumMemberNameAttribute"/></param>
    public readonly record struct EnumMember<TEnum>(TEnum Value, string MemberName, string Name) where TEnum : struct, Enum;

    /// <summary>
    /// The fields of <typeparamref name="TEnum"/>, resolved once and kept for the process: values, names, UTF-8
    /// names and lookups both ways (research R59, R62)
    /// </summary>
    /// <remarks>
    /// Not resolved in a static constructor: a name clash must fail as an
    /// <see cref="InvalidOperationException"/> when definitions are built, every time, not as a
    /// <see cref="TypeInitializationException"/>.
    /// </remarks>
    /// <typeparam name="TEnum"></typeparam>
    internal sealed class EnumFields<TEnum> where TEnum : struct, Enum
    {
        private static EnumFields<TEnum>? s_instance;

        /// <summary>
        /// The fields of <typeparamref name="TEnum"/>
        /// </summary>
        /// <exception cref="InvalidOperationException">Two members' names are equal ignoring case</exception>
        public static EnumFields<TEnum> Instance => s_instance ??= new EnumFields<TEnum>();

        private readonly Dictionary<string, TEnum> _byName;

        private readonly Dictionary<string, TEnum>.AlternateLookup<ReadOnlySpan<char>> _byNameSpan;

        private readonly Dictionary<TEnum, byte[]> _utf8Names;

        private EnumFields()
        {
            List<EnumMember<TEnum>> members = [];
            _byName = new Dictionary<string, TEnum>(StringComparer.OrdinalIgnoreCase);
            _utf8Names = [];

            foreach (FieldInfo field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                TEnum value = (TEnum)field.GetValue(null)!;
                string name = field.GetCustomAttribute<EnumMemberNameAttribute>()?.Name ?? field.Name;

                if (!_byName.TryAdd(name, value))
                {
                    Throwing.WhenEnumMemberNamesClash(typeof(TEnum), name);
                }

                _utf8Names.TryAdd(value, Encoding.UTF8.GetBytes(name));
                members.Add(new EnumMember<TEnum>(value, field.Name, name));
            }

            Members = members;
            _byNameSpan = _byName.GetAlternateLookup<ReadOnlySpan<char>>();
            IsFlags = typeof(TEnum).IsDefined(typeof(FlagsAttribute), inherit: false);
            UnderlyingType = Type.GetTypeCode(Enum.GetUnderlyingType(typeof(TEnum)));
            FlagMembers = [.. members.Where(member => ToUInt64(member.Value) != 0).OrderByDescending(member => ToUInt64(member.Value))];
        }

        /// <summary>
        /// Every member, in declaration order
        /// </summary>
        public IReadOnlyList<EnumMember<TEnum>> Members { get; }

        /// <summary>
        /// Members with a value other than zero, largest first: the parts a flags combination is written as
        /// </summary>
        public IReadOnlyList<EnumMember<TEnum>> FlagMembers { get; }

        /// <summary>
        /// Whether the enum is marked <see cref="FlagsAttribute"/>
        /// </summary>
        public bool IsFlags { get; }

        /// <summary>
        /// The underlying integer type
        /// </summary>
        public TypeCode UnderlyingType { get; }

        /// <summary>
        /// The member named <paramref name="name"/>, ignoring case
        /// </summary>
        public bool TryGetValue(ReadOnlySpan<char> name, out TEnum value) => _byNameSpan.TryGetValue(name, out value);

        /// <summary>
        /// The UTF-8 name of a defined value
        /// </summary>
        public bool TryGetUtf8Name(TEnum value, out ReadOnlySpan<byte> utf8Name)
        {
            if (_utf8Names.TryGetValue(value, out byte[]? name))
            {
                utf8Name = name;
                return true;
            }

            utf8Name = default;
            return false;
        }

        /// <summary>
        /// The bits of <paramref name="value"/>, whatever its underlying type, zero-extended (for flags arithmetic)
        /// </summary>
        public static ulong ToUInt64(TEnum value) => Unsafe.SizeOf<TEnum>() switch
        {
            1 => Unsafe.As<TEnum, byte>(ref value),
            2 => Unsafe.As<TEnum, ushort>(ref value),
            4 => Unsafe.As<TEnum, uint>(ref value),
            _ => Unsafe.As<TEnum, ulong>(ref value)
        };

        /// <summary>
        /// The enum value with the bits of <paramref name="bits"/>, truncated to its underlying type
        /// </summary>
        public static TEnum FromUInt64(ulong bits)
        {
            switch (Unsafe.SizeOf<TEnum>())
            {
                case 1:
                    byte b = (byte)bits;
                    return Unsafe.As<byte, TEnum>(ref b);
                case 2:
                    ushort s = (ushort)bits;
                    return Unsafe.As<ushort, TEnum>(ref s);
                case 4:
                    uint i = (uint)bits;
                    return Unsafe.As<uint, TEnum>(ref i);
                default:
                    return Unsafe.As<ulong, TEnum>(ref bits);
            }
        }
    }
}
