namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A telemetry class whose only public constructor has a parameter (FR-008, this slice)
    /// </summary>
    /// <remarks>
    /// The parameter is deliberately a telemetry type (<see cref="Stem"/>), so the only reason to reject
    /// this class in spec 001 is that its constructor has a parameter at all. From spec 002/003 a
    /// telemetry-typed parameter is allowed (an ancestor from the open-telemetry stack, research R5) and its type
    /// is added automatically (R12); this class is then accepted, and a parameter that is not a telemetry
    /// (e.g. <c>int</c>) is the case that is rejected.
    /// </remarks>
    public sealed class LogWithParameters : Telemetry
    {
        public LogWithParameters(Stem stem)
        {
            StemKey = stem.StemKey;
        }

        public int StemKey { get; set; }

        public int LogKey { get; set; }
    }
}
