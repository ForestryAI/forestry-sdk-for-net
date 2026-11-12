using System.Reflection;
using Forestry.Deserialize.Converters;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Definitions
{
    /// <summary>
    /// Definition of one property or field of a row type: converts a value handed over as UTF-8 text
    /// into the member's type and sets it on a row (research R5, R14)
    /// </summary>
    public abstract class PropertyDefinition
    {
        private protected PropertyDefinition(MemberInfo member, Type memberType, Converter converter)
        {
            Member = member;
            MemberType = memberType;
            Field = new Field(member, memberType, converter);
        }

        /// <summary>
        /// Member name in C#
        /// </summary>
        public string Name => Member.Name;

        /// <summary>
        /// The <see cref="PropertyInfo"/> or <see cref="FieldInfo"/>
        /// </summary>
        public MemberInfo Member { get; }

        /// <summary>
        /// C# type of the member
        /// </summary>
        public Type MemberType { get; }

        /// <summary>
        /// Effective converter of the member's type
        /// </summary>
        public Converter Converter => Field.Converter;

        /// <summary>
        /// Metadata about the member (research R4)
        /// </summary>
        public Field Field { get; }

        /// <summary>
        /// Position in its type definition's <see cref="TypeDefinition.Properties"/>
        /// </summary>
        internal int Index { get; set; }

        /// <summary>
        /// Convert <paramref name="utf8"/> and set it on <paramref name="row"/> through
        /// <paramref name="set"/>, without boxing
        /// </summary>
        /// <param name="row"></param>
        /// <param name="utf8"></param>
        /// <param name="context"></param>
        /// <param name="set">The composed set step, an <c>Action&lt;ContextRow, T&gt;</c> from <see cref="ComposeSet"/></param>
        internal abstract void ReadAndSet(ContextRow row, ReadOnlySpan<byte> utf8, in FieldContext context, Delegate set);

        /// <summary>
        /// Compose the enumeration policies acting on this member's set into one
        /// <c>Action&lt;ContextRow, T&gt;</c>, the first outermost; the built-in step sets the member and
        /// marks it set (research R19, spec 002 R22)
        /// </summary>
        /// <param name="chain"></param>
        /// <returns></returns>
        internal abstract Delegate ComposeSet(IReadOnlyList<Policies.EnumerationPolicy> chain);

        /// <summary>
        /// Create the generic definition for the member's type, once when definitions are built
        /// </summary>
        /// <param name="member"></param>
        /// <param name="memberType"></param>
        /// <param name="converter"></param>
        /// <returns></returns>
        internal static PropertyDefinition Create(MemberInfo member, Type memberType, Converter converter) =>
            (PropertyDefinition)Activator.CreateInstance(
                typeof(PropertyDefinition<>).MakeGenericType(memberType),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: [member, converter],
                culture: null
            )!;
    }
}
