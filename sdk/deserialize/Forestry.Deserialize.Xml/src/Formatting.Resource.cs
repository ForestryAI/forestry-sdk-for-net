namespace Forestry.Deserialize.Xml
{
    internal static partial class Formatting
    {
        #region invalid operation exception
        internal static string DuplicateRowName => Deserialize.Formatting.GetResourceString(nameof(DuplicateRowName), @"Types '{0}' and '{1}' both read element '{2}': give one a different [Row] name.");

        internal static string DuplicateMemberName => Deserialize.Formatting.GetResourceString(nameof(DuplicateMemberName), @"Type '{0}': members '{1}' and '{2}' both read element '{3}': give one a different [Field] name.");
        #endregion
    }
}
