using System.Text;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Converters.Primitive
{
    /// <summary>
    /// Primitive converter for text
    /// </summary>
    internal sealed class StringConverter : Converter<string>
    {
        public override string Read(ReadOnlySpan<byte> utf8) => Encoding.UTF8.GetString(utf8);

        public override void Write(string value, TelemetryWriter writer) => writer.WriteString(value);
    }
}
