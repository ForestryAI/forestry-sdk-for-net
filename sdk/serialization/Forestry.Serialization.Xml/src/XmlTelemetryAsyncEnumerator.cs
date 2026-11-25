using System.Buffers;
using System.IO.Pipelines;
using Forestry.Serialization.Reading;

namespace Forestry.Serialization.Xml
{
    /// <summary>
    /// The XML format's asynchronous telemetry enumerator: the core writes the moves over the pipe, this
    /// override reads with the same reading as the synchronous one (research R2)
    /// </summary>
    internal sealed class XmlTelemetryAsyncEnumerator : FormatTelemetryAsyncEnumerator
    {
        public XmlTelemetryAsyncEnumerator(PipeReader utf8, DeserializeXmlOptions options, CancellationToken cancellationToken)
            : base(utf8, options, cancellationToken)
        {
            _reading = new XmlTelemetryReading(options.Association);
        }

        private readonly XmlTelemetryReading _reading;

        protected override ReadStatus Read(ReadOnlySequence<byte> bytes, bool isFinalBlock, out Telemetry? telemetry, out long consumed) =>
            _reading.Read(Builder, bytes, isFinalBlock, out telemetry, out consumed);
    }
}
