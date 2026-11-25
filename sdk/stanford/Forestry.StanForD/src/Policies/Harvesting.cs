using Forestry.Serialization;
using Forestry.Serialization.Reading;

namespace Forestry.StanForD.Policies {
    /// <summary>
    /// What the Harvesting policies look up in the deserialization context
    /// </summary>
    internal static class Harvesting
    {
        /// <summary>
        /// The machine key of the <see cref="Machine"/> open around <paramref name="row"/>
        /// </summary>
        /// <param name="row">The row completing</param>
        /// <param name="context"></param>
        /// <param name="what">The row, as named in an error, e.g. "Stem 619254"</param>
        /// <exception cref="InvalidDataException">No machine key is read before the row (FR-005)</exception>
        public static Guid MachineKey(ContextRow row, DeserializeContext context, string what)
        {
            ContextRow? machine = context.Ancestors(row).FirstOrDefault(ancestor => ancestor.Row is Machine);

            if (machine is null || !machine.IsSet(nameof(Machine.MachineKey)))
            {
                throw new InvalidDataException($"{what} has no {nameof(Machine.MachineKey)}: no Machine/{nameof(Machine.MachineKey)} is read before it.");
            }

            return machine.Row.AsType<Machine>().MachineKey;
        }

        /// <summary>
        /// The <see cref="Stem"/> open around a log
        /// </summary>
        /// <exception cref="InvalidDataException">The log is not inside a stem with a stem key</exception>
        public static Stem Stem(ContextRow log, DeserializeContext context) =>
            StemRow(log, context).Row.AsType<Stem>();

        /// <summary>
        /// The <see cref="Stem"/> open around a log, as a context row (to ask what the document set)
        /// </summary>
        /// <exception cref="InvalidDataException">The log is not inside a stem with a stem key</exception>
        public static ContextRow StemRow(ContextRow log, DeserializeContext context)
        {
            ContextRow? stem = context.Ancestors(log).FirstOrDefault(ancestor => ancestor.Row is Stem);

            if (stem is null || !stem.IsSet(nameof(StanForD.Stem.StemKey)))
            {
                throw new InvalidDataException($"Log {log.Row.AsType<Log>().LogKey} is not inside a Stem with a {nameof(StanForD.Stem.StemKey)}.");
            }

            return stem;
        }

        /// <summary>
        /// When the machine created the document, in UTC, from the kept header (FR-007)
        /// </summary>
        /// <param name="context"></param>
        /// <param name="what">The row, as named in an error, e.g. "Stem 619254"</param>
        /// <exception cref="InvalidDataException">No header creation date is read before the row (FR-006)</exception>
        public static DateTimeOffset HarvestingCreationTime(DeserializeContext context, string what)
        {
            ContextRow? header = context.Rows<HarvestedProductionHeader>().FirstOrDefault();

            if (header is null || !header.IsSet(nameof(HarvestedProductionHeader.CreationDate)))
            {
                throw new InvalidDataException($"{what} has no partition time: no {nameof(HarvestedProductionHeader)}/{nameof(HarvestedProductionHeader.CreationDate)} is read before it.");
            }

            return header.Row.AsType<HarvestedProductionHeader>().CreationDate.ToUniversalTime();
        }

        /// <summary>
        /// The kept <see cref="ObjectDefinition"/> a stem refers to (FR-009)
        /// </summary>
        /// <param name="stem">The stem, as a context row</param>
        /// <param name="context"></param>
        /// <param name="what">The row, as named in an error, e.g. "Log 1 of Stem 619254"</param>
        /// <exception cref="InvalidDataException">The stem has no object key, or no such object is kept</exception>
        public static ObjectDefinition Object(ContextRow stem, DeserializeContext context, string what)
        {
            if (!stem.IsSet(nameof(StanForD.Stem.ObjectKey)))
            {
                throw new InvalidDataException($"{what} has no contract: its stem has no {nameof(StanForD.Stem.ObjectKey)}.");
            }

            int objectKey = stem.Row.AsType<Stem>().ObjectKey;
            ContextRow? definition = context.Rows<ObjectDefinition>()
                .FirstOrDefault(row => !row.IsOpen && row.Row.AsType<ObjectDefinition>().ObjectKey == objectKey);

            if (definition is null)
            {
                throw new InvalidDataException($"{what} refers to {nameof(StanForD.Stem.ObjectKey)} {objectKey}, but no {nameof(ObjectDefinition)} before it has that key.");
            }

            return definition.Row.AsType<ObjectDefinition>();
        }

        /// <summary>
        /// Checks that a key set on <paramref name="row"/> refers to a common definition kept earlier
        /// in the document (FR-003); a key the document does not set is not checked
        /// </summary>
        /// <typeparam name="TDefinition">The common definition's row type</typeparam>
        /// <param name="row">The row referring</param>
        /// <param name="context"></param>
        /// <param name="what">The row, as named in an error, e.g. "Stem 619254"</param>
        /// <param name="keyMember">The referring member, e.g. <c>nameof(Stem.ObjectKey)</c></param>
        /// <param name="value">The referring member's value</param>
        /// <param name="key">Reads a definition's key</param>
        /// <exception cref="InvalidDataException">No such definition is kept</exception>
        public static void RequireDefinition<TDefinition>(
            ContextRow row,
            DeserializeContext context,
            string what,
            string keyMember,
            int value,
            Func<TDefinition, int> key) where TDefinition : Row
        {
            if (!row.IsSet(keyMember))
            {
                return;
            }

            if (!context.Rows<TDefinition>().Any(definition => !definition.IsOpen && key(definition.Row.AsType<TDefinition>()) == value))
            {
                throw new InvalidDataException($"{what} refers to {keyMember} {value}, but no {typeof(TDefinition).Name} before it has that key.");
            }
        }
    }
}
