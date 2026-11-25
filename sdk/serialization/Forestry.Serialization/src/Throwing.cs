using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Forestry.Serialization.Definitions;

namespace Forestry.Serialization
{
    internal static partial class Throwing
    {
        public static void ArguementIsNull([NotNull] object? argument, [CallerArgumentExpression(nameof(argument))] string? name = null)
        {
            if (argument is null)
            {
                throw new ArgumentNullException(name);
            }
        }

        [DoesNotReturn]
        public static void WhenDeserializeOptionsIsReadOnly()
        {
            throw new InvalidOperationException(Formatting.DeserializeOptionsIsReadOnly);
        }

        [DoesNotReturn]
        public static void WhenNotSingularAttribute(Type attributeType, MemberInfo member)
        {
            string location = member is Type type ? type.ToString() : $"{member.DeclaringType}.{member.Name}";
            throw new InvalidOperationException(Formatting.Format(Formatting.WhenNotSingularAttribute, attributeType, location));
        }

        [DoesNotReturn]
        public static void WhenPipeReaderCanceled()
        {
            throw new OperationCanceledException(Formatting.PipeReaderCanceled);
        }

        #region Definitions (FR-008)
        [DoesNotReturn]
        public static void WhenNoRowTypes()
        {
            throw new InvalidOperationException(Formatting.NoRowTypes);
        }

        [DoesNotReturn]
        public static void WhenNotARowType(Type type)
        {
            throw new ArgumentException(Formatting.Format(Formatting.NotARowType, type.FullName));
        }

