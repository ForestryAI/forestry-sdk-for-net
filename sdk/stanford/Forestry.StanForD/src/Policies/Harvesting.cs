using Forestry.Serialization;
using Forestry.Serialization.Reading;

namespace Forestry.StanForD.Policies {
    /// <summary>
    /// What the Harvesting policies look up in the deserialization context
    /// </summary>
    internal static class Harvesting
    {
        /// <summary>
        /// The machine key of the <see cref="Machine"/> open around <paramref name="telemetry"/>
        /// </summary>
        /// <param name="telemetry">The telemetry completing</param>
        /// <param name="context"></param>
        /// <param name="what">The telemetry, as named in an error, e.g. "Stem 619254"</param>
        /// <exception cref="InvalidDataException">No machine key is read before the telemetry (FR-005)</exception>
        public static Guid MachineKey(ContextTelemetry telemetry, DeserializeContext context, string what)
        {
            ContextTelemetry? machine = context.Ancestors(telemetry).FirstOrDefault(ancestor => ancestor.Telemetry is Machine);

            if (machine is null || !machine.IsSet(nameof(Machine.MachineKey)))
            {
                throw new InvalidDataException($"{what} has no {nameof(Machine.MachineKey)}: no Machine/{nameof(Machine.MachineKey)} is read before it.");
            }

            return machine.Telemetry.AsType<Machine>().MachineKey;
        }

        /// <summary>
        /// The <see cref="Stem"/> open around a log
        /// </summary>
        /// <exception cref="InvalidDataException">The log is not inside a stem with a stem key</exception>
        public static Stem Stem(ContextTelemetry log, DeserializeContext context) =>
            StemTelemetry(log, context).Telemetry.AsType<Stem>();

        /// <summary>
        /// The <see cref="Stem"/> open around a log, as a context telemetry (to ask what the document set)
        /// </summary>
        /// <exception cref="InvalidDataException">The log is not inside a stem with a stem key</exception>
        public static ContextTelemetry StemTelemetry(ContextTelemetry log, DeserializeContext context)
        {
            ContextTelemetry? stem = context.Ancestors(log).FirstOrDefault(ancestor => ancestor.Telemetry is Stem);

            if (stem is null || !stem.IsSet(nameof(StanForD.Stem.StemKey)))
            {
                throw new InvalidDataException($"Log {log.Telemetry.AsType<Log>().LogKey} is not inside a Stem with a {nameof(StanForD.Stem.StemKey)}.");
            }

            return stem;
        }

        /// <summary>
        /// When the machine created the document, in UTC, from the kept header (FR-007)
        /// </summary>
        /// <param name="context"></param>
        /// <param name="what">The telemetry, as named in an error, e.g. "Stem 619254"</param>
        /// <exception cref="InvalidDataException">No header creation date is read before the telemetry (FR-006)</exception>
        public static DateTimeOffset HarvestingCreationTime(DeserializeContext context, string what)
        {
            ContextTelemetry? header = context.Telemetries<HarvestedProductionHeader>().FirstOrDefault();

            if (header is null || !header.IsSet(nameof(HarvestedProductionHeader.CreationDate)))
            {
                throw new InvalidDataException($"{what} has no partition time: no {nameof(HarvestedProductionHeader)}/{nameof(HarvestedProductionHeader.CreationDate)} is read before it.");
            }

            return header.Telemetry.AsType<HarvestedProductionHeader>().CreationDate.ToUniversalTime();
        }

        /// <summary>
        /// The kept <see cref="ObjectDefinition"/> a stem refers to (FR-009)
        /// </summary>
        /// <param name="stem">The stem, as a context telemetry</param>
        /// <param name="context"></param>
        /// <param name="what">The telemetry, as named in an error, e.g. "Log 1 of Stem 619254"</param>
        /// <exception cref="InvalidDataException">The stem has no object key, or no such object is kept</exception>
        public static ObjectDefinition Object(ContextTelemetry stem, DeserializeContext context, string what)
        {
            if (!stem.IsSet(nameof(StanForD.Stem.ObjectKey)))
            {
                throw new InvalidDataException($"{what} has no contract: its stem has no {nameof(StanForD.Stem.ObjectKey)}.");
            }

            int objectKey = stem.Telemetry.AsType<Stem>().ObjectKey;
            ContextTelemetry? definition = context.Telemetries<ObjectDefinition>()
                .FirstOrDefault(telemetry => !telemetry.IsOpen && telemetry.Telemetry.AsType<ObjectDefinition>().ObjectKey == objectKey);

            if (definition is null)
            {
                throw new InvalidDataException($"{what} refers to {nameof(StanForD.Stem.ObjectKey)} {objectKey}, but no {nameof(ObjectDefinition)} before it has that key.");
            }

            return definition.Telemetry.AsType<ObjectDefinition>();
        }

        /// <summary>
        /// Checks that a key set on <paramref name="telemetry"/> refers to a common definition kept earlier
        /// in the document (FR-003); a key the document does not set is not checked
        /// </summary>
        /// <typeparam name="TDefinition">The common definition's telemetry type</typeparam>
        /// <param name="telemetry">The telemetry referring</param>
        /// <param name="context"></param>
        /// <param name="what">The telemetry, as named in an error, e.g. "Stem 619254"</param>
        /// <param name="keyMember">The referring member, e.g. <c>nameof(Stem.ObjectKey)</c></param>
        /// <param name="value">The referring member's value</param>
        /// <param name="key">Reads a definition's key</param>
        /// <exception cref="InvalidDataException">No such definition is kept</exception>
        public static void RequireDefinition<TDefinition>(
            ContextTelemetry telemetry,
            DeserializeContext context,
            string what,
            string keyMember,
            int value,
            Func<TDefinition, int> key) where TDefinition : Telemetry
        {
            if (!telemetry.IsSet(keyMember))
            {
                return;
            }

            if (!context.Telemetries<TDefinition>().Any(definition => !definition.IsOpen && key(definition.Telemetry.AsType<TDefinition>()) == value))
            {
                throw new InvalidDataException($"{what} refers to {keyMember} {value}, but no {typeof(TDefinition).Name} before it has that key.");
            }
        }
    }
}
