namespace Forestry.Deserialize.Tests.Fixtures
{
    /// <summary>
    /// A row class other row classes derive from: <see cref="Row.Is{T}"/> and <see cref="Row.AsType{T}"/>
    /// treat a derived row as its base, as C#'s <c>is</c> does (US2, research R4)
    /// </summary>
    public class BaseLog : Row
    {
        public int LogKey { get; set; }
    }

    /// <summary>
    /// A row class deriving from <see cref="BaseLog"/>
    /// </summary>
    public sealed class DerivedLog : BaseLog
    {
        public decimal LogVolume { get; set; }
    }
}
