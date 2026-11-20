using Forestry.Deserialize;

namespace Forestry.StanForD {
    /// <summary>
    /// Logs are products from tree kinds.  They have a global identifier composed of:
    /// - machine key
    /// - stem (tree) key
    /// - log key, a sequence within the stem
    ///
    /// Log volume, length and diameters are the production metrics.  Partitioning for big
    /// data is done by the time the machine created the Harvesting document, then contract.
    /// </summary>
    /// <remarks>
    /// Members are set by policies from StanForD Harvesting (hpr) elements; each member names
    /// its source.  Measurements keep the units of the document (see the unit members).
    /// </remarks>
    public sealed class Log : Row
    {
        #region identification
        /// <summary>
        /// Global unique identifier: "{machine key}-{stem key}-{log key}", where the machine key is
        /// the GUID without dashes (format "N"), e.g. "20767727ce544fba8f05cfbc218b4300-619255-1"
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Sequence identifier in the stem, from StanForD <c>Log/LogKey</c>; restarts for every stem
        /// </summary>
        public int LogKey { get; set; }

        /// <summary>
        /// Stem (tree) identifier without the machine id, from StanForD <c>Stem/StemKey</c>; a
        /// running number never reset by the machine
        /// </summary>
        public int StemKey { get; set; }

        /// <summary>
        /// Machine identifier, from StanForD <c>Machine/MachineKey</c> (a GUID); not the
        /// <c>MachineUserID</c>, which is an external reference
        /// </summary>
        public Guid MachineKey { get; set; }
        #endregion

        #region partitioning
        /// <summary>
        /// When the harvesting machine created the document, in UTC, from StanForD
        /// <c>HarvestedProductionHeader/CreationDate</c>.  Harvesting documents are incremental,
        /// so this is as close to the cut time as the document allows.  Time spans are the
        /// primary partitioning in the forest industry.
        /// </summary>
        public DateTimeOffset HarvestingCreationTime { get; set; }

        /// <summary>
        /// Havesting contract number associated with the havesting object i.e. business to operative
        /// </summary>
        public string ContractNumber { get; set; } = string.Empty;
        #endregion

        #region metrics
        /// <summary>
        /// Physical volume of the harvested logs as they exist before debarking, from StanForD
        /// <c>LogVolume</c> with categories "m3sob" and "Machine".
        ///
        /// Useful for:
        /// - transportation and logistics calculations
        /// - biomass and energy calculations
        /// </summary>
        public decimal MachineVolumeOverBark { get; set; }

        /// <summary>
        /// Quantity of usable wood excluding bark that may not be part of the agreed timber
        /// product, from StanForD <c>LogVolume</c> with categories "m3sub" and "Machine"
        /// </summary>
        /// <remarks>
        /// Harvesters generally measure the outside diameter of a log and use mathematical
        /// bark-thickness functions to estimate the diameter under bark
        /// </remarks>
        public decimal MachineVolumeUnderBark { get; set; }

        /// <summary>
        /// Represents the volume used to calculate the commercial value of a log, from StanForD
        /// <c>LogVolume</c> with categories "m3 (price)" and "Machine".  A calculation according to
        /// the commercial measurement rules agreed between the timber buyer and seller:
        /// - diameter measurement used (top, middle or multiple measurements)
        /// - if diameter is measured under or over bark
        /// - length is used (actual length or nominal commercial length)
        /// - diameter and length rounding rules
        /// - applicable assortment and quality classifications
        /// </summary>
        public decimal MachineVolumePrice { get; set; }

        /// <summary>
        /// Machine calculated log length, from StanForD <c>LogMeasurement/LogLength</c>
        /// </summary>
        public int MachineLength { get; set; }

        /// <summary>
        /// Butt of the log over bark, from StanForD <c>LogDiameter</c> "Butt ob"; 0 when not measured
        /// </summary>
        public int MachineDiameterButtOverBark { get; set; }

        /// <summary>
        /// Butt of the log under bark, from StanForD <c>LogDiameter</c> "Butt ub"; 0 when not measured
        /// </summary>
        public int MachineDiameterButtUnderBark { get; set; }

