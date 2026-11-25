using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.Reading;

namespace Forestry.Serialization.Xml
{
    /// <summary>
    /// Which element means which telemetry type and which member, built once per options when they are
    /// locked; reflection only then (research R6, R8, R13 A: by name, anywhere). Lookups take the
    /// element name as UTF-8 bytes, without allocating
    /// </summary>
    internal sealed class XmlAssociation
    {
        private XmlAssociation(Dictionary<byte[], TelemetryAssociation> telemetries)
        {
            _telemetries = telemetries.GetAlternateLookup<ReadOnlySpan<byte>>();
        }

        private readonly Dictionary<byte[], TelemetryAssociation>.AlternateLookup<ReadOnlySpan<byte>> _telemetries;

        /// <summary>
        /// Telemetry type whose occurrences have this element name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="telemetry"></param>
        /// <returns></returns>
        public bool TryGetTelemetry(ReadOnlySpan<byte> name, [NotNullWhen(true)] out TelemetryAssociation? telemetry) => _telemetries.TryGetValue(name, out telemetry);

        /// <summary>
        /// Associate the telemetry types added to <paramref name="options"/>, which must be read-only
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each member has one source (spec 002 R30, FR-018; spec 003 FR-006a): an attribute of the telemetry's
        /// element (<see cref="XmlAttributeNameAttribute"/>), the element's own text
        /// (<see cref="XmlElementTextAttribute"/> or <see cref="DeserializeXmlOptions.ElementText{TTelemetry}(string)"/>,
        /// the options winning), or a direct child element (<see cref="XmlElementNameAttribute"/>, or by
        /// default the member name). A member declaring two sources fails. Each source has its own name
        /// lookup and duplicate check.
        /// </para>
        /// <para>
        /// Telemetry and child-element names are resolved here from <see cref="XmlElementNameAttribute"/>, or the
        /// class and member names (spec 003 research R37; spec 001 read the core's <c>[Telemetry]</c> and
        /// <c>[Field]</c>).
        /// </para>
        /// </remarks>
        /// <param name="options"></param>
        /// <returns></returns>
        public static XmlAssociation Create(DeserializeXmlOptions options)
        {
            MethodInfo start = typeof(TelemetryBuilder).GetMethod(nameof(TelemetryBuilder.Start), genericParameterCount: 1, Type.EmptyTypes)!;
            Dictionary<byte[], TelemetryAssociation> telemetries = new(Utf8NameComparer.Instance);

            foreach (Type type in options.ElementTexts.Keys)
            {
                if (!options.Telemetries.Contains(type))
                {
                    Throwing.WhenElementTextTypeNotAdded(type);
                }
            }

            foreach (Type type in options.Telemetries)
            {
                TypeDefinition typeDefinition = options.ThrowingGetTypeDefinition(type);
                Dictionary<byte[], MemberAssociation> members = new(Utf8NameComparer.Instance);
                Dictionary<byte[], MemberAssociation> attributes = new(Utf8NameComparer.Instance);
                MemberAssociation? text = null;
                bool textByOptions = options.ElementTexts.TryGetValue(type, out string? textMemberName);

                if (textByOptions && !typeDefinition.Properties.Any(property => property.Name == textMemberName))
                {
                    Throwing.WhenElementTextMemberNotFound(type, textMemberName!);
                }

                foreach (PropertyDefinition property in typeDefinition.Properties)
                {
                    XmlElementNameAttribute? elementName = property.Member.GetCustomAttribute<XmlElementNameAttribute>(inherit: false);
                    XmlAttributeNameAttribute? attribute = property.Member.GetCustomAttribute<XmlAttributeNameAttribute>(inherit: false);
                    bool textByAttribute = property.Member.IsDefined(typeof(XmlElementTextAttribute), inherit: false);
                    bool textByOption = textByOptions && property.Name == textMemberName;

                    ThrowingAssertOneSource(type, property.Member, elementName, attribute, textByAttribute, textByOption);

                    bool isText = textByOptions ? textByOption : textByAttribute;

                    if (isText)
                    {
                        if (text is not null)
                        {
                            Throwing.WhenMoreThanOneElementText(type, text.Member, property.Member);
                        }

                        text = new MemberAssociation(property.Member);
                        continue;
                    }

                    if (attribute is not null)
                    {
                        byte[] utf8AttributeName = Encoding.UTF8.GetBytes(attribute.Name);

                        if (attributes.TryGetValue(utf8AttributeName, out MemberAssociation? otherAttribute))
                        {
                            Throwing.WhenDuplicateAttributeName(type, otherAttribute.Member, property.Member, attribute.Name);
                        }

                        attributes.Add(utf8AttributeName, new MemberAssociation(property.Member));
                        continue;
                    }

                    string memberName = elementName?.Name ?? property.Member.Name;
                    byte[] utf8MemberName = Encoding.UTF8.GetBytes(memberName);

                    if (members.TryGetValue(utf8MemberName, out MemberAssociation? other))
                    {
                        Throwing.WhenDuplicateMemberName(type, other.Member, property.Member, memberName);
                    }

                    members.Add(utf8MemberName, new MemberAssociation(property.Member));
                }

                string telemetryName = type.GetCustomAttribute<XmlElementNameAttribute>(inherit: false)?.Name ?? type.Name;
                byte[] utf8TelemetryName = Encoding.UTF8.GetBytes(telemetryName);

                if (telemetries.TryGetValue(utf8TelemetryName, out TelemetryAssociation? otherTelemetry))
                {
                    Throwing.WhenDuplicateTelemetryName(otherTelemetry.Type, type, telemetryName);
                }

                Action<TelemetryBuilder> startTelemetry = start.MakeGenericMethod(type).CreateDelegate<Action<TelemetryBuilder>>();

                telemetries.Add(utf8TelemetryName, new TelemetryAssociation(type, startTelemetry, members, attributes, text));
            }

            return new XmlAssociation(telemetries);
        }

