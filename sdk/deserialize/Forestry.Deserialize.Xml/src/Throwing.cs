using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Forestry.Deserialize.Xml
{
    /// <summary>
    /// Throws for the XML media's association of elements with rows and members; the reader's own
    /// exceptions are Forestry.Xml's
    /// </summary>
    internal static partial class Throwing
    {
        #region invalid operation
        [DoesNotReturn]
        public static void WhenDuplicateRowName(Type first, Type second, string name)
        {
            throw new InvalidOperationException(Deserialize.Formatting.Format(Formatting.DuplicateRowName, first.FullName, second.FullName, name));
        }

        [DoesNotReturn]
        public static void WhenDuplicateMemberName(Type type, MemberInfo first, MemberInfo second, string name)
        {
            throw new InvalidOperationException(Deserialize.Formatting.Format(Formatting.DuplicateMemberName, type.FullName, first.Name, second.Name, name));
        }

        [DoesNotReturn]
        public static void WhenDuplicateAttributeName(Type type, MemberInfo first, MemberInfo second, string name)
        {
            throw new InvalidOperationException(Deserialize.Formatting.Format(Formatting.DuplicateAttributeName, type.FullName, first.Name, second.Name, name));
        }

        [DoesNotReturn]
        public static void WhenMoreThanOneTextValue(Type type, MemberInfo first, MemberInfo second)
        {
            throw new InvalidOperationException(Deserialize.Formatting.Format(Formatting.MoreThanOneTextValue, type.FullName, first.Name, second.Name));
        }

        [DoesNotReturn]
        public static void WhenTextValueTypeNotAdded(Type type)
        {
            throw new InvalidOperationException(Deserialize.Formatting.Format(Formatting.TextValueTypeNotAdded, type.FullName));
        }

        [DoesNotReturn]
        public static void WhenTextValueMemberNotFound(Type type, string memberName)
        {
            throw new InvalidOperationException(Deserialize.Formatting.Format(Formatting.TextValueMemberNotFound, type.FullName, memberName));
        }
        #endregion
    }
}
