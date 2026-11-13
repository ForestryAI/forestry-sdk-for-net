using Forestry.Deserialize;
using Forestry.Deserialize.Reading;

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
        public static Guid MachineKey(ContextRow row, DeserializationContext context, string what)
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
        public static Stem Stem(ContextRow log, DeserializationContext context)
        {
            ContextRow? stem = context.Ancestors(log).FirstOrDefault(ancestor => ancestor.Row is Stem);

            if (stem is null || !stem.IsSet(nameof(StanForD.Stem.StemKey)))
            {
                throw new InvalidDataException($"Log {log.Row.AsType<Log>().LogKey} is not inside a Stem with a {nameof(StanForD.Stem.StemKey)}.");
            }

            return stem.Row.AsType<Stem>();
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
            DeserializationContext context,
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
