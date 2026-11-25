using System.Buffers;
using Forestry.Serialization.Reading;

namespace Forestry.Serialization.Xml
{
    /// <summary>
    /// The XML format's synchronous telemetry enumerator: the core writes the moves, this override reads
    /// (research R2)
    /// </summary>
    internal sealed class XmlTelemetryEnumerator : FormatTelemetryEnumerator
    {
        public XmlTelemetryEnumerator(ReadOnlyMemory<byte> utf8, DeserializeXmlOptions options) : base(utf8, options)
        {
            _reading = new XmlTelemetryReading(options.Association);
        }

        private readonly XmlTelemetryReading _reading;

        protected override ReadStatus Read(ReadOnlySequence<byte> bytes, bool isFinalBlock, out Telemetry? telemetry, out long consumed) =>
            _reading.Read(Builder, bytes, isFinalBlock, out telemetry, out consumed);
    }
}
