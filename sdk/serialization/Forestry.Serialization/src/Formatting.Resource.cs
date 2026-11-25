namespace Forestry.Serialization.Immutable
{
    internal static class Formatting {}
}

namespace Forestry.Serialization
{
    internal static partial class Formatting
    {
        internal static System.Resources.ResourceManager ResourceManager => s_resourceManager ??= new System.Resources.ResourceManager(typeof(Immutable.Formatting));

        private static System.Resources.ResourceManager s_resourceManager = null!;

        internal static string DeserializeOptionsIsReadOnly => GetResourceString(nameof(DeserializeOptionsIsReadOnly), @"Deserialize options instance is read-only i.e. locked for changes.");

        internal static string WhenNotSingularAttribute => GetResourceString(nameof(WhenNotSingularAttribute), @"Attribute [name: '{0}',target: '{1}'] is not singular");

        internal static string PipeReaderCanceled => GetResourceString(nameof(PipeReaderCanceled), @"PipeReader was canceled");

        internal static string NoTelemetryTypes => GetResourceString(nameof(NoTelemetryTypes), @"No telemetry type was added to the options: add one with Telemetries.Add<T>().");

        internal static string NotATelemetryType => GetResourceString(nameof(NotATelemetryType), @"Type '{0}' is not a telemetry type: it must be a non-abstract class extending Telemetry.");

        internal static string NoConstructorChosen => GetResourceString(nameof(NoConstructorChosen), @"Type '{0}' has no constructor that can be chosen: keep exactly one public constructor or mark one [TelemetryConstructor].");

        internal static string MoreThanOneTelemetryConstructor => GetResourceString(nameof(MoreThanOneTelemetryConstructor), @"Type '{0}' has more than one constructor marked [TelemetryConstructor]: a telemetry type uses exactly one constructor.");

        internal static string ConstructorHasParameters => GetResourceString(nameof(ConstructorHasParameters), @"Type '{0}': its chosen constructor has parameters, which this version does not support.");

        internal static string MemberPolicyNotSetOnly => GetResourceString(nameof(MemberPolicyNotSetOnly), @"Type '{0}': the policy '{1}' on member '{2}' may only override Set; attach it to the type instead.");

        internal static string DefinitionPolicyOnMember => GetResourceString(nameof(DefinitionPolicyOnMember), @"Type '{0}': the definition policy '{1}' cannot be attached to member '{2}'; attach it to the type or options-wide.");

        internal static string PolicyAttributesAmbiguous => GetResourceString(nameof(PolicyAttributesAmbiguous), @"'{0}' has more than one policy attribute overriding {1}: attributes have no order, so add them through the options instead.");

        internal static string PolicyTypeNotAdded => GetResourceString(nameof(PolicyTypeNotAdded), @"A policy is registered for '{0}', which is not a telemetry type added to the options.");

        internal static string PolicyMemberNotFound => GetResourceString(nameof(PolicyMemberNotFound), @"A policy is registered for member '{1}' of '{0}', which is not one of its members.");

        internal static string EnumerationRunning => GetResourceString(nameof(EnumerationRunning), @"These options have enumeration policies and are already used by a running enumeration; policies keep state for one enumeration at a time, so use another options instance for a concurrent deserialization.");

        internal static string TelemetryTypeMembersInvalid => GetResourceString(nameof(TelemetryTypeMembersInvalid), @"Type '{0}' cannot be a telemetry type:{1}");

        internal static string MemberTypeIsNestedStructure => GetResourceString(nameof(MemberTypeIsNestedStructure), @"member '{0}' of type '{1}' is a nested structure, but every member of a telemetry must be a single value (a primitive, a string, an enum, or a struct that formats to UTF-8 and parses from text). Copy its values onto the telemetry as single values, or make it its own telemetry type.");

        internal static string MemberTypeNotUtf8Formattable => GetResourceString(nameof(MemberTypeNotUtf8Formattable), @"member '{0}' of type '{1}' formats only to chars: implement IUtf8SpanFormattable, so telemetries can be written without a string per value.");

        internal static string MemberWithoutConverter => GetResourceString(nameof(MemberWithoutConverter), @"member '{0}' of type '{1}' has no converter; add one to the options.");

        internal static string NoConverter => GetResourceString(nameof(NoConverter), @"No converter handles type '{0}'.");

        internal static string TelemetryTypeNotAdded => GetResourceString(nameof(TelemetryTypeNotAdded), @"Type '{0}' was not added to the options as a telemetry type.");

        internal static string MemberNotOfTelemetry => GetResourceString(nameof(MemberNotOfTelemetry), @"Member '{0}' is not a member of the open telemetry type '{1}'.");

        internal static string NoOpenTelemetry => GetResourceString(nameof(NoOpenTelemetry), @"No telemetry is open.");

        internal static string Unconvertible => GetResourceString(nameof(Unconvertible), @"Member '{0}' at '{1}' of type '{2}' cannot be converted to {3}.");

        internal static string NotParsable => GetResourceString(nameof(NotParsable), @"The value is not a {0}.");

        internal static string EnumeratedTwice => GetResourceString(nameof(EnumeratedTwice), @"The telemetries of a deserialization can be enumerated once: deserialize again to read the document again.");

        internal static string PartialAtFinalBlock => GetResourceString(nameof(PartialAtFinalBlock), @"The format asked for more bytes after the final block.");

        internal static string UnknownMember => GetResourceString(nameof(UnknownMember), @"Type '{0}' has no member '{1}'.");

        internal static string MemberValueOfOtherType => GetResourceString(nameof(MemberValueOfOtherType), @"Type '{0}': member '{1}' is a {2}, not a {3}.");

        internal static string SetAfterEnumerated => GetResourceString(nameof(SetAfterEnumerated), @"Type '{0}': member '{1}' cannot be set, the telemetry came out already and no longer changes.");

        internal static string CompletionActionOutsideCompletion => GetResourceString(nameof(CompletionActionOutsideCompletion), @"Type '{0}': {1} is only allowed on the telemetry being completed, during its own completion.");

        internal static string CompletionActionTwice => GetResourceString(nameof(CompletionActionTwice), @"Type '{0}': {1} was already called for this telemetry; each completion action is taken at most once, by one policy.");

        internal static string ReleaseNotAllowed => GetResourceString(nameof(ReleaseNotAllowed), @"Type '{0}': Release is not allowed, {1}.");

        internal static string ReleaseOpen => GetResourceString(nameof(ReleaseOpen), @"the telemetry is still open");

        internal static string ReleaseTwice => GetResourceString(nameof(ReleaseTwice), @"the telemetry was released already");

        internal static string ReleaseNotKept => GetResourceString(nameof(ReleaseNotKept), @"the telemetry is not kept");

        internal static string ReleaseUntilEnd => GetResourceString(nameof(ReleaseUntilEnd), @"Type '{0}': Release is not allowed, the telemetry is kept until the end of the enumeration.");

        internal static string ContextLimitReached => GetResourceString(nameof(ContextLimitReached), @"The deserialization context keeps more telemetries than MaxContextTelemetries allows ({0}). Kept: {1}. Raise MaxContextTelemetries on the options if the document needs more.");

        internal static string TelemetryNotOfType => GetResourceString(nameof(TelemetryNotOfType), @"A telemetry of type '{0}' is not a '{1}'.");
    }
}
