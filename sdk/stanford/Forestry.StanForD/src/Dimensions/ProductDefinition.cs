using Forestry.Serialization;

namespace Forestry.StanForD {
    /// <summary>
    /// A product definition loaded into the machine, from StanForD <c>ProductDefinition</c>;
    /// referenced by logs through their product key
    /// </summary>
    public sealed class ProductDefinition : Telemetry
    {
        #region identification
        /// <summary>
        /// Global unique identifier: "{machine key}-{product key}", where the machine key is
        /// the GUID without dashes (format "N"), e.g. "20767727ce544fba8f05cfbc218b4300-3350"
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Product identifier, from StanForD <c>ProductDefinition/ProductKey</c>; a running number
        /// created by the machine
        /// </summary>
        public int ProductKey { get; set; }

        /// <summary>
        /// Machine identifier, from StanForD <c>Machine/MachineKey</c> (a GUID); not the
        /// <c>MachineUserID</c>, which is an external reference
        /// </summary>
        public Guid MachineKey { get; set; }
        #endregion

        #region dimensions
        /// <summary>
        /// Descriptive product name e.g. "Pine sawlog" or "Spruce pulpwood", from StanForD
        /// <c>ClassifiedProductDefinition/ProductName</c>
        /// </summary>
        /// <remarks>
        /// Not a direct child of <c>ProductDefinition</c>, so a policy sets it from a
        /// <c>ClassifiedProductDefinition</c> contextual telemetry
        /// </remarks>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Free text identity information (code) normally to be used by logging organization
        /// e.g. "012-JGA", from StanForD <c>ClassifiedProductDefinition/ProductInfo</c>
        /// </summary>
        /// <remarks>
        /// Set by a policy, as for <see cref="ProductName"/>
        /// </remarks>
        public string ProductInfo { get; set; } = string.Empty;

        /// <summary>
        /// Additional identity information normally to be used by logging organization e.g.
        /// "JGA121", from StanForD <c>ClassifiedProductDefinition/ProductVersion</c>
        /// </summary>
        /// <remarks>
        /// Set by a policy, as for <see cref="ProductName"/>
        /// </remarks>
        public string ProductVersion { get; set; } = string.Empty;

        /// <summary>
        /// The product's external reference, from StanForD <c>ClassifiedProductDefinition/ProductUserID</c>
        /// </summary>
        /// <remarks>
        /// Set by a policy, as for <see cref="ProductName"/>
        /// </remarks>
        public string ProductUserID { get; set; } = string.Empty;

        /// <summary>
        /// Where the product is delivered, e.g. a sawmill, from StanForD
        /// <c>ClassifiedProductDefinition/ProductDestination/BusinessName</c>
        /// </summary>
        /// <remarks>
        /// Set by a policy from a <see cref="ProductDestination"/> telemetry
        /// </remarks>
        public string ProductDestinationBusinessName { get; set; } = string.Empty;

        /// <summary>
        /// The destination's business identity, from StanForD
        /// <c>ClassifiedProductDefinition/ProductDestination/BusinessID</c>
        /// </summary>
        /// <remarks>
        /// Set by a policy from a <see cref="ProductDestination"/> telemetry
        /// </remarks>
        public string ProductDestinationBusinessID { get; set; } = string.Empty;
        #endregion
    }
}
