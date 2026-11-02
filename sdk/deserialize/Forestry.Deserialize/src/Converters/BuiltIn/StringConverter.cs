using System.Text;

namespace Forestry.Deserialize.Converters.BuiltIn
{
    /// <summary>
    /// Built-in converter for text
    /// </summary>
    internal sealed class StringConverter : Converter<string>
    {
        public override string Convert(ReadOnlySpan<byte> utf8) => Encoding.UTF8.GetString(utf8);
    }
}
