using Forestry.Serialization;

namespace Forestry.StanForD {
    /// <summary>
    /// A species group (tree kind) loaded into the machine, from StanForD
    /// <c>SpeciesGroupDefinition</c>; stems reference it through their species group key
    /// </summary>
    /// <remarks>
    /// The species group key comes last in the definition, after its grades, so the definition
    /// is only complete at its end
    /// </remarks>
    public sealed class SpeciesGroupDefinition : Telemetry
    {
        #region identification
        /// <summary>
        /// Global unique identifier: "{machine key}-{species group key}", where the machine key is
        /// the GUID without dashes (format "N"), e.g. "20767727ce544fba8f05cfbc218b4300-221"
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Species group identifier, from StanForD <c>SpeciesGroupDefinition/SpeciesGroupKey</c>;
        /// a running number created by the machine
        /// </summary>
        public int SpeciesGroupKey { get; set; }

        /// <summary>
        /// Machine identifier, from StanForD <c>Machine/MachineKey</c> (a GUID); not the
        /// <c>MachineUserID</c>, which is an external reference
        /// </summary>
        public Guid MachineKey { get; set; }
        #endregion

        #region dimensions
        /// <summary>
        /// Species group name e.g. "GRAN" (spruce in Swedish), from StanForD
        /// <c>SpeciesGroupDefinition/SpeciesGroupName</c>
        /// </summary>
        public string SpeciesGroupName { get; set; } = string.Empty;

        /// <summary>
        /// Species group identity information, from StanForD <c>SpeciesGroupDefinition/SpeciesGroupInfo</c>
        /// </summary>
        public string SpeciesGroupInfo { get; set; } = string.Empty;

        /// <summary>
        /// The species group's external reference, from StanForD
        /// <c>SpeciesGroupDefinition/SpeciesGroupUserID</c>
        /// </summary>
        public string SpeciesGroupUserID { get; set; } = string.Empty;

        /// <summary>
        /// Species group version, from StanForD <c>SpeciesGroupDefinition/SpeciesGroupVersion</c>
        /// </summary>
        public string SpeciesGroupVersion { get; set; } = string.Empty;
        #endregion
    }
}
