namespace Forestry.Deserialize.Tests.Fixtures
{
    /// <summary>
    /// A class not extending <see cref="Row"/>: adding it with <c>Rows.Add(params Type[])</c> throws (FR-008, R12)
    /// </summary>
    public sealed class NotARow
    {
        public int LogKey { get; set; }
    }
}
