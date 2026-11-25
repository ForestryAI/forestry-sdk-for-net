namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// A telemetry class other telemetry classes derive from: <see cref="Telemetry.Is{T}"/> and <see cref="Telemetry.AsType{T}"/>
    /// treat a derived telemetry as its base, as C#'s <c>is</c> does (US2, research R4)
    /// </summary>
    public class BaseLog : Telemetry
    {
        public int LogKey { get; set; }
    }

    /// <summary>
    /// A telemetry class deriving from <see cref="BaseLog"/>
    /// </summary>
    public sealed class DerivedLog : BaseLog
    {
        public decimal LogVolume { get; set; }
    }
}
