using System.Diagnostics.CodeAnalysis;

namespace Forestry.Deserialize.Xml
{
    internal static partial class Throwing
    {
        [DoesNotReturn]
        public static void WhenValueNotPositive(int value, string name)
        {
            throw new InvalidOperationException(Deserialize.Formatting.Format(Formatting.WhenNotPositive, value, name));
        }

    }
}