namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// A wrapper inside an order, added as a row type that is not enumerated so a policy can copy its
    /// member onto the order (spec 002, tasks.md T040, research R29 option A)
    /// </summary>
    public sealed class Details : Row
    {
        public int OrderNumber { get; set; }
    }
}
