using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Definitions
{
    /// <summary>
    /// Definition of a telemetry type, built once per options: it constructs telemetries of its concrete type and
    /// owns the property definitions that set their members; it sets no members itself (research R5). Each
    /// telemetry type'"'"'s definition is a <see cref="TypeDefinition{T}"/>, whose object converter writes its
    /// telemetries (spec 004 research R49)
    /// </summary>
    public abstract class TypeDefinition
    {
        private const BindingFlags ConstructorBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private protected TypeDefinition(
            Type type,
            TelemetrySerializationOptions options,
            Func<Telemetry> constructor,
            PropertyDefinition[] properties
        ) {
            Type = type;
            Options = options;
            _constructor = constructor;
            Properties = properties;
            _propertiesByMember = properties.ToFrozenDictionary(property => MemberKey.Of(property.Member));
            _indexesByName = properties.ToFrozenDictionary(property => property.Name, property => property.Index, StringComparer.Ordinal);
        }

        /// <summary>
        /// The telemetry type, extending <see cref="Telemetry"/>
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Options that built this definition
        /// </summary>
        public TelemetrySerializationOptions Options { get; }

        /// <summary>
        /// One property definition per included property or field: each a single value with a resolved
        /// converter (spec 003, research R33)
        /// </summary>
        public IReadOnlyList<PropertyDefinition> Properties { get; }

        private readonly Func<Telemetry> _constructor;

        private readonly FrozenDictionary<MemberKey, PropertyDefinition> _propertiesByMember;

        private readonly FrozenDictionary<string, int> _indexesByName;

        /// <summary>
        /// Construct a telemetry with the chosen constructor (no parameters in this slice)
        /// </summary>
        /// <returns></returns>
        internal Telemetry Construct() => _constructor();

        /// <summary>
        /// Property definition of one of this type's members
        /// </summary>
        /// <param name="member"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        internal bool TryGetProperty(MemberInfo member, [NotNullWhen(true)] out PropertyDefinition? property) =>
            _propertiesByMember.TryGetValue(MemberKey.Of(member), out property);

        /// <summary>
        /// Index in <see cref="Properties"/> of the member with this C# name: the bit its set-member
        /// bitmap uses (spec 002, research R22)
        /// </summary>
        /// <param name="memberName"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        internal bool TryGetPropertyIndex(string memberName, out int index) =>
            _indexesByName.TryGetValue(memberName, out index);

        /// <summary>
        /// Write <paramref name="telemetry"/> through this type'"'"'s resolved object converter (spec 004 research R49)
        /// </summary>
        /// <param name="telemetry">A telemetry of <see cref="Type"/></param>
        /// <param name="writer"></param>
        internal abstract void WriteTelemetry(Telemetry telemetry, TelemetryWriter writer);

        /// <summary>
        /// Resolve the object converter of this telemetry type from the options, once when the definition is
        /// built: the options'"'"' converter for the type when it is an object converter, else the default
        /// </summary>
        /// <param name="options"></param>
        /// <returns>Null when resolved; otherwise why the type cannot be written</returns>
        private protected abstract string? ResolveObjectConverter(TelemetrySerializationOptions options);

        /// <summary>
        /// Build the definition of a telemetry type, rejecting it before any byte is read (FR-008), with one
        /// exception naming every member that cannot be mapped (spec 003, research R33)
        /// </summary>
        /// <param name="type"></param>
        /// <param name="options"></param>
        /// <param name="includeMember">Whether a candidate property or field is a member: the definition
        /// policies' chain (research R19)</param>
        /// <returns></returns>
        internal static TypeDefinition Create(Type type, TelemetrySerializationOptions options, Func<MemberInfo, bool> includeMember)
        {
            ConstructorInfo constructor = ChooseConstructor(type);

            if (constructor.GetParameters().Length > 0)
            {
                Throwing.WhenConstructorHasParameters(type);
            }

            List<PropertyDefinition> properties = [];
            List<string>? failures = null;

            foreach (MemberInfo member in GetMembers(type))
            {
                if (!includeMember(member))
                {
                    continue;  // left out by a definition policy (research R19): never checked (spec 003 R33)
                }

                Type memberType = member is PropertyInfo property ? property.PropertyType : ((FieldInfo)member).FieldType;

                // The property definition checks the single value and resolves its converter; failures
                // of all members are gathered into one exception (spec 003, research R33)
                if (!PropertyDefinition.TryCreate(member, memberType, options, out PropertyDefinition? definition, out string? failure))
                {
                    (failures ??= []).Add(failure);
                    continue;
                }

                definition.Index = properties.Count;
                properties.Add(definition);
            }

            if (failures is not null)
            {
                Throwing.WhenTelemetryTypeMembersInvalid(type, failures);
            }

            Func<Telemetry> construct = Expression.Lambda<Func<Telemetry>>(Expression.Convert(Expression.New(constructor), typeof(Telemetry))).Compile();

            TypeDefinition created = (TypeDefinition)Activator.CreateInstance(
                typeof(TypeDefinition<>).MakeGenericType(type),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: [type, options, construct, properties.ToArray()],
                culture: null
            )!;

            string? converterFailure = created.ResolveObjectConverter(options);

            if (converterFailure is not null)
            {
                Throwing.WhenTelemetryTypeMembersInvalid(type, [converterFailure]);
            }

            return created;
        }

        /// <summary>
        /// Exactly one constructor marked <see cref="TelemetryConstructorAttribute"/> is used, whatever its
        /// accessibility; none marked and exactly one public constructor: that one; otherwise, or with
        /// more than one marked, fail fast (research R5)
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static ConstructorInfo ChooseConstructor(Type type)
        {
            ConstructorInfo[] constructors = type.GetConstructors(ConstructorBindingFlags);
            ConstructorInfo[] marked = Array.FindAll(constructors, constructor => constructor.IsDefined(typeof(TelemetryConstructorAttribute), inherit: false));

            if (marked.Length > 1)
            {
                Throwing.WhenMoreThanOneTelemetryConstructor(type);
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
