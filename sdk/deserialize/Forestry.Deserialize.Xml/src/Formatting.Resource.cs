namespace Forestry.Deserialize.Xml
{
    internal static partial class Formatting
    {
        #region xml exception
        internal static string WhenDocumentHasNoRootElement => Deserialize.Formatting.GetResourceString(nameof(WhenDocumentHasNoRootElement), "XML Document has no root element when reading is completed");

        internal static string WhenDocumentHasElementNotEnded => Deserialize.Formatting.GetResourceString(nameof(WhenDocumentHasElementNotEnded), "XML Document element not ended when reading is completed");

        internal static string WhenDocumentHasNoTokens => Deserialize.Formatting.GetResourceString(nameof(WhenDocumentHasNoTokens), "XML Document has no token when reading is completed");

        internal static string WhenNoNameAfterElementStartTerminal => Deserialize.Formatting.GetResourceString(nameof(WhenNoNameAfterElementStartTerminal), @"Value {0} is not a name terminal");
        #endregion

        #region invalid operation exception
        internal static string WhenNotPositive => Deserialize.Formatting.GetResourceString(nameof(WhenNotPositive), @"Value {0} named {1} is not positive");
        #endregion
    }
}