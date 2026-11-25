using Forestry.Serialization;

namespace Forestry.StanForD {
    /// <summary>
    /// Trees (stems) have metrics about geography, processing (operation + tree handling),
    /// and the machine's boom.
    /// </summary>
    public sealed class Stem : Telemetry
    {
        #region identification
        /// <summary>
        /// Global unique identifier: "{machine key}-{stem key}", where the machine key is
        /// the GUID without dashes (format "N"), e.g. "20767727ce544fba8f05cfbc218b4300-619255"
        /// </summary>
        public string Id { get; set; } = string.Empty;

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
        /// Contract number, from StanForD <c>ObjectDefinition/ContractNumber</c>.  Grouping logs by
        /// contract is common in the forest industry.
        /// </summary>
        public string ContractNumber { get; set; } = string.Empty;
        #endregion

        #region metrics
        /// <summary>
        /// Operation and number of trees handled
        /// </summary>
        public ProcessingCategory ProcessingCategory { get; set; }

        /// <summary>
        /// Latitude in decimal degrees, signed: positive north of the equator, negative south.
        /// From StanForD <c>StemCoordinates/Latitude</c>, an absolute value whose
        /// <c>latitudeCategory</c> ("North" or "South") gives the sign.
        /// </summary>
        /// <remarks>
        /// The position of the GPS receiver, given by <c>StemCoordinates@receiverPosition</c>, usually
        /// the base machine ("Base machine position"), not the tree itself; the tree can stand as far
        /// away as the boom reaches.
        /// </remarks>
        public decimal Latitude { get; set; }

        /// <summary>
        /// Longitude in decimal degrees, signed: positive east of the prime meridian, negative west.
        /// From StanForD <c>StemCoordinates/Longitude</c>, an absolute value whose
        /// <c>longitudeCategory</c> ("East" or "West") gives the sign.
        /// </summary>
        /// <remarks>
        /// The position of the GPS receiver, as for <see cref="Latitude"/>.
        /// </remarks>
        public decimal Longitude { get; set; }

        /// <summary>
        /// Height above sea level, from StanForD <c>StemCoordinates/Altitude</c>, registered
        /// according to the coordinate reference system
        /// </summary>
        public decimal Altitude { get; set; }

        /// <summary>
        /// Coordinate reference system used for the location data, from StanForD
        /// <c>StemCoordinates@coordinateReferenceSystem</c> (e.g. "WGS84")
        /// </summary>
        public CoordinateReferenceSystem CoordinateReferenceSystem { get; set; }

        /// <summary>
        /// Boom angle (0-360 degrees) when felling the tree, from StanForD
        /// <c>BoomPositioning/BoomAngle</c> with <c>boomPositioningCategory</c> "Felling"
        /// </summary>
        public int BoomAngleFelling { get; set; }

        /// <summary>
        /// Boom angle (0-360 degrees) when the last log was cut, from StanForD
        /// <c>BoomPositioning/BoomAngle</c> with <c>boomPositioningCategory</c> "Last log cut"
        /// </summary>
        public int BoomAngleLastLogCut { get; set; }

        /// <summary>
        /// Boom extension when felling the tree: the distance from the base machine to the
        /// attachment (e.g. the harvester head), in the document's length unit.  From StanForD
        /// <c>BoomPositioning/BoomExtension</c> with <c>boomPositioningCategory</c> "Felling".
        /// </summary>
        /// <remarks>
        /// StanForD allows a "default" value meaning the normal extension rather than a measured
        /// one.  The real documents seen so far report 65535 for every stem, which is such a value,
        /// so filter it out before computing averages or distances.
        /// </remarks>
        public int BoomExtensionFelling { get; set; }

        /// <summary>
        /// Boom extension when the last log was cut, in the document's length unit.  From StanForD
        /// <c>BoomPositioning/BoomExtension</c> with <c>boomPositioningCategory</c> "Last log cut".
        /// </summary>
        /// <remarks>
        /// May be the "default" value (e.g. 65535) rather than a measurement, as for
        /// <see cref="BoomExtensionFelling"/>.
        /// </remarks>
        public int BoomExtensionLastLogCut { get; set; }

        /// <summary>
        /// Diameter at breast height, in the document's diameter unit, from StanForD
        /// <c>SingleTreeProcessedStem/DBH</c>; 0 when not measured
        /// </summary>
        public int DBH { get; set; }
        #endregion

        #region dimensions
        /// <summary>
        /// The machine's sequence number of the stem within its object, from StanForD
        /// <c>Stem/StemNumber</c>; its highest value against the stems counted shows a missing document
        /// </summary>
        public int StemNumber { get; set; }

        /// <summary>
        /// Object the stem was harvested in, from StanForD <c>Stem/ObjectKey</c>; refers to an
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
        /// Sub-object (a part of the object's area) the stem was harvested in, from StanForD
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
        #endregion
    }
}
