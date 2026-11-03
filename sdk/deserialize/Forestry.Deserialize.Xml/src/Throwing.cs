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
        #endregion
    }
}
