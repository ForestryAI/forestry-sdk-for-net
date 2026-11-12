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
        /// <remarks>
        /// <para>
        /// Each member has one source (spec 002 R30, FR-018): an attribute of the row's element
        /// (<see cref="XmlAttributeValueAttribute"/>), the element's text (<see cref="XmlTextValueAttribute"/>
        /// or <see cref="DeserializeXmlOptions.TextValue{TRow}(string)"/>, the options winning), or by
        /// default a direct child element. Each source has its own name lookup and duplicate check.
        /// </para>
        /// <para>
        /// Row and child-element names are resolved here from <see cref="RowAttribute"/> and
        /// <see cref="FieldAttribute"/>, or the class and member names: core annotations the media
        /// interprets (research R8), a known debt (#103).
        /// </para>
        /// </remarks>
        /// <param name="options"></param>
        /// <returns></returns>
        public static XmlAssociation Create(DeserializeXmlOptions options)
        {
            MethodInfo start = typeof(RowBuilder).GetMethod(nameof(RowBuilder.Start), genericParameterCount: 1, Type.EmptyTypes)!;
            Dictionary<byte[], RowAssociation> rows = new(Utf8NameComparer.Instance);

            foreach (Type type in options.TextValues.Keys)
            {
                if (!options.Rows.Contains(type))
                {
                    Throwing.WhenTextValueTypeNotAdded(type);
                }
            }

            foreach (Type type in options.Rows)
            {
                TypeDefinition typeDefinition = options.ThrowingGetTypeDefinition(type);
                Dictionary<byte[], MemberAssociation> members = new(Utf8NameComparer.Instance);
                Dictionary<byte[], MemberAssociation> attributes = new(Utf8NameComparer.Instance);
                MemberAssociation? text = null;
                bool textByOptions = options.TextValues.TryGetValue(type, out string? textMemberName);

                if (textByOptions && !typeDefinition.Properties.Any(property => property.Name == textMemberName))
                {
                    Throwing.WhenTextValueMemberNotFound(type, textMemberName!);
                }

                foreach (PropertyDefinition property in typeDefinition.Properties)
                {
                    bool isText = textByOptions
                        ? property.Name == textMemberName
                        : property.Member.IsDefined(typeof(XmlTextValueAttribute), inherit: false);

                    if (isText)
                    {
                        if (text is not null)
                        {
                            Throwing.WhenMoreThanOneTextValue(type, text.Member, property.Member);
                        }

                        text = new MemberAssociation(property.Member);
                        continue;
                    }

                    if (property.Member.GetCustomAttribute<XmlAttributeValueAttribute>(inherit: false) is { } attribute)
                    {
                        byte[] utf8AttributeName = Encoding.UTF8.GetBytes(attribute.Name);

                        if (attributes.TryGetValue(utf8AttributeName, out MemberAssociation? otherAttribute))
                        {
                            Throwing.WhenDuplicateAttributeName(type, otherAttribute.Member, property.Member, attribute.Name);
                        }

                        attributes.Add(utf8AttributeName, new MemberAssociation(property.Member));
                        continue;
                    }

                    string memberName = property.Member.GetCustomAttribute<FieldAttribute>(inherit: false)?.Name is { Length: > 0 } fieldName ? fieldName : property.Member.Name;
                    byte[] utf8MemberName = Encoding.UTF8.GetBytes(memberName);

                    if (members.TryGetValue(utf8MemberName, out MemberAssociation? other))
                    {
                        Throwing.WhenDuplicateMemberName(type, other.Member, property.Member, memberName);
                    }

                    members.Add(utf8MemberName, new MemberAssociation(property.Member));
                }

                string rowName = type.GetCustomAttribute<RowAttribute>(inherit: false)?.Name is { Length: > 0 } name ? name : type.Name;
                byte[] utf8RowName = Encoding.UTF8.GetBytes(rowName);

                if (rows.TryGetValue(utf8RowName, out RowAssociation? otherRow))
                {
                    Throwing.WhenDuplicateRowName(otherRow.Type, type, rowName);
                }

                Action<RowBuilder> startRow = start.MakeGenericMethod(type).CreateDelegate<Action<RowBuilder>>();

                rows.Add(utf8RowName, new RowAssociation(type, startRow, members, attributes, text));
            }

            return new XmlAssociation(rows);
        }

        /// <summary>
        /// A row type's element: start a row of it, find its members by child element name, by attribute
        /// name, and its text member
        /// </summary>
        internal sealed class RowAssociation
        {
            public RowAssociation(
                Type type,
                Action<RowBuilder> start,
                Dictionary<byte[], MemberAssociation> members,
                Dictionary<byte[], MemberAssociation> attributes,
                MemberAssociation? text)
            {
                Type = type;
                Start = start;
                _members = members.GetAlternateLookup<ReadOnlySpan<byte>>();
                _attributes = attributes.GetAlternateLookup<ReadOnlySpan<byte>>();
                Text = text;
            }

            private readonly Dictionary<byte[], MemberAssociation>.AlternateLookup<ReadOnlySpan<byte>> _attributes;

            /// <summary>
            /// The member set from the element's text, if any
            /// </summary>
            public MemberAssociation? Text { get; }

            /// <summary>
            /// Member whose value has this attribute name on the row's element
            /// </summary>
            /// <param name="name"></param>
            /// <param name="member"></param>
            /// <returns></returns>
            public bool TryGetAttribute(ReadOnlySpan<byte> name, [NotNullWhen(true)] out MemberAssociation? member) => _attributes.TryGetValue(name, out member);

            /// <summary>
            /// The row type
            /// </summary>
            public Type Type { get; }

            /// <summary>
            /// Calls <c>Start&lt;T&gt;()</c> for the row type, bound once
            /// </summary>
            public Action<RowBuilder> Start { get; }

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
        /// A member's element: hands its value to the row builder
        /// </summary>
        /// <param name="member"></param>
        internal sealed class MemberAssociation(MemberInfo member)
        {
            /// <summary>
            /// The property or field
            /// </summary>
            public MemberInfo Member { get; } = member;

            private readonly PropertyInfo? _property = member as PropertyInfo;

            private readonly FieldInfo? _field = member as FieldInfo;

            public void Set(RowBuilder builder, ReadOnlySpan<byte> utf8, in FieldContext context)
            {
                if (_property is not null)
                {
                    builder.Set(_property, utf8, in context);
                }
                else
                {
                    builder.Set(_field!, utf8, in context);
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