        /// <summary>
        /// Midpoint of the log over bark, from StanForD <c>LogDiameter</c> "Mid ob"; 0 when not measured
        /// </summary>
        public int MachineDiameterMidOverBark { get; set; }

        /// <summary>
        /// Midpoint of the log under bark, from StanForD <c>LogDiameter</c> "Mid ub"; 0 when not measured
        /// </summary>
        public int MachineDiameterMidUnderBark { get; set; }

        /// <summary>
        /// Commercial measurement compatibility with markets using the German HKS convention
        /// over bark, from StanForD <c>LogDiameter</c> "Mid HKS ob"; 0 when not measured
        /// </summary>
        public int MachineDiameterMidHKSOverBark { get; set; }

        /// <summary>
        /// Commercial measurement compatibility with markets using the German HKS convention
        /// under bark, from StanForD <c>LogDiameter</c> "Mid HKS ub"; 0 when not measured
        /// </summary>
        public int MachineDiameterMidHKSUnderBark { get; set; }

        /// <summary>
        /// StanForD rule 10 cm from the top end over bark, from StanForD <c>LogDiameter</c>
        /// "Top ob"; 0 when not measured
        /// </summary>
        public int MachineDiameterTopOverBark { get; set; }

        /// <summary>
        /// StanForD rule 10 cm from the top end under bark, from StanForD <c>LogDiameter</c>
        /// "Top ub"; 0 when not measured
        /// </summary>
        public int MachineDiameterTopUnderBark { get; set; }
        #endregion

        #region dimensions
        /// <summary>
        /// Unit of the diameters as given by the document, from StanForD
        /// <c>HarvestedProduction@diameterUnit</c> (e.g. "mm")
        /// </summary>
        public string DiameterUnit { get; set; } = string.Empty;

        /// <summary>
        /// Unit of the length as given by the document, from StanForD
        /// <c>HarvestedProduction@lengthUnit</c> (e.g. "cm")
        /// </summary>
        public string LengthUnit { get; set; } = string.Empty;

        /// <summary>
        /// Unit of the volumes as given by the document, from StanForD
        /// <c>HarvestedProduction@volumeUnit</c> (e.g. "m3")
        /// </summary>
        public string VolumeUnit { get; set; } = string.Empty;

        /// <summary>
        /// The machine's sequence number of the stem within its object, from StanForD
        /// <c>Stem/StemNumber</c>; its highest value against the stems counted shows a missing document
        /// </summary>
        public int StemNumber { get; set; }

        /// <summary>
        /// Object the log's stem was harvested in, from StanForD <c>Stem/ObjectKey</c>; refers to an
        /// <see cref="ObjectDefinition"/> of the same machine
        /// </summary>
        public int ObjectKey { get; set; }

        /// <summary>
        /// Descriptive name of the harvesting object, from the referenced
        /// <see cref="ObjectDefinition.ObjectName"/>; separates objects within a contract
        /// </summary>
        public string ObjectName { get; set; } = string.Empty;

        /// <summary>
        /// The object's external reference, from the referenced <see cref="ObjectDefinition.ObjectUserID"/>
        /// </summary>
        public string ObjectUserID { get; set; } = string.Empty;

        /// <summary>
        /// Sub-object (a part of the object's area) the log's stem was harvested in, from StanForD
        /// <c>Stem/SubObjectKey</c>; refers to a <see cref="SubObject"/> of the stem's object
        /// </summary>
        public int SubObjectKey { get; set; }

        /// <summary>
        /// Descriptive name of the sub-object, from the referenced <see cref="SubObject.SubObjectName"/>
        /// </summary>
        public string SubObjectName { get; set; } = string.Empty;

        /// <summary>
        /// The sub-object's external reference, from the referenced <see cref="SubObject.SubObjectUserID"/>
        /// </summary>
        public string SubObjectUserID { get; set; } = string.Empty;

        /// <summary>
        /// Species group (tree kind) of the stem, from StanForD <c>Stem/SpeciesGroupKey</c>; refers
        /// to a <see cref="SpeciesGroupDefinition"/> of the same machine
        /// </summary>
        public int SpeciesGroupKey { get; set; }

        /// <summary>
        /// Species group name e.g. "GRAN" (spruce in Swedish), from the referenced
        /// <see cref="SpeciesGroupDefinition.SpeciesGroupName"/>
        /// </summary>
        public string SpeciesGroupName { get; set; } = string.Empty;

