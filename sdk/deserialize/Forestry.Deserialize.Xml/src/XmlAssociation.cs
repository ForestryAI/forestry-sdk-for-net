using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Xml
{
    /// <summary>
    /// Which element means which row type and which member, built once per options when they are
    /// locked; reflection only then (research R6, R8, R13 A: by name, anywhere). Lookups take the
    /// element name as UTF-8 bytes, without allocating
    /// </summary>
    internal sealed class XmlAssociation
    {
        private XmlAssociation(Dictionary<byte[], RowAssociation> rows)
        {
            _rows = rows.GetAlternateLookup<ReadOnlySpan<byte>>();
        }

        private readonly Dictionary<byte[], RowAssociation>.AlternateLookup<ReadOnlySpan<byte>> _rows;

        /// <summary>
        /// Row type whose occurrences have this element name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="row"></param>
        /// <returns></returns>
        public bool TryGetRow(ReadOnlySpan<byte> name, [NotNullWhen(true)] out RowAssociation? row) => _rows.TryGetValue(name, out row);

        /// <summary>
        /// Associate the row types added to <paramref name="options"/>, which must be read-only
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public static XmlAssociation Create(DeserializeOptions options)
        {
            MethodInfo start = typeof(RowPipelines).GetMethod(nameof(RowPipelines.Pipeline), genericParameterCount: 1, Type.EmptyTypes)!;
            Dictionary<byte[], RowAssociation> rows = new(Utf8NameComparer.Instance);

            foreach (Type type in options.Rows)
            {
                TypeDefinition typeDefinition = options.ThrowingGetTypeDefinition(type);
                Dictionary<byte[], MemberAssociation> members = new(Utf8NameComparer.Instance);

                foreach (PropertyDefinition property in typeDefinition.Properties)
                {
                    string memberName = property.Member.GetCustomAttribute<FieldAttribute>(inherit: false)?.Name is { Length: > 0 } fieldName ? fieldName : property.Member.Name;
                    members[Encoding.UTF8.GetBytes(memberName)] = new MemberAssociation(property.Member);
                }

                string rowName = type.GetCustomAttribute<RowAttribute>(inherit: false)?.Name is { Length: > 0 } name ? name : type.Name;
                Action<RowPipelines> startRow = start.MakeGenericMethod(type).CreateDelegate<Action<RowPipelines>>();

                rows[Encoding.UTF8.GetBytes(rowName)] = new RowAssociation(type, startRow, members);
            }

            return new XmlAssociation(rows);
        }

        /// <summary>
        /// A row type's element: start a row of it, find its members by element name
        /// </summary>
        internal sealed class RowAssociation
        {
            public RowAssociation(Type type, Action<RowPipelines> start, Dictionary<byte[], MemberAssociation> members)
            {
                Type = type;
                Start = start;
                _members = members.GetAlternateLookup<ReadOnlySpan<byte>>();
            }

            /// <summary>
            /// The row type
            /// </summary>
            public Type Type { get; }

            /// <summary>
            /// Calls <c>Pipeline&lt;T&gt;()</c> for the row type, bound once
            /// </summary>
            public Action<RowPipelines> Start { get; }

            private readonly Dictionary<byte[], MemberAssociation>.AlternateLookup<ReadOnlySpan<byte>> _members;

            /// <summary>
            /// Member whose value has this child element name
            /// </summary>
            /// <param name="name"></param>
            /// <param name="member"></param>
            /// <returns></returns>
            public bool TryGetMember(ReadOnlySpan<byte> name, [NotNullWhen(true)] out MemberAssociation? member) => _members.TryGetValue(name, out member);
        }

        /// <summary>
        /// A member's element: hands its value to the member pipeline
        /// </summary>
        /// <param name="member"></param>
        internal sealed class MemberAssociation(MemberInfo member)
        {
            private readonly PropertyInfo? _property = member as PropertyInfo;

            private readonly FieldInfo? _field = member as FieldInfo;

            public void Set(RowPipelines pipelines, ReadOnlySpan<byte> utf8, in FieldContext context)
            {
                if (_property is not null)
                {
                    pipelines.Pipeline(_property, utf8, in context);
                }
                else
                {
                    pipelines.Pipeline(_field!, utf8, in context);
                }
            }
        }

        /// <summary>
        /// Ordinal UTF-8 name comparer, looked up by span without allocating
        /// </summary>
        private sealed class Utf8NameComparer : IEqualityComparer<byte[]>, IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>
        {
            public static readonly Utf8NameComparer Instance = new();

            public bool Equals(byte[]? x, byte[]? y) => x.AsSpan().SequenceEqual(y);

            public int GetHashCode(byte[] obj) => GetHashCode(obj.AsSpan());

            public bool Equals(ReadOnlySpan<byte> alternate, byte[] other) => alternate.SequenceEqual(other);

            public int GetHashCode(ReadOnlySpan<byte> alternate)
            {
                HashCode hash = default;
                hash.AddBytes(alternate);
                return hash.ToHashCode();
            }

            public byte[] Create(ReadOnlySpan<byte> alternate) => alternate.ToArray();
        }
    }
}
