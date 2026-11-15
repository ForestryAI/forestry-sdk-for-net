using Forestry.Deserialize;

namespace Forestry.StanForD {
    /// <summary>
    /// A harvesting object loaded into the machine, from StanForD <c>ObjectDefinition</c>; an
    /// object belongs to a contract, and stems reference it through their object key
    /// </summary>
    public sealed class ObjectDefinition : Row
    {
        #region identification
        /// <summary>
        /// Global unique identifier: "{machine key}-{object key}", where the machine key is
        /// the GUID without dashes (format "N"), e.g. "20767727ce544fba8f05cfbc218b4300-283"
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Object identifier, from StanForD <c>ObjectDefinition/ObjectKey</c>; a running number
        /// created by the machine
        /// </summary>
        public int ObjectKey { get; set; }

        /// <summary>
        /// Machine identifier, from StanForD <c>Machine/MachineKey</c> (a GUID); not the
        /// <c>MachineUserID</c>, which is an external reference
        /// </summary>
        public Guid MachineKey { get; set; }

        /// <summary>
        /// Descriptive name of the harvesting object, from StanForD
        /// <c>ObjectDefinition/ObjectName</c>
        /// </summary>
        public string ObjectName { get; set; } = string.Empty;

        /// <summary>
        /// The object's external reference, from StanForD <c>ObjectDefinition/ObjectUserID</c>
        /// </summary>
        public string ObjectUserID { get; set; } = string.Empty;
        #endregion

        #region partitioning
        /// <summary>
        /// Contract number the object belongs to, from StanForD
        /// <c>ObjectDefinition/ContractNumber</c>; copied onto stems and logs for partitioning
        /// </summary>
        public string ContractNumber { get; set; } = string.Empty;
        #endregion

        #region dimensions
        #endregion
    }
}
