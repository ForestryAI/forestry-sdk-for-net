namespace Forestry.Deserialize.Xml
{
    internal static partial class Formatting
    {
        #region xml exception
        internal static string WhenNoNameAfterElementStartTerminal => Deserialize.Formatting.GetResourceString(nameof(WhenNoNameAfterElementStartTerminal), @"Value {0} is not a name terminal");
        #endregion

        #region invalid operation exception
        internal static string WhenNotPositive => Deserialize.Formatting.GetResourceString(nameof(WhenNotPositive), @"Value {0} named {1} is not positive");
        #endregion
    }
}