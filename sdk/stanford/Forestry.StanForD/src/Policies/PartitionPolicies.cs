using Forestry.Deserialize;
using Forestry.Deserialize.Policies;
using Forestry.Deserialize.Reading;

namespace Forestry.StanForD.Policies {
    /// <summary>
    /// The header never comes out; it is kept until the document ends so every stem and log can take
    /// its creation date as their partition time
    /// </summary>
    internal sealed class HeaderPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        public override void Complete(ContextRow row, DeserializationContext context, Action<ContextRow> next)
        {
            row.Enumerate(false);
            row.Keep(Lifetime.UntilEnd);
            next(row);
        }
    }

    /// <summary>
    /// A stem's partition values (FR-007, FR-009): <c>HarvestingCreationTime</c> (the header's creation
    /// date in UTC), then <c>ContractNumber</c>, with the object's <c>ObjectName</c>
    /// </summary>
    internal sealed class StemPartitionPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        /// <exception cref="InvalidDataException">No header creation date, no object key, or no such
        /// object</exception>
        public override void Complete(ContextRow row, DeserializationContext context, Action<ContextRow> next)
        {
            string what = $"Stem {row.Row.AsType<Stem>().StemKey}";
            DateTimeOffset creationTime = Harvesting.HarvestingCreationTime(context, what);
            ObjectDefinition definition = Harvesting.Object(row, context, what);

            row.Set(nameof(Stem.HarvestingCreationTime), creationTime);
            row.Set(nameof(Stem.ContractNumber), definition.ContractNumber);
            row.Set(nameof(Stem.ObjectName), definition.ObjectName);

            next(row);
        }
    }

    /// <summary>
    /// A log's partition values, through its stem (FR-007, FR-009)
    /// </summary>
    internal sealed class LogPartitionPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        /// <exception cref="InvalidDataException">No enclosing stem, no header creation date, no object key
        /// on the stem, or no such object</exception>
        public override void Complete(ContextRow row, DeserializationContext context, Action<ContextRow> next)
        {
            ContextRow stem = Harvesting.StemRow(row, context);
            string what = $"Log {row.Row.AsType<Log>().LogKey} of Stem {stem.Row.AsType<Stem>().StemKey}";
            DateTimeOffset creationTime = Harvesting.HarvestingCreationTime(context, what);
            ObjectDefinition definition = Harvesting.Object(stem, context, what);

            row.Set(nameof(Log.HarvestingCreationTime), creationTime);
            row.Set(nameof(Log.ContractNumber), definition.ContractNumber);
            row.Set(nameof(Log.ObjectName), definition.ObjectName);

            next(row);
        }
    }
}
