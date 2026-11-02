using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Forestry.Deserialize.Converters;

namespace Forestry.Deserialize.Definitions
{
    /// <summary>
    /// Definition of a row type, built once per options: it constructs rows of its concrete type and
    /// owns the property definitions that set their members; it sets no members itself (research R5)
    /// </summary>
    public sealed class TypeDefinition
    {
        private const BindingFlags ConstructorBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private TypeDefinition(
            Type type,
            DeserializeOptions options,
            bool isHandedOut,
            Func<Row> constructor,
            PropertyDefinition[] properties
        ) {
            Type = type;
            Options = options;
            IsHandedOut = isHandedOut;
            _constructor = constructor;
            Properties = properties;
            _propertiesByMember = properties.ToFrozenDictionary(property => MemberKey.Of(property.Member));
        }

        /// <summary>
        /// The row type, extending <see cref="Row"/>
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Options that built this definition
        /// </summary>
        public DeserializeOptions Options { get; }

        /// <summary>
        /// Rows of this type are handed out: the type was added explicitly (research R12)
        /// </summary>
        public bool IsHandedOut { get; }

        /// <summary>
        /// One property definition per property or field whose type a converter handles
        /// </summary>
        public IReadOnlyList<PropertyDefinition> Properties { get; }

        private readonly Func<Row> _constructor;

        private readonly FrozenDictionary<MemberKey, PropertyDefinition> _propertiesByMember;

        /// <summary>
        /// Construct a row with the chosen constructor (no parameters in this slice)
        /// </summary>
        /// <returns></returns>
        internal Row Construct() => _constructor();

        /// <summary>
        /// Property definition of one of this type's members
        /// </summary>
        /// <param name="member"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        internal bool TryGetProperty(MemberInfo member, [NotNullWhen(true)] out PropertyDefinition? property) =>
            _propertiesByMember.TryGetValue(MemberKey.Of(member), out property);

        /// <summary>
        /// Build the definition of a row type, rejecting it before any byte is read (FR-008)
        /// </summary>
        /// <param name="type"></param>
        /// <param name="options"></param>
        /// <param name="isHandedOut"></param>
        /// <returns></returns>
        internal static TypeDefinition Create(Type type, DeserializeOptions options, bool isHandedOut)
        {
            ConstructorInfo constructor = ChooseConstructor(type);

            if (constructor.GetParameters().Length > 0)
            {
                Throwing.WhenConstructorHasParameters(type);
            }

            List<PropertyDefinition> properties = [];

            foreach (MemberInfo member in GetMembers(type))
            {
                Type memberType = member is PropertyInfo property ? property.PropertyType : ((FieldInfo)member).FieldType;
                Converter? converter = options.ResolveConverter(memberType);

                if (converter is null)
                {
                    Throwing.WhenMemberWithoutConverter(type, member, memberType);
                }

                properties.Add(PropertyDefinition.Create(member, memberType, converter));
            }

            Func<Row> construct = Expression.Lambda<Func<Row>>(Expression.Convert(Expression.New(constructor), typeof(Row))).Compile();

            return new TypeDefinition(type, options, isHandedOut, construct, [.. properties]);
        }

        /// <summary>
        /// Exactly one constructor marked <see cref="RowConstructorAttribute"/> is used, whatever its
        /// accessibility; none marked and exactly one public constructor: that one; otherwise, or with
        /// more than one marked, fail fast (research R5)
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static ConstructorInfo ChooseConstructor(Type type)
        {
            ConstructorInfo[] constructors = type.GetConstructors(ConstructorBindingFlags);
            ConstructorInfo[] marked = Array.FindAll(constructors, constructor => constructor.IsDefined(typeof(RowConstructorAttribute), inherit: false));

            if (marked.Length > 1)
            {
                Throwing.WhenMoreThanOneRowConstructor(type);
            }

            if (marked.Length == 1)
            {
                return marked[0];
            }

            ConstructorInfo[] publicConstructors = Array.FindAll(constructors, constructor => constructor.IsPublic);

            if (publicConstructors.Length != 1)
            {
                Throwing.WhenNoConstructorChosen(type);
            }

            return publicConstructors[0];
        }

        /// <summary>
        /// Public instance properties with a public setter, and public instance fields that can be set
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static IEnumerable<MemberInfo> GetMembers(Type type)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.SetMethod is { IsPublic: true } && property.GetIndexParameters().Length == 0)
                {
                    yield return property;
                }
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!field.IsInitOnly && !field.IsLiteral)
                {
                    yield return field;
                }
            }
        }

        /// <summary>
        /// Stable key of a member, independent of how its <see cref="MemberInfo"/> was obtained
        /// (research R6)
        /// </summary>
        private readonly record struct MemberKey(Module Module, int MetadataToken)
        {
            public static MemberKey Of(MemberInfo member) => new(member.Module, member.MetadataToken);
        }
    }
}
