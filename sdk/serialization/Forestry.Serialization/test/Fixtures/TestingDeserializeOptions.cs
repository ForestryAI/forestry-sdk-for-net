using System.IO.Pipelines;
using Forestry.Serialization.Reading;

namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// Concrete options for core tests, which reference no format package (C0, C3): definitions,
    /// validation and converter resolution only; reading a document is not supported
    /// </summary>
    public sealed class TestingDeserializeOptions : DeserializeOptions
    {
        protected internal override FormatRowEnumerator CreateRowEnumerator(ReadOnlyMemory<byte> utf8) =>
            throw new NotSupportedException("Core tests read no document");

        protected internal override FormatRowAsyncEnumerator CreateRowAsyncEnumerator(PipeReader utf8, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Core tests read no document");
    }
}
