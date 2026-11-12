using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Policies;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Tests.Fixtures
{
    /// <summary>
    /// An enumeration policy taking part only in Construct, recognisable by its type where the options show
    /// the policies that apply to a definition (tasks.md T049, T053, research R19)
    /// </summary>
    public sealed class ConstructMarkerPolicy : EnumerationPolicy
    {
        public override Row Construct(TypeDefinition type, DeserializationContext context, Func<DeserializationContext, Row> next) => next(context);
    }

    /// <summary>
    /// A second Construct-only policy, to tell which of two applies
    /// </summary>
    public sealed class OtherConstructMarkerPolicy : EnumerationPolicy
    {
        public override Row Construct(TypeDefinition type, DeserializationContext context, Func<DeserializationContext, Row> next) => next(context);
    }

    /// <summary>
    /// An enumeration policy taking part only in Set (the member method)
    /// </summary>
    public sealed class SetMarkerPolicy : EnumerationPolicy
    {
        public override void Set<T>(ContextRow row, PropertyDefinition<T> property, T value, DeserializationContext context, Action<ContextRow, T> next) => next(row, value);
    }

    /// <summary>
    /// A second Set-only policy, to tell which of two applies
    /// </summary>
    public sealed class OtherSetMarkerPolicy : EnumerationPolicy
    {
        public override void Set<T>(ContextRow row, PropertyDefinition<T> property, T value, DeserializationContext context, Action<ContextRow, T> next) => next(row, value);
    }

    /// <summary>
    /// A definition policy leaving out every member whose name starts with <c>Excluded</c>, property or
    /// field alike (tasks.md T089, research R19)
    /// </summary>
    public sealed class ExcludingPolicy : DefinitionPolicy
    {
        public override bool IncludeMember(Type type, System.Reflection.MemberInfo member, Func<System.Reflection.MemberInfo, bool> next) =>
            !member.Name.StartsWith("Excluded", StringComparison.Ordinal) && next(member);
    }
}
