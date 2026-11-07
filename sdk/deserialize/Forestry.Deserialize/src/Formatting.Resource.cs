namespace Forestry.Deserialize.Immutable
{
    internal static class Formatting {}
}

namespace Forestry.Deserialize
{
    internal static partial class Formatting
    {
        internal static System.Resources.ResourceManager ResourceManager => s_resourceManager ??= new System.Resources.ResourceManager(typeof(Immutable.Formatting));

        private static System.Resources.ResourceManager s_resourceManager = null!;

        internal static string DeserializeOptionsIsReadOnly => GetResourceString(nameof(DeserializeOptionsIsReadOnly), @"Deserialize options instance is read-only i.e. locked for changes.");

        internal static string WhenNotSingularAttribute => GetResourceString(nameof(WhenNotSingularAttribute), @"Attribute [name: '{0}',target: '{1}'] is not singular");

        internal static string PipeReaderCanceled => GetResourceString(nameof(PipeReaderCanceled), @"PipeReader was canceled");

        internal static string NoRowTypes => GetResourceString(nameof(NoRowTypes), @"No row type was added to the options: add one with Rows.Add<T>().");

        internal static string NotARowType => GetResourceString(nameof(NotARowType), @"Type '{0}' is not a row type: it must be a non-abstract class extending Row.");

        internal static string NoConstructorChosen => GetResourceString(nameof(NoConstructorChosen), @"Type '{0}' has no constructor that can be chosen: keep exactly one public constructor or mark one [RowConstructor].");

        internal static string MoreThanOneRowConstructor => GetResourceString(nameof(MoreThanOneRowConstructor), @"Type '{0}' has more than one constructor marked [RowConstructor]: a row type uses exactly one constructor.");

        internal static string ConstructorHasParameters => GetResourceString(nameof(ConstructorHasParameters), @"Type '{0}': its chosen constructor has parameters, which this version does not support.");

        internal static string MemberPolicyNotSetOnly => GetResourceString(nameof(MemberPolicyNotSetOnly), @"Type '{0}': the policy '{1}' on member '{2}' may only override Set; attach it to the type instead.");

        internal static string DefinitionPolicyOnMember => GetResourceString(nameof(DefinitionPolicyOnMember), @"Type '{0}': the definition policy '{1}' cannot be attached to member '{2}'; attach it to the type or options-wide.");

        internal static string PolicyAttributesAmbiguous => GetResourceString(nameof(PolicyAttributesAmbiguous), @"'{0}' has more than one policy attribute overriding {1}: attributes have no order, so add them through the options instead.");

        internal static string PolicyTypeNotAdded => GetResourceString(nameof(PolicyTypeNotAdded), @"A policy is registered for '{0}', which is not a row type added to the options.");

        internal static string PolicyMemberNotFound => GetResourceString(nameof(PolicyMemberNotFound), @"A policy is registered for member '{1}' of '{0}', which is not one of its members.");

        internal static string EnumerationRunning => GetResourceString(nameof(EnumerationRunning), @"These options have enumeration policies and are already used by a running enumeration; policies keep state for one enumeration at a time, so use another options instance for a concurrent deserialization.");

        internal static string MemberWithoutConverter => GetResourceString(nameof(MemberWithoutConverter), @"Type '{0}': member '{1}' of type '{2}' has no converter; add one to the options.");

        internal static string NoConverter => GetResourceString(nameof(NoConverter), @"No converter handles type '{0}'.");

        internal static string RowTypeNotAdded => GetResourceString(nameof(RowTypeNotAdded), @"Type '{0}' was not added to the options as a row type.");

        internal static string MemberNotOfRow => GetResourceString(nameof(MemberNotOfRow), @"Member '{0}' is not a member of the open row type '{1}'.");

        internal static string NoOpenRow => GetResourceString(nameof(NoOpenRow), @"No row is open.");

        internal static string Unconvertible => GetResourceString(nameof(Unconvertible), @"Member '{0}' at '{1}' of type '{2}' cannot be converted to {3}.");

        internal static string NotParsable => GetResourceString(nameof(NotParsable), @"The value is not a {0}.");

        internal static string EnumeratedTwice => GetResourceString(nameof(EnumeratedTwice), @"The rows of a deserialization can be enumerated once: deserialize again to read the document again.");

        internal static string PartialAtFinalBlock => GetResourceString(nameof(PartialAtFinalBlock), @"The media asked for more bytes after the final block.");

        internal static string RowNotOfType => GetResourceString(nameof(RowNotOfType), @"A row of type '{0}' is not a '{1}'.");
    }
}
