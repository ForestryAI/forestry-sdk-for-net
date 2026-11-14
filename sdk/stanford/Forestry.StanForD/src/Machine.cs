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

        /// <summary>
        /// The machine's external reference, from StanForD <c>Machine/MachineUserID</c>
        /// </summary>
        public string MachineUserID { get; set; } = string.Empty;

        /// <summary>
        /// The logging contractor's business identity, from StanForD
        /// <c>Machine/LoggingContractor/BusinessID</c>; set by a policy from a <see cref="LoggingContractor"/>
        /// </summary>
        public string LoggingContractorBusinessID { get; set; } = string.Empty;
    }
}
