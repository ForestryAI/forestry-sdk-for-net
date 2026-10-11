namespace Forestry.Deserialize.Xml
{
    internal static partial class Formatting
    {
        #region xml exception
        internal static string Incomplete => Deserialize.Formatting.GetResourceString(nameof(Incomplete), @"Malformed markup: reading completed inside {0}{1}");

        internal static string Expected => Deserialize.Formatting.GetResourceString(nameof(Expected), @"Malformed markup: expected {0}{1}");

        internal static string Name => Deserialize.Formatting.GetResourceString(nameof(Name), @"Malformed markup: {0}{1} breaks the naming rules");

        internal static string Order => Deserialize.Formatting.GetResourceString(nameof(Order), @"Malformed markup: {0}{1} is out of order");

        internal static string Unique => Deserialize.Formatting.GetResourceString(nameof(Unique), @"Malformed markup: {0}{1} is repeated");
        #endregion

        #region unsupported
        internal static string WhenNameUnsupportedCharacter => Deserialize.Formatting.GetResourceString(nameof(WhenNameUnsupportedCharacter), @"Unsupported markup: a name non-terminal contains a character outside ASCII");
        #endregion

        #region invalid operation exception
        internal static string WhenNotPositive => Deserialize.Formatting.GetResourceString(nameof(WhenNotPositive), @"Value {0} named {1} is not positive");
        #endregion
    }
}
