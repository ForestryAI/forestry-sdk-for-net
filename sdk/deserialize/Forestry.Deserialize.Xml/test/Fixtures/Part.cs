namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A part that may contain parts of its own: a row type nested in itself (spec 002, tasks.md T032, T039)
    /// </summary>
    public sealed class Part : Row
    {
        public int PartNumber { get; set; }
    }
}
