using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Forestry.Serialization.Xml
{
    /// <summary>
    /// Throws for the XML format's association of elements with telemetries and members; the reader's own
    /// exceptions are Forestry.Xml's
    /// </summary>
    internal static partial class Throwing
    {
        #region invalid operation
        [DoesNotReturn]
        public static void WhenDuplicateTelemetryName(Type first, Type second, string name)
        {
            throw new InvalidOperationException(Serialization.Formatting.Format(Formatting.DuplicateTelemetryName, first.FullName, second.FullName, name));
        }

        [DoesNotReturn]
        public static void WhenDuplicateMemberName(Type type, MemberInfo first, MemberInfo second, string name)
        {
            throw new InvalidOperationException(Serialization.Formatting.Format(Formatting.DuplicateMemberName, type.FullName, first.Name, second.Name, name));
        }

        [DoesNotReturn]
        public static void WhenDuplicateAttributeName(Type type, MemberInfo first, MemberInfo second, string name)
        {
            throw new InvalidOperationException(Serialization.Formatting.Format(Formatting.DuplicateAttributeName, type.FullName, first.Name, second.Name, name));
        }

        [DoesNotReturn]
        public static void WhenMoreThanOneElementText(Type type, MemberInfo first, MemberInfo second)
        {
            throw new InvalidOperationException(Serialization.Formatting.Format(Formatting.MoreThanOneElementText, type.FullName, first.Name, second.Name));
        }

        [DoesNotReturn]
        public static void WhenElementTextTypeNotAdded(Type type)
        {
            throw new InvalidOperationException(Serialization.Formatting.Format(Formatting.ElementTextTypeNotAdded, type.FullName));
        }

        [DoesNotReturn]
        public static void WhenElementTextMemberNotFound(Type type, string memberName)
        {
            throw new InvalidOperationException(Serialization.Formatting.Format(Formatting.ElementTextMemberNotFound, type.FullName, memberName));
        }

        [DoesNotReturn]
        public static void WhenMemberWithMoreThanOneSource(Type type, MemberInfo member, IEnumerable<string> sources)
        {
            throw new InvalidOperationException(Serialization.Formatting.Format(Formatting.MemberWithMoreThanOneSource, type.FullName, member.Name, string.Join(", ", sources)));
        }
        #endregion
    }
}
