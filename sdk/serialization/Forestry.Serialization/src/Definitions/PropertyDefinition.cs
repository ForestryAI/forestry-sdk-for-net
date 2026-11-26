using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Reading;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Definitions
{
    /// <summary>
    /// Definition of one property or field of a telemetry type: converts a value handed over as UTF-8 text
    /// into the member's type and sets it on a telemetry (research R5, R14)
    /// </summary>
    public abstract class PropertyDefinition
    {
        private protected PropertyDefinition(MemberInfo member, Type memberType)
        {
            Member = member;
            MemberType = memberType;
            Utf8Name = Encoding.UTF8.GetBytes(member.Name);
        }

        /// <summary>
        /// Member name in C#
        /// </summary>
        public string Name => Member.Name;

        /// <summary>
        /// <see cref="Name"/> in UTF-8, encoded once when the definition is built, so a writer never encodes
        /// names per telemetry (spec 004 research R46)
        /// </summary>
        public ReadOnlyMemory<byte> Utf8Name { get; }

        /// <summary>
        /// The <see cref="PropertyInfo"/> or <see cref="FieldInfo"/>
        /// </summary>
        public MemberInfo Member { get; }

        /// <summary>
        /// C# type of the member
        /// </summary>
        public Type MemberType { get; }

        /// <summary>
        /// The converter this member uses, resolved from the options when the definition is configured
        /// (spec 003, research R33)
        /// </summary>
        public Converter ResolvedConverter { get; private set; } = null!;

        /// <summary>
        /// Position in its type definition's <see cref="TypeDefinition.Properties"/>, e.g. for a columnar writer to
        /// pick the column in <see cref="TelemetryWriter.WriteName"/> (spec 004 research R46)
        /// </summary>
        public int Index { get; internal set; }

        /// <summary>
        /// Whether the member can be read from a telemetry, to write it
        /// </summary>
        internal abstract bool CanGet { get; }

        /// <summary>
        /// Write this member of <paramref name="telemetry"/> through <paramref name="writer"/>: a missing value
        /// through <see cref="TelemetryWriter.WriteNull"/>; otherwise <see cref="TelemetryWriter.WriteName"/>
        /// then the resolved converter's <c>Write</c>, without boxing (spec 004 research R46, R49; System.Text.Json's
        /// <c>GetMemberAndWriteJson</c>)
        /// </summary>
        /// <param name="telemetry"></param>
        /// <param name="writer"></param>
        internal abstract void WriteMember(Telemetry telemetry, TelemetryWriter writer);

        /// <summary>
        /// Convert <paramref name="utf8"/> and set it on <paramref name="telemetry"/> through
        /// <paramref name="set"/>, without boxing
        /// </summary>
        /// <param name="telemetry"></param>
        /// <param name="utf8"></param>
        /// <param name="information"></param>
        /// <param name="set">The composed set step, an <c>Action&lt;ContextTelemetry, T&gt;</c> from <see cref="ComposeSet"/></param>
        internal abstract void ReadAndSet(ContextTelemetry telemetry, ReadOnlySpan<byte> utf8, in ReadingInformation information, Delegate set);

        /// <summary>
        /// Compose the enumeration policies acting on this member's set into one
        /// <c>Action&lt;ContextTelemetry, T&gt;</c>, the first outermost; the built-in step sets the member and
        /// marks it set (research R19, spec 002 R22)
        /// </summary>
        /// <param name="chain"></param>
        /// <returns></returns>
        internal abstract Delegate ComposeSet(IReadOnlyList<Policies.EnumerationPolicy> chain);

        /// <summary>
        /// Resolve this member's converter from the options, once when definitions are built; null when
        /// none handles its type (research R33). Format packages take no part in it
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        private protected abstract Converter? ResolveConverter(TelemetrySerializationOptions options);

        /// <summary>
        /// Resolve and keep the converter (research R33, step 3)
        /// </summary>
        /// <param name="options"></param>
        /// <returns>Null when configured; otherwise why the member cannot be mapped</returns>
        private string? Configure(TelemetrySerializationOptions options)
        {
            Converter? converter = ResolveConverter(options);

            if (converter is null)
            {
                return Formatting.Format(Formatting.MemberWithoutConverter, Member.Name, Throwing.DisplayName(MemberType));
            }

            ResolvedConverter = converter;
            return null;
        }

        /// <summary>
        /// Create and configure the definition of a member, once when definitions are built (research
        /// R33): check the member's type is a single value (R32) before the generic definition exists,
        /// since some types cannot be a generic argument; create it; resolve its converter
        /// </summary>
        /// <param name="member"></param>
        /// <param name="memberType"></param>
        /// <param name="options"></param>
        /// <param name="definition">The definition, when the member can be mapped</param>
        /// <param name="failure">Why it cannot, for the type definition to gather</param>
        /// <returns></returns>
        internal static bool TryCreate(
            MemberInfo member,
            Type memberType,
            TelemetrySerializationOptions options,
            [NotNullWhen(true)] out PropertyDefinition? definition,
            [NotNullWhen(false)] out string? failure
        ) {
            definition = null;
            failure = CheckSingleValue(member, memberType);

            if (failure is not null)
            {
                return false;
            }

            PropertyDefinition created = (PropertyDefinition)Activator.CreateInstance(
                typeof(PropertyDefinition<>).MakeGenericType(memberType),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: [member],
                culture: null
            )!;

            failure = created.Configure(options);

            if (failure is not null)
            {
                return false;
            }

            definition = created;
            return true;
        }

        /// <summary>
        /// Whether a member's type is a single value (constitution C7, research R32): after unwrapping
        /// <see cref="Nullable{T}"/>, a primitive, <see cref="string"/>, an enum, or a value type that
        /// formats to UTF-8 (<see cref="IUtf8SpanFormattable"/>) and parses from text
        /// (<see cref="IUtf8SpanParsable{TSelf}"/> or <see cref="ISpanParsable{TSelf}"/>)
        /// </summary>
        /// <param name="member"></param>
        /// <param name="memberType"></param>
        /// <returns>Null for a single value; otherwise why not: a value type formatting only to chars, or a
        /// nested structure</returns>
        private static string? CheckSingleValue(MemberInfo member, Type memberType)
        {
            Type type = Nullable.GetUnderlyingType(memberType) ?? memberType;

            if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
            {
                return null;
            }

            if (type.IsValueType)
            {
                bool formatsToUtf8 = typeof(IUtf8SpanFormattable).IsAssignableFrom(type);

                if (formatsToUtf8 && (Implements(type, typeof(IUtf8SpanParsable<>)) || Implements(type, typeof(ISpanParsable<>))))
                {
                    return null;
                }

                if (!formatsToUtf8 && typeof(ISpanFormattable).IsAssignableFrom(type))
                {
                    return Formatting.Format(Formatting.MemberTypeNotUtf8Formattable, member.Name, Throwing.DisplayName(memberType));
                }
            }

            return Formatting.Format(Formatting.MemberTypeIsNestedStructure, member.Name, Throwing.DisplayName(memberType));
        }

        /// <summary>
        /// Whether <paramref name="type"/> implements the self-referencing generic interface for itself,
        /// e.g. <c>ISpanParsable&lt;Guid&gt;</c> for <see cref="Guid"/>; looked up among its interfaces,
        /// since constructing the interface for a type outside its constraint would throw
        /// </summary>
        /// <param name="type"></param>
        /// <param name="genericInterface"></param>
        /// <returns></returns>
        private static bool Implements(Type type, Type genericInterface) =>
            type.GetInterfaces().Any(contract =>
                contract.IsGenericType &&
                contract.GetGenericTypeDefinition() == genericInterface &&
                contract.GetGenericArguments()[0] == type);
    }
}
