using System.Text;

namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// Turn <see cref="FieldContext"/> spans into strings only when asked (research R15)
    /// </summary>
    public static class FieldContextExtensions
    {
        /// <summary>
        /// UTF-8 bytes as a string
        /// </summary>
        /// <param name="utf8"></param>
        /// <returns></returns>
        public static string ToUtf8String(this ReadOnlySpan<byte> utf8) => Encoding.UTF8.GetString(utf8);
    }
}
