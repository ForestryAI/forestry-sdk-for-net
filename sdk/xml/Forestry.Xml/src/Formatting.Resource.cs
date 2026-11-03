namespace Forestry.Xml.Immutable
{
    internal static class Formatting {}
}

namespace Forestry.Xml
{
    internal static partial class Formatting
    {
        internal static System.Resources.ResourceManager ResourceManager => s_resourceManager ??= new System.Resources.ResourceManager(typeof(Immutable.Formatting));

        private static System.Resources.ResourceManager s_resourceManager = null!;

        #region xml exception
        internal static string Incomplete => GetResourceString(nameof(Incomplete), @"Malformed markup: reading completed inside {0}{1}");

        internal static string Expected => GetResourceString(nameof(Expected), @"Malformed markup: expected {0}{1}");

        internal static string Name => GetResourceString(nameof(Name), @"Malformed markup: {0}{1} breaks the naming rules");

        internal static string Order => GetResourceString(nameof(Order), @"Malformed markup: {0}{1} is out of order");

        internal static string Unique => GetResourceString(nameof(Unique), @"Malformed markup: {0}{1} is repeated");

        internal static string Unexpected => GetResourceString(nameof(Unexpected), @"Malformed markup: {0} starts no markup allowed here");
        #endregion

        #region unsupported
        internal static string WhenNameUnsupportedCharacter => GetResourceString(nameof(WhenNameUnsupportedCharacter), @"Unsupported markup: a name non-terminal contains a character outside ASCII");
        #endregion

        #region invalid operation exception
        internal static string WhenNotPositive => GetResourceString(nameof(WhenNotPositive), @"Value {0} named {1} is not positive");
        #endregion
    }
}
