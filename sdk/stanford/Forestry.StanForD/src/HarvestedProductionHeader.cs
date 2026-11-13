using Forestry.Deserialize;

namespace Forestry.StanForD {
    /// <summary>
    /// The header of a Harvesting document, from StanForD <c>HarvestedProductionHeader</c>; never comes
    /// out, but gives every stem and log its partition time
    /// </summary>
    public sealed class HarvestedProductionHeader : Row
    {
        /// <summary>
        /// When the harvesting machine created the document, with the machine's local offset, from
        /// StanForD <c>HarvestedProductionHeader/CreationDate</c>
        /// </summary>
        public DateTimeOffset CreationDate { get; set; }
    }
}
