using System.Buffers;
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
        public static void WhenNoTelemetryTypes()
        {
            throw new InvalidOperationException(Formatting.NoTelemetryTypes);
        }

        [DoesNotReturn]
        public static void WhenNotATelemetryType(Type type)
        {
            throw new ArgumentException(Formatting.Format(Formatting.NotATelemetryType, type.FullName));
        }

        [DoesNotReturn]
        public static void WhenNoConstructorChosen(Type type)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.NoConstructorChosen, type.FullName));
        }

        [DoesNotReturn]
        public static void WhenMoreThanOneTelemetryConstructor(Type type)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.MoreThanOneTelemetryConstructor, type.FullName));
        }

        [DoesNotReturn]
        public static void WhenConstructorHasParameters(Type type)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.ConstructorHasParameters, type.FullName));
        }

        /// <summary>
        /// One exception for a telemetry type naming every member that failed when its definition was built:
        /// a nested structure, a struct formatting only to chars, or no converter (spec 003, research R33)
        /// </summary>
        /// <param name="type"></param>
        /// <param name="failures">One message per failing member, from <see cref="Formatting.MemberTypeIsNestedStructure"/>,
        /// <see cref="Formatting.MemberTypeNotUtf8Formattable"/> or <see cref="Formatting.MemberWithoutConverter"/></param>
        [DoesNotReturn]
        public static void WhenTelemetryTypeMembersInvalid(Type type, IReadOnlyList<string> failures)
        {
            string list = string.Concat(failures.Select(failure => Environment.NewLine + "- " + failure));
            throw new InvalidOperationException(Formatting.Format(Formatting.TelemetryTypeMembersInvalid, type.FullName, list));
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

        [DoesNotReturn]
        public static IBufferWriter<byte> WhenWriterHasNoOutput()
        {
            throw new InvalidOperationException(Formatting.WriterHasNoOutput);
        }

        [DoesNotReturn]
        public static void WhenConverterCannotWrite(Type converterType, Type valueType)
        {
            throw new NotSupportedException(Formatting.Format(Formatting.ConverterCannotWrite, DisplayName(converterType), DisplayName(valueType)));
        }
        #endregion

        #region Reading
        [DoesNotReturn]
        public static void WhenTelemetryTypeNotAdded(Type type)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.TelemetryTypeNotAdded, type.FullName));
        }

        [DoesNotReturn]
        public static void WhenMemberNotOfTelemetry(MemberInfo member, Type telemetryType)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.MemberNotOfTelemetry, member.Name, telemetryType.FullName));
        }

        [DoesNotReturn]
        public static void WhenNoOpenTelemetry()
        {
            throw new InvalidOperationException(Formatting.NoOpenTelemetry);
        }

        [DoesNotReturn]
        public static void WhenUnconvertible(PropertyDefinition property, Type telemetryType, string path, Exception innerException)
        {
            throw new FormatException(Formatting.Format(Formatting.Unconvertible, property.Name, path, telemetryType.FullName, property.MemberType.Name), innerException);
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
        public static void WhenUnknownMember(Type telemetryType, string memberName)
        {
            throw new ArgumentException(Formatting.Format(Formatting.UnknownMember, telemetryType.FullName, memberName), nameof(memberName));
        }

        [DoesNotReturn]
        public static void WhenMemberValueOfOtherType(Type telemetryType, PropertyDefinition property, Type valueType)
        {
            throw new ArgumentException(Formatting.Format(Formatting.MemberValueOfOtherType, telemetryType.FullName, property.Name, property.MemberType.Name, valueType.Name));
        }

        [DoesNotReturn]
        public static void WhenSetAfterEnumerated(Type telemetryType, string memberName)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.SetAfterEnumerated, telemetryType.FullName, memberName));
        }

        [DoesNotReturn]
        public static void WhenCompletionActionOutsideCompletion(Type telemetryType, string action)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.CompletionActionOutsideCompletion, telemetryType.FullName, action));
        }

        [DoesNotReturn]
        public static void WhenCompletionActionTwice(Type telemetryType, string action)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.CompletionActionTwice, telemetryType.FullName, action));
        }

        [DoesNotReturn]
        public static void WhenReleaseNotAllowed(Type telemetryType, bool isOpen, bool isReleased)
        {
            string reason = isOpen ? Formatting.ReleaseOpen : isReleased ? Formatting.ReleaseTwice : Formatting.ReleaseNotKept;
            throw new InvalidOperationException(Formatting.Format(Formatting.ReleaseNotAllowed, telemetryType.FullName, reason));
        }

        [DoesNotReturn]
        public static void WhenReleaseUntilEnd(Type telemetryType)
        {
            throw new InvalidOperationException(Formatting.Format(Formatting.ReleaseUntilEnd, telemetryType.FullName));
        }

        [DoesNotReturn]
        public static void WhenContextLimitReached(int limit, IEnumerable<Reading.ContextTelemetry> kept)
        {
            string perType = string.Join(", ", kept
                .GroupBy(telemetry => telemetry.Definition.Type.Name)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key} ({group.Count()})"));

            throw new Reading.DeserializeContextLimitException(Formatting.Format(Formatting.ContextLimitReached, limit, perType));
        }
        #endregion

        #region Telemetries (FR-006)
        [DoesNotReturn]
        public static void WhenTelemetryNotOfType(Type telemetryType, Type type)
        {
            throw new InvalidCastException(Formatting.Format(Formatting.TelemetryNotOfType, telemetryType.FullName, type.FullName));
        }
        #endregion
    }
}