        /// <summary>
        /// Species group identity information, from the referenced
        /// <see cref="SpeciesGroupDefinition.SpeciesGroupInfo"/>
        /// </summary>
        public string SpeciesGroupInfo { get; set; } = string.Empty;

        /// <summary>
        /// The species group's external reference, from the referenced
        /// <see cref="SpeciesGroupDefinition.SpeciesGroupUserID"/>
        /// </summary>
        public string SpeciesGroupUserID { get; set; } = string.Empty;

        /// <summary>
        /// Species group version, from the referenced <see cref="SpeciesGroupDefinition.SpeciesGroupVersion"/>
        /// </summary>
        public string SpeciesGroupVersion { get; set; } = string.Empty;

        /// <summary>
        /// Operator who harvested the stem, from StanForD <c>Stem/OperatorKey</c>; refers to an
        /// <see cref="OperatorDefinition"/> of the same machine
        /// </summary>
        public int OperatorKey { get; set; }

        /// <summary>
        /// Business the operator works for, from the referenced
        /// <see cref="OperatorDefinition.OperatorBusinessName"/>
        /// </summary>
        public string OperatorBusinessName { get; set; } = string.Empty;

        /// <summary>
        /// The operator's external reference, from the referenced <see cref="OperatorDefinition.OperatorUserID"/>
        /// </summary>
        public string OperatorUserID { get; set; } = string.Empty;

        /// <summary>
        /// The machine's external reference, from <see cref="Machine.MachineUserID"/>
        /// </summary>
        public string MachineUserID { get; set; } = string.Empty;

        /// <summary>
        /// The logging contractor's business identity, from <see cref="Machine.LoggingContractorBusinessID"/>
        /// </summary>
        public string LoggingContractorBusinessID { get; set; } = string.Empty;

        /// <summary>
        /// Product the log was cut as, from StanForD <c>Log/ProductKey</c>; refers to a
        /// <see cref="ProductDefinition"/> of the same machine
        /// </summary>
        public int ProductKey { get; set; }

        /// <summary>
        /// Descriptive product name e.g. "Pine sawlog" or "Spruce pulpwood", from the referenced
        /// <see cref="ProductDefinition.ProductName"/>
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// Free text identity information (code) normally to be used by logging organization
        /// e.g. "012-JGA", from the referenced <see cref="ProductDefinition.ProductInfo"/>
        /// </summary>
        public string ProductInfo { get; set; } = string.Empty;

        /// <summary>
        /// Additional identity information normally to be used by logging organization, from the
        /// referenced <see cref="ProductDefinition.ProductVersion"/>
        /// </summary>
        public string ProductVersion { get; set; } = string.Empty;

        /// <summary>
        /// The product's external reference, from the referenced <see cref="ProductDefinition.ProductUserID"/>
        /// </summary>
        public string ProductUserID { get; set; } = string.Empty;

        /// <summary>
        /// Where the product is delivered, e.g. a sawmill, from the referenced
        /// <see cref="ProductDefinition.ProductDestinationBusinessName"/>
        /// </summary>
        public string ProductDestinationBusinessName { get; set; } = string.Empty;

        /// <summary>
        /// The destination's business identity, from the referenced
        /// <see cref="ProductDefinition.ProductDestinationBusinessID"/>
        /// </summary>
        public string ProductDestinationBusinessID { get; set; } = string.Empty;

        /// <summary>
        /// Cutting reason, from StanForD <c>CuttingCategory/CuttingReason</c>
        /// </summary>
        public CuttingReason CuttingReason { get; set; }

        /// <summary>
        /// Top sawing normally used if stem is broken in order to avoid waste, from StanForD
        /// <c>Log/TopSawing</c>
        /// </summary>
        public bool TopSawing { get; set; }

        /// <summary>
        /// Function to reset length measurement if whole stem was first delimbed before bucked or
        /// if stem fell out of the head during processing.  This function means that the saw does
        /// not need to be activated in order to reset length measurement.  From StanForD
        /// <c>Log/FindButtEndFunction</c>
        /// </summary>
        public bool FindButtEndFunction { get; set; }
        #endregion
    }
}
