using System.IO.Pipelines;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Reading;

namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// Concrete options for core tests, which reference no format package (C0, C3): definitions,
    /// validation and converter resolution only; reading a document is not supported
    /// </summary>
    public sealed class TestingDeserializeOptions : DeserializeOptions
    {
        /// <summary>
        /// Converters standing in for a format package's defaults (spec 005 T011: a member-aware factory)
        /// </summary>
        public IReadOnlyList<Converter> Format { get; init; } = [];

        protected override IEnumerable<Converter> FormatConverters => Format;

        protected internal override FormatTelemetryEnumerator CreateTelemetryEnumerator(ReadOnlyMemory<byte> utf8) =>
            throw new NotSupportedException("Core tests read no document");

        protected internal override FormatTelemetryAsyncEnumerator CreateTelemetryAsyncEnumerator(PipeReader utf8, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Core tests read no document");
    }
}
