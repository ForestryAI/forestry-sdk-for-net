using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Forestry.Deserialize.Definitions;

namespace Forestry.Deserialize
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

        [DoesNotReturn]
        public static void WhenMemberWithoutConverter(Type type, MemberInfo member, Type memberType)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.MemberWithoutConverter, type.FullName, member.Name, memberType.FullName));
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

        #region Rows (FR-006)
        [DoesNotReturn]
        public static void WhenRowNotOfType(Type rowType, Type type)
        {
            throw new InvalidCastException(Formatting.Format(Formatting.RowNotOfType, rowType.FullName, type.FullName));
        }
        #endregion
    }
}
