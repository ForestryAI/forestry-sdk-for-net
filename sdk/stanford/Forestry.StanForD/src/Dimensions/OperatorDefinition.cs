using Forestry.Serialization;

namespace Forestry.StanForD {
    /// <summary>
    /// A machine operator loaded into the machine, from StanForD <c>OperatorDefinition</c>; stems
    /// reference it through their operator key
    /// </summary>
    /// <remarks>
    /// StanForD's operator contact information holds personal data (first and last name, e-mail).
    /// Only the business name is read; personal details are never carried onto rows.
    /// </remarks>
    public sealed class OperatorDefinition : Row
    {
        #region identification
        /// <summary>
        /// Global unique identifier: "{machine key}-{operator key}", where the machine key is
        /// the GUID without dashes (format "N"), e.g. "20767727ce544fba8f05cfbc218b4300-2"
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Operator identifier, from StanForD <c>OperatorDefinition/OperatorKey</c>; a running
        /// number created by the machine
        /// </summary>
        public int OperatorKey { get; set; }

        /// <summary>
        /// Machine identifier, from StanForD <c>Machine/MachineKey</c> (a GUID); not the
        /// <c>MachineUserID</c>, which is an external reference
        /// </summary>
        public Guid MachineKey { get; set; }
        #endregion

        #region dimensions
        /// <summary>
        /// Business the operator works for, from StanForD
        /// <c>OperatorDefinition/ContactInformation/BusinessName</c>
        /// </summary>
        /// <remarks>
        /// Not a direct child of <c>OperatorDefinition</c>, so a policy sets it from a
        /// <c>ContactInformation</c> contextual row
        /// </remarks>
        public string OperatorBusinessName { get; set; } = string.Empty;

        /// <summary>
        /// The operator's external reference, from StanForD <c>OperatorDefinition/OperatorUserID</c>
        /// </summary>
        public string OperatorUserID { get; set; } = string.Empty;
        #endregion
    }
}
