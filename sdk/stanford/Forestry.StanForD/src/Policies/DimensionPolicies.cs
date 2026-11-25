using Forestry.Serialization;
using Forestry.Serialization.Policies;
using Forestry.Serialization.Reading;

namespace Forestry.StanForD.Policies {
    /// <summary>
    /// Copies dimensions onto a stem or log (FR-021 to FR-023): everything a query filters or groups on
    /// is on the row itself, so no query joins
    /// </summary>
    internal static class Dimensions
    {
        /// <summary>
        /// The kept common definition of type <typeparamref name="TDefinition"/> with <paramref name="value"/>
        /// as its key
        /// </summary>
        /// <exception cref="InvalidDataException">No such definition is kept (FR-003)</exception>
        public static TDefinition Definition<TDefinition>(DeserializeContext context, Func<TDefinition, bool> matches, string what, string keyMember, int value)
            where TDefinition : Row =>
            context.Rows<TDefinition>()
                .Where(row => !row.IsOpen)
                .Select(row => row.Row.AsType<TDefinition>())
                .FirstOrDefault(matches)
                ?? throw new InvalidDataException($"{what} refers to {keyMember} {value}, but no {typeof(TDefinition).Name} before it has that key.");

        /// <summary>
        /// Sets a stem's dimensions on <paramref name="target"/>: the stem itself, or one of its logs
        /// </summary>
        /// <param name="stemRow">The stem, as a context row</param>
        /// <param name="target">The row to set: the stem or a log inside it</param>
        /// <param name="context"></param>
        /// <param name="what">The target, as named in an error, e.g. "Log 1 of Stem 619254"</param>
        public static void FromStem(ContextRow stemRow, ContextRow target, DeserializeContext context, string what)
        {
            Stem stem = stemRow.Row.AsType<Stem>();
            bool isLog = !ReferenceEquals(stemRow, target);

            if (isLog)
            {
                CopyIfSet(stemRow, target, nameof(Stem.StemNumber), stem.StemNumber);
                CopyIfSet(stemRow, target, nameof(Stem.ObjectKey), stem.ObjectKey);
                CopyIfSet(stemRow, target, nameof(Stem.SubObjectKey), stem.SubObjectKey);
                CopyIfSet(stemRow, target, nameof(Stem.SpeciesGroupKey), stem.SpeciesGroupKey);
                CopyIfSet(stemRow, target, nameof(Stem.OperatorKey), stem.OperatorKey);
            }

            if (stemRow.IsSet(nameof(Stem.ObjectKey)))
            {
                ObjectDefinition definition = Definition<ObjectDefinition>(context, d => d.ObjectKey == stem.ObjectKey, what, nameof(Stem.ObjectKey), stem.ObjectKey);
                target.Set(nameof(Stem.ObjectUserID), definition.ObjectUserID);

                if (stemRow.IsSet(nameof(Stem.SubObjectKey)))
                {
                    SubObject subObject = Definition<SubObject>(context, s => s.ObjectKey == stem.ObjectKey && s.SubObjectKey == stem.SubObjectKey, what, nameof(Stem.SubObjectKey), stem.SubObjectKey);
                    target.Set(nameof(Stem.SubObjectName), subObject.SubObjectName);
                    target.Set(nameof(Stem.SubObjectUserID), subObject.SubObjectUserID);
                }
            }

            if (stemRow.IsSet(nameof(Stem.SpeciesGroupKey)))
            {
                SpeciesGroupDefinition definition = Definition<SpeciesGroupDefinition>(context, d => d.SpeciesGroupKey == stem.SpeciesGroupKey, what, nameof(Stem.SpeciesGroupKey), stem.SpeciesGroupKey);
                target.Set(nameof(Stem.SpeciesGroupName), definition.SpeciesGroupName);
                target.Set(nameof(Stem.SpeciesGroupInfo), definition.SpeciesGroupInfo);
                target.Set(nameof(Stem.SpeciesGroupUserID), definition.SpeciesGroupUserID);
                target.Set(nameof(Stem.SpeciesGroupVersion), definition.SpeciesGroupVersion);
            }

            if (stemRow.IsSet(nameof(Stem.OperatorKey)))
            {
                OperatorDefinition definition = Definition<OperatorDefinition>(context, d => d.OperatorKey == stem.OperatorKey, what, nameof(Stem.OperatorKey), stem.OperatorKey);
                target.Set(nameof(Stem.OperatorBusinessName), definition.OperatorBusinessName);
                target.Set(nameof(Stem.OperatorUserID), definition.OperatorUserID);
            }

            if (Enclosing.Row<Machine>(stemRow, context) is { } machine)
            {
                Machine value = machine.Row.AsType<Machine>();
                target.Set(nameof(Stem.MachineUserID), value.MachineUserID);
                target.Set(nameof(Stem.LoggingContractorBusinessID), value.LoggingContractorBusinessID);
            }
        }

        private static void CopyIfSet(ContextRow from, ContextRow to, string member, int value)
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
        public override void Complete(ContextRow row, DeserializeContext context, Action<ContextRow> next)
        {
            Dimensions.FromStem(row, row, context, $"Stem {row.Row.AsType<Stem>().StemKey}");
            next(row);
        }
    }

    /// <summary>
    /// A log's dimensions: its stem's (copied, since the stem completes after its logs), and its
    /// product's and destination's
    /// </summary>
    internal sealed class LogDimensionPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        public override void Complete(ContextRow row, DeserializeContext context, Action<ContextRow> next)
        {
            Log log = row.Row.AsType<Log>();
            ContextRow stem = Harvesting.StemRow(row, context);
            string what = $"Log {log.LogKey} of Stem {stem.Row.AsType<Stem>().StemKey}";

            Dimensions.FromStem(stem, row, context, what);

            if (row.IsSet(nameof(Log.ProductKey)))
            {
                ProductDefinition product = Dimensions.Definition<ProductDefinition>(context, d => d.ProductKey == log.ProductKey, what, nameof(Log.ProductKey), log.ProductKey);
                row.Set(nameof(Log.ProductName), product.ProductName);
                row.Set(nameof(Log.ProductInfo), product.ProductInfo);
                row.Set(nameof(Log.ProductVersion), product.ProductVersion);
                row.Set(nameof(Log.ProductUserID), product.ProductUserID);
                row.Set(nameof(Log.ProductDestinationBusinessName), product.ProductDestinationBusinessName);
                row.Set(nameof(Log.ProductDestinationBusinessID), product.ProductDestinationBusinessID);
            }

            next(row);
        }
    }
}
