using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Tests.Fixtures
{
    /// <summary>
    /// Concrete options for core tests, which reference no media package (C0, C3): definitions,
    /// validation and converter resolution only; reading a document is not supported
    /// </summary>
    public sealed class TestingDeserializeOptions : DeserializeOptions
    {
        protected internal override RowEnumerator CreateRowEnumerator(ReadOnlyMemory<byte> utf8) =>
            throw new NotSupportedException("Core tests read no document");
    }
}
