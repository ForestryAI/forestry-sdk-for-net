namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A telemetry with date, time and duration members, plain and nullable (spec 005 tasks.md T002)
    /// </summary>
    public sealed class TimedTelemetry : Telemetry
    {
        public DateOnly Date { get; set; }

        public TimeOnly Time { get; set; }

        public TimeSpan Duration { get; set; }

        public DateOnly? MaybeDate { get; set; }

        public TimeSpan? MaybeDuration { get; set; }
    }
}
