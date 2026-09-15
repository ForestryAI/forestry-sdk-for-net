using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Forestry.Deserialize.Xml.Reading
{
    /// <summary>
    /// Typed value getters
    /// </summary>
    public ref partial struct Utf8XmlReader
    {
        public static readonly UTF8Encoding Encoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        
    }
}