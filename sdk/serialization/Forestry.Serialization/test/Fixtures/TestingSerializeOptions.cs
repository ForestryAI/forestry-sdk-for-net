using Forestry.Serialization.Definitions;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// The string test format (spec 004 tasks.md T012, research R49, R54): a telemetry is its members'
    /// converter output joined by <c>;</c>, one telemetry per line, nothing escaped. A null member writes
    /// no value, or is left out when <see cref="OmitNulls"/> is set
    /// </summary>
    /// <remarks>
    /// Linked into Forestry.Serialization.Xml's tests for the memory test (T020), which have no access
    /// to the core's internals, so it overrides only protected and public members.
    /// </remarks>
    public sealed class TestingSerializeOptions : SerializeOptions
    {
        /// <summary>
        /// Leave null members out instead of writing them without a value
        /// </summary>
        public bool OmitNulls { get; init; }

        /// <summary>
        /// Called after each whole telemetry is written (from <c>WriteEndTelemetry</c>), to observe that
        /// telemetries are taken one at a time (T015) or to cancel part-way (T019)
        /// </summary>
        public Action? OnTelemetryWritten { get; init; }

        protected override TelemetryWriter CreateWriter() => new LineTelemetryWriter(OmitNulls, OnTelemetryWritten);

        /// <summary>
        /// Nothing at the start, a new line at the end, <c>;</c> before every member but the first written;
        /// values with the writer's defaults (plain UTF-8)
        /// </summary>
        private sealed class LineTelemetryWriter(bool omitNulls, Action? onTelemetryWritten) : TelemetryWriter
        {
            /// <summary>
            /// Whether a member of the current telemetry was written, so the next one needs a separator
            /// </summary>
            private bool _wroteMember;

            protected override void OnReset() => _wroteMember = false;

            public override void WriteStartTelemetry(TypeDefinition definition) => _wroteMember = false;

            public override void WriteEndTelemetry()
            {
                WriteRaw("\n"u8);
                onTelemetryWritten?.Invoke();
            }

            public override void WriteName(PropertyDefinition property)
            {
                if (_wroteMember)
                {
                    WriteRaw(";"u8);
                }

                _wroteMember = true;
            }

            public override void WriteNull(PropertyDefinition property)
            {
                if (!omitNulls)
                {
                    WriteName(property);
                }
            }
        }
    }
}
