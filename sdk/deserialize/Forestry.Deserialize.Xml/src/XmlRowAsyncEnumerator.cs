using System.Buffers;
using System.IO.Pipelines;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Xml
{
    /// <summary>
    /// The XML media's asynchronous row enumerator: the core writes the moves over the pipe, this
    /// override reads with the same reading as the synchronous one (research R2)
    /// </summary>
    internal sealed class XmlRowAsyncEnumerator : RowAsyncEnumerator
    {
        public XmlRowAsyncEnumerator(PipeReader utf8, DeserializeXmlOptions options, CancellationToken cancellationToken)
            : base(utf8, options, cancellationToken)
        {
            _reading = new XmlRowReading(options.Association);
        }

        private readonly XmlRowReading _reading;

        protected override RowState Read(ReadOnlySequence<byte> bytes, bool isFinalBlock, out Row? row, out long consumed) =>
            _reading.Read(Pipelines, bytes, isFinalBlock, out row, out consumed);
    }
}
