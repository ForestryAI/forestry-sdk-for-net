using System.Reflection;
using Forestry.Deserialize.Converters;

namespace Forestry.Deserialize
{
    /// <summary>
    /// Metadata about one member of a row, never a value (research R4). The value lives on the row's
    /// typed property or field; per-occurrence metadata (depth, name, path) is kept only when a policy
    /// asks for it
    /// </summary>
    public sealed class Field
    {
        internal Field(MemberInfo member, Type type, Converter converter)
        {
            Member = member;
            Type = type;
            Converter = converter;
        }

        /// <summary>
        /// The <see cref="PropertyInfo"/> or <see cref="FieldInfo"/> of the member
        /// </summary>
        public MemberInfo Member { get; }

        /// <summary>
        /// Member name in C#
        /// </summary>
        public string Name => Member.Name;

        /// <summary>
        /// C# type of the member
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Effective converter of the member's type
        /// </summary>
        public Converter Converter { get; }
    }
}
