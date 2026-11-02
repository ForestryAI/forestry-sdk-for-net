namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A <b>valid</b> row class with one constructor marked <see cref="RowConstructorAttribute"/>:
    /// the marked one is used, whatever its accessibility (FR-008, research R5 rule 1)
    /// </summary>
    /// <remarks>
    /// The public constructor has a parameter and is not marked, so it must not be chosen (it would
    /// be rejected in this slice); the marked one is private and parameterless. Which constructor ran
    /// is told by a method, not a member, so the document cannot set it.
    /// </remarks>
    [Row("Log")]
    public sealed class LogWithMarkedConstructor : Row
    {
        private readonly bool _constructedByMarked;

        public LogWithMarkedConstructor(int logKey)
        {
            LogKey = logKey;
        }

        [RowConstructor]
        private LogWithMarkedConstructor()
        {
            _constructedByMarked = true;
        }

        public int LogKey { get; set; }

        public decimal LogVolume { get; set; }

        public bool WasConstructedByMarked() => _constructedByMarked;
    }
}
