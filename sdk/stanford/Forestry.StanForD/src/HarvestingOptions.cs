using Forestry.Deserialize.Xml;
using Forestry.StanForD.Converters;
using Forestry.StanForD.Policies;

namespace Forestry.StanForD {
    /// <summary>
    /// Sets up XML deserialization options for StanForD Harvesting (hpr) documents
    /// </summary>
    public static class HarvestingOptionsExtensions
    {
        /// <summary>
        /// Adds what shredding a Harvesting document needs: <see cref="Stem"/> and <see cref="Log"/>, the only
        /// rows that come out; <see cref="Machine"/> and the common definitions, which never come out but are
        /// read for the stems and logs; converters for the StanForD texts the rows use; and the policies
        /// setting each row's identification
        /// </summary>
        /// <param name="options">Options not used yet</param>
        /// <returns>The same options</returns>
        public static DeserializeXmlOptions AddHarvesting(this DeserializeXmlOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.Rows
                .Add<Stem>()
                .Add<Log>()
                .Add<Machine>()
                .Add<ObjectDefinition>()
                .Add<SpeciesGroupDefinition>()
                .Add<OperatorDefinition>()
                .Add<ProductDefinition>();

            options.Converters.Add(new StanForDEnumConverter<ProcessingCategory>());
            options.Converters.Add(new StanForDEnumConverter<CoordinateReferenceSystem>());
            options.Converters.Add(new StanForDEnumConverter<CuttingReason>());

            options.Policies
                .Add<Machine>(new MachinePolicy())
                .Add<ObjectDefinition>(new CommonDefinitionPolicy<ObjectDefinition>(nameof(ObjectDefinition.ObjectKey), definition => definition.ObjectKey))
                .Add<SpeciesGroupDefinition>(new CommonDefinitionPolicy<SpeciesGroupDefinition>(nameof(SpeciesGroupDefinition.SpeciesGroupKey), definition => definition.SpeciesGroupKey))
                .Add<OperatorDefinition>(new CommonDefinitionPolicy<OperatorDefinition>(nameof(OperatorDefinition.OperatorKey), definition => definition.OperatorKey))
                .Add<ProductDefinition>(new CommonDefinitionPolicy<ProductDefinition>(nameof(ProductDefinition.ProductKey), definition => definition.ProductKey))
                .Add<Stem>(new StemIdentificationPolicy())
                .Add<Log>(new LogIdentificationPolicy());

            return options;
        }
    }
}
