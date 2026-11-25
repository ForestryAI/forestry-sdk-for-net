using System.Buffers;
using Forestry.Serialization.Reading;

namespace Forestry.Serialization.Xml
{
    /// <summary>
    /// The XML format's synchronous row enumerator: the core writes the moves, this override reads
    /// (research R2)
    /// </summary>
    internal sealed class XmlRowEnumerator : FormatRowEnumerator
    {
        public XmlRowEnumerator(ReadOnlyMemory<byte> utf8, DeserializeXmlOptions options) : base(utf8, options)
        {
            _reading = new XmlRowReading(options.Association);
        }

        private readonly XmlRowReading _reading;

        protected override ReadStatus Read(ReadOnlySequence<byte> bytes, bool isFinalBlock, out Row? row, out long consumed) =>
            _reading.Read(Builder, bytes, isFinalBlock, out row, out consumed);
    }
}
