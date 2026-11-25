using System.Buffers;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// The string test format (spec 004 tasks.md T012, research R49, R54): a telemetry is its members'
    /// converter output joined by <c>;</c>, one telemetry per line, nothing escaped. A null member
    /// writes the empty null section, or is left out when <see cref="OmitNulls"/> is set
    /// </summary>
    /// <remarks>
    /// Linked into Forestry.Serialization.Xml's tests for the memory test (T020), which have no access
    /// to the core's internals, so it overrides only protected members.
    /// </remarks>
    public sealed class TestingSerializeOptions : SerializeOptions
    {
        /// <summary>
        /// Leave null members out instead of writing the empty null section (R49 <c>OmitNull</c>)
        /// </summary>
        public bool OmitNulls { get; init; }

        /// <summary>
        /// Called after each whole telemetry is written (from <c>WriteEnd</c>), to observe that telemetries
        /// are taken one at a time (T015) or to cancel part-way (T019)
        /// </summary>
        public Action? OnTelemetryWritten { get; init; }

        protected override ObjectConverter CreateObjectConverter(TypeDefinition definition) =>
            new LineObjectConverter(definition, OmitNulls, OnTelemetryWritten);

        /// <summary>
        /// Nothing at the start, a new line at the end; no name section, <c>;</c> between members, and an
        /// empty null section or none
        /// </summary>
        private sealed class LineObjectConverter(TypeDefinition definition, bool omitNulls, Action? onTelemetryWritten) : ObjectConverter(definition)
        {
            private static readonly ReadOnlyMemory<byte> Separator = ";"u8.ToArray();

            protected override void WriteEnd(IBufferWriter<byte> writer)
            {
                writer.GetSpan(1)[0] = (byte)'\n';
                writer.Advance(1);
                onTelemetryWritten?.Invoke();
            }

            protected override MemberSections CreateMemberSections(PropertyDefinition property) =>
                omitNulls
                    ? MemberSections.OmitNull(ReadOnlyMemory<byte>.Empty, Separator)
                    : new MemberSections(ReadOnlyMemory<byte>.Empty, Separator, ReadOnlyMemory<byte>.Empty);
        }
    }
}
