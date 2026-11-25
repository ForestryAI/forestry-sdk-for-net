using Forestry.Serialization;
using Forestry.Serialization.Policies;
using Forestry.Serialization.Reading;

namespace Forestry.StanForD.Policies {
    /// <summary>
    /// The machine never comes out: it stays open around every stem, where the policies find it as an
    /// ancestor
    /// </summary>
    internal sealed class MachinePolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        public override void Complete(ContextRow row, DeserializeContext context, Action<ContextRow> next)
        {
            row.Enumerate(false);
            next(row);
        }
    }

    /// <summary>
    /// A stem's identification (FR-001): the machine key, and <c>Id</c> = "{machine key}-{stem key}";
    /// its object, species group and operator keys must refer to definitions earlier in the document
    /// (FR-003)
    /// </summary>
    internal sealed class StemIdentificationPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        /// <exception cref="InvalidDataException">No machine key, or a reference to an undefined
        /// definition</exception>
        public override void Complete(ContextRow row, DeserializeContext context, Action<ContextRow> next)
        {
            Stem stem = row.Row.AsType<Stem>();
            string what = $"Stem {stem.StemKey}";
            Guid machineKey = Harvesting.MachineKey(row, context, what);

            Harvesting.RequireDefinition<ObjectDefinition>(row, context, what, nameof(Stem.ObjectKey), stem.ObjectKey, definition => definition.ObjectKey);
            Harvesting.RequireDefinition<SpeciesGroupDefinition>(row, context, what, nameof(Stem.SpeciesGroupKey), stem.SpeciesGroupKey, definition => definition.SpeciesGroupKey);
            Harvesting.RequireDefinition<OperatorDefinition>(row, context, what, nameof(Stem.OperatorKey), stem.OperatorKey, definition => definition.OperatorKey);

            row.Set(nameof(Stem.MachineKey), machineKey);
            row.Set(nameof(Stem.Id), Identification.Format(machineKey, stem.StemKey));

            next(row);
        }
    }

    /// <summary>
    /// A log's identification (FR-002): the machine key, its stem's key, and <c>Id</c> =
    /// "{machine key}-{stem key}-{log key}"; its product key must refer to a definition earlier in the
    /// document (FR-003)
    /// </summary>
    internal sealed class LogIdentificationPolicy : EnumerationPolicy
    {
        /// <inheritdoc/>
        /// <exception cref="InvalidDataException">No machine key, no enclosing stem, or a reference to an
        /// undefined definition</exception>
        public override void Complete(ContextRow row, DeserializeContext context, Action<ContextRow> next)
        {
            Log log = row.Row.AsType<Log>();
            Stem stem = Harvesting.Stem(row, context);
            string what = $"Log {log.LogKey} of Stem {stem.StemKey}";
            Guid machineKey = Harvesting.MachineKey(row, context, what);

            Harvesting.RequireDefinition<ProductDefinition>(row, context, what, nameof(Log.ProductKey), log.ProductKey, definition => definition.ProductKey);

            row.Set(nameof(Log.MachineKey), machineKey);
            row.Set(nameof(Log.StemKey), stem.StemKey);
            row.Set(nameof(Log.Id), Identification.Format(machineKey, stem.StemKey, log.LogKey));

            next(row);
        }
    }
}
