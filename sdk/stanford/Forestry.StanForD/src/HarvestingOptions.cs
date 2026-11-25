using Forestry.Serialization.Xml;
using Forestry.StanForD.Converters;
using Forestry.StanForD.Policies;

namespace Forestry.StanForD {
    /// <summary>
    /// What happens to a StanForD element whose categories match no row member (FR-012)
    /// </summary>
    public enum UnmatchedOccurrence
    {
        /// <summary>
        /// Deserialization fails, naming the element and its categories (the default)
        /// </summary>
        Fail,

        /// <summary>
        /// The element is ignored
        /// </summary>
        Ignore
    }

    /// <summary>
    /// Choices for shredding Harvesting documents
    /// </summary>
    public sealed class HarvestingOptions
    {
        /// <summary>
        /// What happens to a volume, diameter or measurement whose categories match no <see cref="Log"/>
        /// member; <see cref="UnmatchedOccurrence.Fail"/> by default
        /// </summary>
        public UnmatchedOccurrence Unmatched { get; set; } = UnmatchedOccurrence.Fail;
    }

    /// <summary>
    /// Sets up XML deserialization options for StanForD Harvesting (hpr) documents
    /// </summary>
    public static class HarvestingOptionsExtensions
    {
        /// <summary>
        /// Adds what shredding a Harvesting document needs: <see cref="Stem"/> and <see cref="Log"/>, the only
        /// rows that come out; the header, the machine, the common definitions and the log measurements,
        /// which never come out but are read for the stems and logs; converters for the StanForD texts the
        /// rows use; and the policies setting each row's identification, partition values and measurements
        /// </summary>
        /// <param name="options">Options not used yet</param>
        /// <param name="configure">Changes the <see cref="HarvestingOptions"/> defaults, e.g. ignoring unmatched
        /// measurements</param>
        /// <returns>The same options</returns>
        public static DeserializeXmlOptions AddHarvesting(this DeserializeXmlOptions options, Action<HarvestingOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(options);

            HarvestingOptions harvesting = new();
            configure?.Invoke(harvesting);

            options.Rows
                .Add<Stem>()
                .Add<Log>()
                .Add<HarvestedProductionHeader>()
                .Add<Machine>()
                .Add<ObjectDefinition>()
                .Add<SpeciesGroupDefinition>()
                .Add<OperatorDefinition>()
                .Add<ProductDefinition>()
                .Add<SubObject>()
                .Add<ContactInformation>()
                .Add<ClassifiedProductDefinition>()
                .Add<ProductDestination>()
                .Add<LoggingContractor>()
                .Add<StemCoordinates>()
                .Add<Latitude>()
                .Add<Longitude>()
                .Add<BoomPositioning>()
                .Add<SingleTreeProcessedStem>()
                .Add<LogVolume>()
                .Add<LogMeasurement>()
                .Add<LogDiameter>();

            options.Converters.Add(new StanForDEnumConverter<ProcessingCategory>());
            options.Converters.Add(new StanForDEnumConverter<CoordinateReferenceSystem>());
            options.Converters.Add(new StanForDEnumConverter<CuttingReason>());

            options.Policies
                .Add<HarvestedProductionHeader>(new HeaderPolicy())
                .Add<Machine>(new MachinePolicy())
                .Add<ObjectDefinition>(new CommonDefinitionPolicy<ObjectDefinition>(nameof(ObjectDefinition.ObjectKey), definition => definition.ObjectKey))
                .Add<SpeciesGroupDefinition>(new CommonDefinitionPolicy<SpeciesGroupDefinition>(nameof(SpeciesGroupDefinition.SpeciesGroupKey), definition => definition.SpeciesGroupKey))
                .Add<OperatorDefinition>(new CommonDefinitionPolicy<OperatorDefinition>(nameof(OperatorDefinition.OperatorKey), definition => definition.OperatorKey))
                .Add<ProductDefinition>(new CommonDefinitionPolicy<ProductDefinition>(nameof(ProductDefinition.ProductKey), definition => definition.ProductKey))
                .Add<SubObject>(new SubObjectPolicy())
                .Add<ContactInformation>(new ContactInformationPolicy())
                .Add<ClassifiedProductDefinition>(new ClassifiedProductDefinitionPolicy())
                .Add<ProductDestination>(new ProductDestinationPolicy())
                .Add<LoggingContractor>(new LoggingContractorPolicy())
                .Add<StemCoordinates>(new StemCoordinatesPolicy())
                .Add<Latitude>(new LatitudePolicy(harvesting.Unmatched))
                .Add<Longitude>(new LongitudePolicy(harvesting.Unmatched))
                .Add<BoomPositioning>(new BoomPositioningPolicy(harvesting.Unmatched))
                .Add<SingleTreeProcessedStem>(new SingleTreeProcessedStemPolicy())
                .Add<LogVolume>(new LogVolumePolicy(harvesting.Unmatched))
                .Add<LogMeasurement>(new LogMeasurementPolicy(harvesting.Unmatched))
                .Add<LogDiameter>(new LogDiameterPolicy(harvesting.Unmatched))
                .Add<Stem>(new StemIdentificationPolicy())
                .Add<Log>(new LogIdentificationPolicy())
                .Add<Stem>(new StemPartitionPolicy())
                .Add<Log>(new LogPartitionPolicy())
                .Add<Stem>(new StemDimensionPolicy())
                .Add<Log>(new LogDimensionPolicy());

            return options;
        }
    }
}
