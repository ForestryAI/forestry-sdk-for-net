using Forestry.Deserialize;

namespace Forestry.StanForD {
    /// <summary>
    /// The harvesting machine producing the document, from StanForD <c>Machine</c>.  Its key makes
    /// stem, log and definition keys globally unique.
    /// </summary>
    public sealed class Machine : Row
    {
        /// <summary>
        /// Machine identifier, from StanForD <c>Machine/MachineKey</c> (a GUID); not the
        /// <c>MachineUserID</c>, which is an external reference
        /// </summary>
        public Guid MachineKey { get; set; }
    }
}
