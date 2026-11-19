namespace Forestry.Deserialize.Xml
{
    internal static partial class Formatting
    {
        #region invalid operation exception
        internal static string DuplicateRowName => Deserialize.Formatting.GetResourceString(nameof(DuplicateRowName), @"Types '{0}' and '{1}' both read element '{2}': give one a different [XmlElementName] name.");

        internal static string DuplicateMemberName => Deserialize.Formatting.GetResourceString(nameof(DuplicateMemberName), @"Type '{0}': members '{1}' and '{2}' both read element '{3}': give one a different [XmlElementName] name.");

        internal static string DuplicateAttributeName => Deserialize.Formatting.GetResourceString(nameof(DuplicateAttributeName), @"Type '{0}': members '{1}' and '{2}' both read attribute '{3}': give one a different [XmlAttributeName] name.");

        internal static string MoreThanOneElementText => Deserialize.Formatting.GetResourceString(nameof(MoreThanOneElementText), @"Type '{0}': members '{1}' and '{2}' are both marked [XmlElementText]: a row type has at most one member read from its element's text.");

        internal static string MemberWithMoreThanOneSource => Deserialize.Formatting.GetResourceString(nameof(MemberWithMoreThanOneSource), @"Type '{0}': member '{1}' has more than one source in the document ({2}): a member is read from one place, a child element, an attribute or the element's text.");

        internal static string ElementTextTypeNotAdded => Deserialize.Formatting.GetResourceString(nameof(ElementTextTypeNotAdded), @"An element text is set through the options (ElementText) for '{0}', which is not a row type added to the options.");

        internal static string ElementTextMemberNotFound => Deserialize.Formatting.GetResourceString(nameof(ElementTextMemberNotFound), @"An element text is set through the options (ElementText) for member '{1}' of '{0}', which is not one of its members.");
        #endregion
    }
}
