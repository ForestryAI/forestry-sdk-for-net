namespace Forestry.Deserialize.Xml
{
    internal static partial class Formatting
    {
        #region invalid operation exception
        internal static string DuplicateRowName => Deserialize.Formatting.GetResourceString(nameof(DuplicateRowName), @"Types '{0}' and '{1}' both read element '{2}': give one a different [Row] name.");

        internal static string DuplicateMemberName => Deserialize.Formatting.GetResourceString(nameof(DuplicateMemberName), @"Type '{0}': members '{1}' and '{2}' both read element '{3}': give one a different [Field] name.");

        internal static string DuplicateAttributeName => Deserialize.Formatting.GetResourceString(nameof(DuplicateAttributeName), @"Type '{0}': members '{1}' and '{2}' both read attribute '{3}': give one a different [XmlAttributeValue] name.");

        internal static string MoreThanOneTextValue => Deserialize.Formatting.GetResourceString(nameof(MoreThanOneTextValue), @"Type '{0}': members '{1}' and '{2}' are both marked [XmlTextValue]: a row type has at most one member read from its element's text.");

        internal static string TextValueTypeNotAdded => Deserialize.Formatting.GetResourceString(nameof(TextValueTypeNotAdded), @"A text value is set through the options for '{0}', which is not a row type added to the options.");

        internal static string TextValueMemberNotFound => Deserialize.Formatting.GetResourceString(nameof(TextValueMemberNotFound), @"A text value is set through the options for member '{1}' of '{0}', which is not one of its members.");
        #endregion
    }
}
