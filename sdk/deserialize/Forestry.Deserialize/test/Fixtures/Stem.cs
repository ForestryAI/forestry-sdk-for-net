namespace Forestry.Deserialize.Tests.Fixtures
{
    /// <summary>
    /// A row class used as a constructor parameter of <see cref="LogWithParameters"/>: a parameter
    /// type that is itself a row, as later slices require (research R5)
    /// </summary>
    public sealed class Stem : Row
    {
        public int StemKey { get; set; }
    }
}
