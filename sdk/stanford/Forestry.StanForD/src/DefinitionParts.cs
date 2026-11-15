using Forestry.Deserialize;

namespace Forestry.StanForD {
    /// <summary>
    /// A part of an object's area, from StanForD <c>ObjectDefinition/SubObject</c>; never comes out, kept
    /// until the document ends so stems and logs can copy its dimensions
    /// </summary>
    public sealed class SubObject : Row
    {
        /// <summary>
        /// The object the sub-object belongs to; set by a policy from the enclosing
        /// <see cref="ObjectDefinition"/>, since sub-object keys are only unique within an object
        /// </summary>
        public int ObjectKey { get; set; }

        /// <summary>
        /// Sub-object identifier within its object, from StanForD <c>SubObject/SubObjectKey</c>
        /// </summary>
        public int SubObjectKey { get; set; }

        /// <summary>
        /// Descriptive name of the sub-object, from StanForD <c>SubObject/SubObjectName</c>
        /// </summary>
        public string SubObjectName { get; set; } = string.Empty;

        /// <summary>
        /// The sub-object's external reference, from StanForD <c>SubObject/SubObjectUserID</c>
        /// </summary>
        public string SubObjectUserID { get; set; } = string.Empty;
    }

    /// <summary>
    /// Contact information, from StanForD <c>ContactInformation</c>; never comes out. Only the business
    /// name is read, never personal details (names, e-mail), and only an operator's is used
    /// </summary>
    public sealed class ContactInformation : Row
    {
        /// <summary>
        /// The business, from StanForD <c>ContactInformation/BusinessName</c>
        /// </summary>
        public string BusinessName { get; set; } = string.Empty;
    }

    /// <summary>
    /// A product's commercial identity, from StanForD <c>ProductDefinition/ClassifiedProductDefinition</c>;
    /// never comes out, its values are copied onto the enclosing <see cref="ProductDefinition"/>
    /// </summary>
    public sealed class ClassifiedProductDefinition : Row
    {
        /// <summary>
        /// Descriptive product name, from StanForD <c>ClassifiedProductDefinition/ProductName</c>
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Product identity information, from StanForD <c>ClassifiedProductDefinition/ProductInfo</c>
        /// </summary>
        public string ProductInfo { get; set; } = string.Empty;

        /// <summary>
        /// Product version, from StanForD <c>ClassifiedProductDefinition/ProductVersion</c>
        /// </summary>
        public string ProductVersion { get; set; } = string.Empty;

        /// <summary>
        /// The product's external reference, from StanForD <c>ClassifiedProductDefinition/ProductUserID</c>
        /// </summary>
        public string ProductUserID { get; set; } = string.Empty;
    }

    /// <summary>
    /// Where a product is delivered, from StanForD <c>ClassifiedProductDefinition/ProductDestination</c>;
    /// never comes out, its values are copied onto the enclosing <see cref="ProductDefinition"/>
    /// </summary>
    public sealed class ProductDestination : Row
    {
        /// <summary>
        /// The receiving business, e.g. a sawmill, from StanForD <c>ProductDestination/BusinessName</c>
        /// </summary>
        public string BusinessName { get; set; } = string.Empty;

        /// <summary>
        /// The receiving business's identity, from StanForD <c>ProductDestination/BusinessID</c>
        /// </summary>
        public string BusinessID { get; set; } = string.Empty;
    }

    /// <summary>
    /// The logging contractor running the machine, from StanForD <c>Machine/LoggingContractor</c>; never
    /// comes out, its identity is copied onto the <see cref="Machine"/>
    /// </summary>
    public sealed class LoggingContractor : Row
    {
        /// <summary>
        /// The contractor's business identity, from StanForD <c>LoggingContractor/BusinessID</c>
        /// </summary>
        public string BusinessID { get; set; } = string.Empty;
    }
}
