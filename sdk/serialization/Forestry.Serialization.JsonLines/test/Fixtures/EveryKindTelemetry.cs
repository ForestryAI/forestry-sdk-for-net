using Forestry.Serialization.Converters;

namespace Forestry.Serialization.JsonLines.Tests.Fixtures
{
    /// <summary>
    /// A plain enum (spec 005 tasks.md T036)
    /// </summary>
    public enum Shade
    {
        Light,
        Dark
    }

    /// <summary>
    /// A telemetry with a member of every flat kind, nullable members, and two members of one enum type, one
    /// marked for numbers (spec 005 tasks.md T036, FR-011, FR-002d)
    /// </summary>
    public sealed class EveryKindTelemetry : Telemetry
    {
        public string? Text { get; set; }

        public bool Flag { get; set; }

        public int Count { get; set; }

        public long Big { get; set; }

        public double Ratio { get; set; }

        public decimal Volume { get; set; }

        public Guid Key { get; set; }

        public DateTime At { get; set; }

        public DateTimeOffset AtOffset { get; set; }

        public DateOnly Day { get; set; }

        public TimeOnly Time { get; set; }

        public TimeSpan Duration { get; set; }

        public Shade Shade { get; set; }

        [JsonLinesEnumForm(EnumForm.Number)]
        public Shade ShadeNumber { get; set; }

        public int? MaybeCount { get; set; }

        public DateOnly? MaybeDay { get; set; }

        /// <summary>
        /// Every member with a value
        /// </summary>
        public static EveryKindTelemetry WithValues(int count = 7) => new()
        {
            Text = "Gran",
            Flag = true,
            Count = count,
            Big = long.MaxValue,
            Ratio = 0.1,
            Volume = 123456789.123456789123456789m,
            Key = new Guid("3cdb35d7-6108-4b19-8d30-d6dfd2c44e9b"),
            At = new DateTime(2026, 1, 14, 17, 14, 5, 123, DateTimeKind.Utc),
            AtOffset = new DateTimeOffset(2026, 1, 14, 17, 14, 5, 123, TimeSpan.FromHours(1)),
            Day = new DateOnly(2026, 11, 27),
            Time = new TimeOnly(13, 20, 0, 500),
            Duration = new TimeSpan(1, 2, 3, 4, 500),
            Shade = Shade.Dark,
            ShadeNumber = Shade.Dark,
            MaybeCount = 5,
            MaybeDay = new DateOnly(2026, 11, 28)
        };

        /// <summary>
        /// The nullable members without a value
        /// </summary>
        public static EveryKindTelemetry WithNulls()
        {
            EveryKindTelemetry telemetry = WithValues();
            telemetry.Text = null;
            telemetry.MaybeCount = null;
            telemetry.MaybeDay = null;
            return telemetry;
        }
    }

    /// <summary>
    /// A telemetry with one <see cref="double"/>, for the non-finite values (T043)
    /// </summary>
    public sealed class RatioTelemetry : Telemetry
    {
        public double Ratio { get; set; }
    }

    /// <summary>
    /// A telemetry with one <see cref="string"/>, for escaping (T039)
    /// </summary>
    public sealed class TextTelemetry : Telemetry
    {
        public string? Text { get; set; }
    }
}
