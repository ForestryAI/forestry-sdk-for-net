using Forestry.Serialization.Converters;

namespace Forestry.Serialization.Xml.Tests.Fixtures
{
    /// <summary>
    /// An enum read from XML as a name or, with <see cref="XmlEnumFormAttribute"/>, as a number
    /// </summary>
    public enum Shade
    {
        Light = 0,

        Dark = 1
    }

    /// <summary>
    /// Serialization spec 005, User Story 4 (#152): a telemetry with an enum member read as a name (no
    /// attribute), the same enum read as a number, and a <see cref="TimeSpan"/> read as an XML Schema
    /// duration (tasks.md T058)
    /// </summary>
    [XmlElementName("Log")]
    public sealed class EnumAndDurationTelemetry : Telemetry
    {
        public int LogKey { get; set; }

        public Shade Shade { get; set; }

        [XmlEnumForm(EnumForm.Number)]
        public Shade ShadeNumber { get; set; }

        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// A developer converter for <see cref="Shade"/>, recognisable as the effective converter over the
    /// XML enum converter (US4-3, FR-002e): it reads every text as <see cref="Shade.Light"/>
    /// </summary>
    public sealed class MarkingShadeConverter : Converter<Shade>
    {
        public override Shade Read(ReadOnlySpan<byte> utf8) => Shade.Light;
    }
}
