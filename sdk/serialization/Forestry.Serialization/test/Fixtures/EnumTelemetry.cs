namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A plain enum (spec 005 tasks.md T002)
    /// </summary>
    public enum Shade
    {
        Light,
        Dark
    }

    /// <summary>
    /// An enum whose members declare their names (research R62)
    /// </summary>
    public enum Quality
    {
        [EnumMemberName("Quality A")]
        A,
        [EnumMemberName("Quality B")]
        B
    }

    /// <summary>
    /// A flags enum: combinations are written and read as names joined with ", " (research R59)
    /// </summary>
    [Flags]
    public enum Parts
    {
        None = 0,
        Top = 1,
        Butt = 2
    }

    /// <summary>
    /// An enum whose underlying value does not fit a <see cref="long"/> (research R59)
    /// </summary>
    public enum Wide : ulong
    {
        Max = ulong.MaxValue
    }

    /// <summary>
    /// Two members whose declared names differ only in case: the options fail at lock (research R62)
    /// </summary>
    public enum Clash
    {
        [EnumMemberName("x")]
        One,
        [EnumMemberName("X")]
        Two
    }

    /// <summary>
    /// A telemetry with enum members; the members ending in <c>Number</c> are the ones
    /// <see cref="MemberAwareEnumFactory"/> creates number-form converters for (research R58)
    /// </summary>
    public sealed class EnumTelemetry : Telemetry
    {
        public Shade Shade { get; set; }

        public Shade? MaybeShade { get; set; }

        public Quality Quality { get; set; }

        public Parts Parts { get; set; }

        public Shade ShadeNumber { get; set; }

        public Shade? MaybeShadeNumber { get; set; }
    }

    /// <summary>
    /// A telemetry whose enum has clashing member names
    /// </summary>
    public sealed class ClashTelemetry : Telemetry
    {
        public Clash Clash { get; set; }
    }
}
