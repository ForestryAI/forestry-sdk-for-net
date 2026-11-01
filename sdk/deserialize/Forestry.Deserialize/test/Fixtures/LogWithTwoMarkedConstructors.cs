namespace Forestry.Deserialize.Tests.Fixtures
{
    /// <summary>
    /// An <b>invalid</b> row class: two constructors are marked <see cref="RowConstructorAttribute"/>,
    /// but a row type uses exactly one constructor, so at most one may be marked (FR-008, research R5)
    /// </summary>
    /// <remarks>
    /// <c>AttributeUsage(AllowMultiple = false)</c> only stops the attribute appearing twice on the
    /// <em>same</em> constructor; nothing in the compiler stops it on two constructors of one class,
    /// which is why this is checked when definitions are built. The second constructor needs a
    /// parameter only because C# allows one parameterless constructor; the rejection must be for
    /// "more than one marked", not for the parameter.
    /// </remarks>
    public sealed class LogWithTwoMarkedConstructors : Row
    {
        [RowConstructor]
        public LogWithTwoMarkedConstructors()
        {
        }

        [RowConstructor]
        private LogWithTwoMarkedConstructors(int logKey)
        {
            LogKey = logKey;
        }

        public int LogKey { get; set; }
    }
}
