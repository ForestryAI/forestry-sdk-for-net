using Forestry.Serialization;
using Forestry.Serialization.Policies;
using Forestry.Serialization.Reading;

namespace Forestry.StanForD.Policies {
    /// <summary>
    /// The header never comes out; it is kept until the document ends so every stem and log can take
    /// its creation date as their partition time
    /// </summary>
    internal sealed class HeaderPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        public override void Complete(ContextTelemetry telemetry, DeserializeContext context, Action<ContextTelemetry> next)
        {
            telemetry.Enumerate(false);
            telemetry.Keep(Lifetime.UntilEnd);
            next(telemetry);
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
        public override void Complete(ContextTelemetry telemetry, DeserializeContext context, Action<ContextTelemetry> next)
        {
            string what = $"Stem {telemetry.Telemetry.AsType<Stem>().StemKey}";
            DateTimeOffset creationTime = Harvesting.HarvestingCreationTime(context, what);
            ObjectDefinition definition = Harvesting.Object(telemetry, context, what);

            telemetry.Set(nameof(Stem.HarvestingCreationTime), creationTime);
            telemetry.Set(nameof(Stem.ContractNumber), definition.ContractNumber);
            telemetry.Set(nameof(Stem.ObjectName), definition.ObjectName);

            next(telemetry);
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
        public override void Complete(ContextTelemetry telemetry, DeserializeContext context, Action<ContextTelemetry> next)
        {
            ContextTelemetry stem = Harvesting.StemTelemetry(telemetry, context);
            string what = $"Log {telemetry.Telemetry.AsType<Log>().LogKey} of Stem {stem.Telemetry.AsType<Stem>().StemKey}";
            DateTimeOffset creationTime = Harvesting.HarvestingCreationTime(context, what);
            ObjectDefinition definition = Harvesting.Object(stem, context, what);

            telemetry.Set(nameof(Log.HarvestingCreationTime), creationTime);
            telemetry.Set(nameof(Log.ContractNumber), definition.ContractNumber);
            telemetry.Set(nameof(Log.ObjectName), definition.ObjectName);

            next(telemetry);
        }
    }
}