        /// <summary>
        /// A member is read from one place (spec 003 FR-006a, research R37): two or more of
        /// <see cref="XmlElementNameAttribute"/>, <see cref="XmlAttributeNameAttribute"/> and
        /// <see cref="XmlElementTextAttribute"/>, or <see cref="DeserializeXmlOptions.ElementText{TTelemetry}(string)"/>
        /// with either of the first two, fail. The option with the text attribute names the same source
        /// </summary>
        /// <param name="type"></param>
        /// <param name="member"></param>
        /// <param name="elementName"></param>
        /// <param name="attribute"></param>
        /// <param name="textByAttribute"></param>
        /// <param name="textByOption"></param>
        private static void ThrowingAssertOneSource(
            Type type,
            MemberInfo member,
            XmlElementNameAttribute? elementName,
            XmlAttributeNameAttribute? attribute,
            bool textByAttribute,
            bool textByOption
        ) {
            List<string> sources = [];

            if (elementName is not null)
            {
                sources.Add("[XmlElementName]");
            }

            if (attribute is not null)
            {
                sources.Add("[XmlAttributeName]");
            }

            if (textByAttribute)
            {
                sources.Add("[XmlElementText]");
            }
            else if (textByOption)
            {
                sources.Add("ElementText<TTelemetry> in the options");
            }

            if (sources.Count > 1)
            {
                Throwing.WhenMemberWithMoreThanOneSource(type, member, sources);
            }
        }

        /// <summary>
        /// A telemetry type's element: start a telemetry of it, find its members by child element name, by attribute
        /// name, and its text member
        /// </summary>
        internal sealed class TelemetryAssociation
        {
            public TelemetryAssociation(
                Type type,
                Action<TelemetryBuilder> start,
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
            /// Member whose value has this attribute name on the telemetry's element
            /// </summary>
            /// <param name="name"></param>
            /// <param name="member"></param>
            /// <returns></returns>
            public bool TryGetAttribute(ReadOnlySpan<byte> name, [NotNullWhen(true)] out MemberAssociation? member) => _attributes.TryGetValue(name, out member);

            /// <summary>
            /// The telemetry type
            /// </summary>
            public Type Type { get; }

            /// <summary>
            /// Calls <c>Start&lt;T&gt;()</c> for the telemetry type, bound once
            /// </summary>
            public Action<TelemetryBuilder> Start { get; }

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
        /// A member's element: hands its value to the telemetry builder
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

            public void Set(TelemetryBuilder builder, ReadOnlySpan<byte> utf8, in ReadingInformation information)
            {
                if (_property is not null)
                {
                    builder.Set(_property, utf8, in information);
                }
                else
                {
                    builder.Set(_field!, utf8, in information);
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
