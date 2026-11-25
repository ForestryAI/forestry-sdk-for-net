using Forestry.Serialization;
using Forestry.Serialization.Policies;
using Forestry.Serialization.Reading;

namespace Forestry.StanForD.Policies {
    /// <summary>
    /// Copies dimensions onto a stem or log (FR-021 to FR-023): everything a query filters or groups on
    /// is on the telemetry itself, so no query joins
    /// </summary>
    internal static class Dimensions
    {
        /// <summary>
        /// The kept common definition of type <typeparamref name="TDefinition"/> with <paramref name="value"/>
        /// as its key
        /// </summary>
        /// <exception cref="InvalidDataException">No such definition is kept (FR-003)</exception>
        public static TDefinition Definition<TDefinition>(DeserializeContext context, Func<TDefinition, bool> matches, string what, string keyMember, int value)
            where TDefinition : Telemetry =>
            context.Telemetries<TDefinition>()
                .Where(telemetry => !telemetry.IsOpen)
                .Select(telemetry => telemetry.Telemetry.AsType<TDefinition>())
                .FirstOrDefault(matches)
                ?? throw new InvalidDataException($"{what} refers to {keyMember} {value}, but no {typeof(TDefinition).Name} before it has that key.");

        /// <summary>
        /// Sets a stem's dimensions on <paramref name="target"/>: the stem itself, or one of its logs
        /// </summary>
        /// <param name="stemTelemetry">The stem, as a context telemetry</param>
        /// <param name="target">The telemetry to set: the stem or a log inside it</param>
        /// <param name="context"></param>
        /// <param name="what">The target, as named in an error, e.g. "Log 1 of Stem 619254"</param>
        public static void FromStem(ContextTelemetry stemTelemetry, ContextTelemetry target, DeserializeContext context, string what)
        {
            Stem stem = stemTelemetry.Telemetry.AsType<Stem>();
            bool isLog = !ReferenceEquals(stemTelemetry, target);

            if (isLog)
            {
                CopyIfSet(stemTelemetry, target, nameof(Stem.StemNumber), stem.StemNumber);
                CopyIfSet(stemTelemetry, target, nameof(Stem.ObjectKey), stem.ObjectKey);
                CopyIfSet(stemTelemetry, target, nameof(Stem.SubObjectKey), stem.SubObjectKey);
                CopyIfSet(stemTelemetry, target, nameof(Stem.SpeciesGroupKey), stem.SpeciesGroupKey);
                CopyIfSet(stemTelemetry, target, nameof(Stem.OperatorKey), stem.OperatorKey);
            }

            if (stemTelemetry.IsSet(nameof(Stem.ObjectKey)))
            {
                ObjectDefinition definition = Definition<ObjectDefinition>(context, d => d.ObjectKey == stem.ObjectKey, what, nameof(Stem.ObjectKey), stem.ObjectKey);
                target.Set(nameof(Stem.ObjectUserID), definition.ObjectUserID);

                if (stemTelemetry.IsSet(nameof(Stem.SubObjectKey)))
                {
                    SubObject subObject = Definition<SubObject>(context, s => s.ObjectKey == stem.ObjectKey && s.SubObjectKey == stem.SubObjectKey, what, nameof(Stem.SubObjectKey), stem.SubObjectKey);
                    target.Set(nameof(Stem.SubObjectName), subObject.SubObjectName);
                    target.Set(nameof(Stem.SubObjectUserID), subObject.SubObjectUserID);
                }
            }

            if (stemTelemetry.IsSet(nameof(Stem.SpeciesGroupKey)))
            {
                SpeciesGroupDefinition definition = Definition<SpeciesGroupDefinition>(context, d => d.SpeciesGroupKey == stem.SpeciesGroupKey, what, nameof(Stem.SpeciesGroupKey), stem.SpeciesGroupKey);
                target.Set(nameof(Stem.SpeciesGroupName), definition.SpeciesGroupName);
                target.Set(nameof(Stem.SpeciesGroupInfo), definition.SpeciesGroupInfo);
                target.Set(nameof(Stem.SpeciesGroupUserID), definition.SpeciesGroupUserID);
                target.Set(nameof(Stem.SpeciesGroupVersion), definition.SpeciesGroupVersion);
            }

            if (stemTelemetry.IsSet(nameof(Stem.OperatorKey)))
            {
                OperatorDefinition definition = Definition<OperatorDefinition>(context, d => d.OperatorKey == stem.OperatorKey, what, nameof(Stem.OperatorKey), stem.OperatorKey);
                target.Set(nameof(Stem.OperatorBusinessName), definition.OperatorBusinessName);
                target.Set(nameof(Stem.OperatorUserID), definition.OperatorUserID);
            }

            if (Enclosing.Telemetry<Machine>(stemTelemetry, context) is { } machine)
            {
                Machine value = machine.Telemetry.AsType<Machine>();
                target.Set(nameof(Stem.MachineUserID), value.MachineUserID);
                target.Set(nameof(Stem.LoggingContractorBusinessID), value.LoggingContractorBusinessID);
            }
        }

        private static void CopyIfSet(ContextTelemetry from, ContextTelemetry to, string member, int value)
        {
            if (from.IsSet(member))
            {
                to.Set(member, value);
            }
        }
    }

    /// <summary>
    /// A stem's dimensions, from its definitions and its machine
    /// </summary>
    internal sealed class StemDimensionPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        public override void Complete(ContextTelemetry telemetry, DeserializeContext context, Action<ContextTelemetry> next)
        {
            Dimensions.FromStem(telemetry, telemetry, context, $"Stem {telemetry.Telemetry.AsType<Stem>().StemKey}");
            next(telemetry);
        }
    }

    /// <summary>
    /// A log's dimensions: its stem's (copied, since the stem completes after its logs), and its
    /// product's and destination's
    /// </summary>
    internal sealed class LogDimensionPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        public override void Complete(ContextTelemetry telemetry, DeserializeContext context, Action<ContextTelemetry> next)
        {
            Log log = telemetry.Telemetry.AsType<Log>();
            ContextTelemetry stem = Harvesting.StemTelemetry(telemetry, context);
            string what = $"Log {log.LogKey} of Stem {stem.Telemetry.AsType<Stem>().StemKey}";

            Dimensions.FromStem(stem, telemetry, context, what);

            if (telemetry.IsSet(nameof(Log.ProductKey)))
            {
                ProductDefinition product = Dimensions.Definition<ProductDefinition>(context, d => d.ProductKey == log.ProductKey, what, nameof(Log.ProductKey), log.ProductKey);
                telemetry.Set(nameof(Log.ProductName), product.ProductName);
                telemetry.Set(nameof(Log.ProductInfo), product.ProductInfo);
                telemetry.Set(nameof(Log.ProductVersion), product.ProductVersion);
                telemetry.Set(nameof(Log.ProductUserID), product.ProductUserID);
                telemetry.Set(nameof(Log.ProductDestinationBusinessName), product.ProductDestinationBusinessName);
                telemetry.Set(nameof(Log.ProductDestinationBusinessID), product.ProductDestinationBusinessID);
            }

            next(telemetry);
        }
    }
}