        [DoesNotReturn]
        public static void WhenNoConstructorChosen(Type type)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.NoConstructorChosen, type.FullName));
        }

        [DoesNotReturn]
        public static void WhenMoreThanOneRowConstructor(Type type)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.MoreThanOneRowConstructor, type.FullName));
        }

        [DoesNotReturn]
        public static void WhenConstructorHasParameters(Type type)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.ConstructorHasParameters, type.FullName));
        }

        /// <summary>
        /// One exception for a row type naming every member that failed when its definition was built:
        /// a nested structure, a struct formatting only to chars, or no converter (spec 003, research R33)
        /// </summary>
        /// <param name="type"></param>
        /// <param name="failures">One message per failing member, from <see cref="Formatting.MemberTypeIsNestedStructure"/>,
        /// <see cref="Formatting.MemberTypeNotUtf8Formattable"/> or <see cref="Formatting.MemberWithoutConverter"/></param>
        [DoesNotReturn]
        public static void WhenRowTypeMembersInvalid(Type type, IReadOnlyList<string> failures)
        {
            string list = string.Concat(failures.Select(failure => Environment.NewLine + "- " + failure));
            throw new InvalidOperationException(Formatting.Format(Formatting.RowTypeMembersInvalid, type.FullName, list));
        }

        /// <summary>
        /// A type's name for a message: its full name, with generic arguments written as in C#
        /// (<c>System.Collections.Generic.List&lt;System.Int32&gt;</c>) instead of the runtime's form
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal static string DisplayName(Type type)
        {
            if (type.IsArray)
            {
                return DisplayName(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            }

            if (!type.IsGenericType)
            {
                return type.FullName ?? type.Name;
            }

            string name = type.GetGenericTypeDefinition().FullName ?? type.Name;
            int tick = name.IndexOf('`');
            return (tick < 0 ? name : name[..tick]) + "<" + string.Join(", ", type.GetGenericArguments().Select(DisplayName)) + ">";
        }

        [DoesNotReturn]
        public static void WhenNoConverter(Type type)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.NoConverter, type.FullName));
        }
        #endregion

        #region Reading
        [DoesNotReturn]
        public static void WhenRowTypeNotAdded(Type type)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.RowTypeNotAdded, type.FullName));
        }

        [DoesNotReturn]
        public static void WhenMemberNotOfRow(MemberInfo member, Type rowType)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.MemberNotOfRow, member.Name, rowType.FullName));
        }

        [DoesNotReturn]
        public static void WhenNoOpenRow()
        {
            throw new InvalidOperationException(Formatting.NoOpenRow);
        }

        [DoesNotReturn]
        public static void WhenUnconvertible(PropertyDefinition property, Type rowType, string path, Exception innerException)
        {
            throw new FormatException(Formatting.Format(Formatting.Unconvertible, property.Name, path, rowType.FullName, property.MemberType.Name), innerException);
        }

        [DoesNotReturn]
        public static void WhenMemberPolicyNotSetOnly(Type policyType, Type type, MemberInfo member)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.MemberPolicyNotSetOnly, type.FullName, policyType.Name, member.Name));
        }

        [DoesNotReturn]
        public static void WhenDefinitionPolicyOnMember(Type policyType, Type type, MemberInfo member)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.DefinitionPolicyOnMember, type.FullName, policyType.Name, member.Name));
        }

        [DoesNotReturn]
        public static void WhenPolicyAttributesAmbiguous(MemberInfo target, string method)
        {
            string name = target is Type type ? type.FullName! : $"{target.DeclaringType?.FullName}.{target.Name}";
            throw new InvalidOperationException(Formatting.Format(Formatting.PolicyAttributesAmbiguous, name, method));
        }

        [DoesNotReturn]
        public static void WhenPolicyTypeNotAdded(Type type)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.PolicyTypeNotAdded, type.FullName));
        }

        [DoesNotReturn]
        public static void WhenPolicyMemberNotFound(Type type, string member)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.PolicyMemberNotFound, type.FullName, member));
        }

        [DoesNotReturn]
        public static void WhenEnumerationRunning()
        {
            throw new InvalidOperationException(Formatting.EnumerationRunning);
        }

        [DoesNotReturn]
        public static void WhenEnumeratedTwice()
        {
            throw new InvalidOperationException(Formatting.EnumeratedTwice);
        }

        [DoesNotReturn]
        public static void WhenPartialAtFinalBlock()
        {
            throw new InvalidOperationException(Formatting.PartialAtFinalBlock);
        }
        #endregion

        #region Deserialization context (spec 002)
        [DoesNotReturn]
        public static void WhenUnknownMember(Type rowType, string memberName)
        {
            throw new ArgumentException(Formatting.Format(Formatting.UnknownMember, rowType.FullName, memberName), nameof(memberName));
        }

        [DoesNotReturn]
        public static void WhenMemberValueOfOtherType(Type rowType, PropertyDefinition property, Type valueType)
        {
            throw new ArgumentException(Formatting.Format(Formatting.MemberValueOfOtherType, rowType.FullName, property.Name, property.MemberType.Name, valueType.Name));
        }

        [DoesNotReturn]
        public static void WhenSetAfterEnumerated(Type rowType, string memberName)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.SetAfterEnumerated, rowType.FullName, memberName));
        }

        [DoesNotReturn]
        public static void WhenCompletionActionOutsideCompletion(Type rowType, string action)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.CompletionActionOutsideCompletion, rowType.FullName, action));
        }

        [DoesNotReturn]
        public static void WhenCompletionActionTwice(Type rowType, string action)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.CompletionActionTwice, rowType.FullName, action));
        }

        [DoesNotReturn]
        public static void WhenReleaseNotAllowed(Type rowType, bool isOpen, bool isReleased)
        {
            string reason = isOpen ? Formatting.ReleaseOpen : isReleased ? Formatting.ReleaseTwice : Formatting.ReleaseNotKept;
            throw new InvalidOperationException(Formatting.Format(Formatting.ReleaseNotAllowed, rowType.FullName, reason));
        }

        [DoesNotReturn]
        public static void WhenReleaseUntilEnd(Type rowType)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.ReleaseUntilEnd, rowType.FullName));
        }

        [DoesNotReturn]
        public static void WhenContextLimitReached(int limit, IEnumerable<Reading.ContextRow> kept)
        {
            string perType = string.Join(", ", kept
                .GroupBy(row => row.Definition.Type.Name)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key} ({group.Count()})"));

            throw new Reading.DeserializeContextLimitException(Formatting.Format(Formatting.ContextLimitReached, limit, perType));
        }
        #endregion

        #region Rows (FR-006)
        [DoesNotReturn]
        public static void WhenRowNotOfType(Type rowType, Type type)
        {
            throw new InvalidCastException(Formatting.Format(Formatting.RowNotOfType, rowType.FullName, type.FullName));
        }
        #endregion
    }
}
