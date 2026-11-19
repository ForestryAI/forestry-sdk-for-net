using System.Text;

namespace Forestry.Deserialize
{
    /// <summary>
    /// UTF-8 spans, such as <see cref="ReadingInformation.Name"/> and <see cref="ReadingInformation.Path"/>,
    /// as strings only when asked (spec 001 research R15; named after what it extends in spec 003, R36)
    /// </summary>
    public static class Utf8Extensions
    {
        /// <summary>
        /// UTF-8 bytes as a string
        /// </summary>
        /// <param name="utf8"></param>
        /// <returns></returns>
        public static string ToUtf8String(this ReadOnlySpan<byte> utf8) => Encoding.UTF8.GetString(utf8);
    }
}
