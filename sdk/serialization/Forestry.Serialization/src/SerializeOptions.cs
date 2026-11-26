using System.Collections.Frozen;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization
{
    /// <summary>
    /// Options of one format for writing (e.g. JSON Lines): serializing calls each converter's
    /// <see cref="Converter{T}.Write"/>, through the format's <see cref="TelemetryWriter"/>. Shares with
    /// <see cref="DeserializeOptions"/> what reading and writing have in common
    /// (<see cref="TelemetrySerializerOptions"/>: telemetry types, converters, definitions). Once first used
    /// they are read-only (spec 004 research R44, R49)
    /// </summary>
    public abstract class SerializeOptions : TelemetrySerializerOptions
    {
        /// <summary>
        /// Options of a format, for writing
        /// </summary>
        protected SerializeOptions()
        {
        }

        /// <summary>
        /// The format's writer: how a telemetry is framed and how each value is represented. Called once per
        /// serialize call, since a writer keeps state between the telemetries of a call (e.g. whether a member
        /// was written). Protected, so a format package needs no access to the core's internals (research R49)
        /// </summary>
        /// <returns></returns>
        protected abstract TelemetryWriter CreateWriter();

        /// <summary>
        /// A new writer for one serialize call
        /// </summary>
        /// <returns></returns>
        internal TelemetryWriter CreateWriterForCall() => CreateWriter() ?? throw new InvalidOperationException(Formatting.Format(Formatting.NoTelemetryWriter, GetType().FullName));

        /// <summary>
        /// Refuse, when the options lock, any member that cannot be written: a converter that only reads, or a
        /// property without a getter; one exception per telemetry type, before any byte is written (FR-008, S1)
        /// </summary>
        /// <param name="typeDefinitions"></param>
        private protected override void OnDefinitionsBuilt(FrozenDictionary<Type, TypeDefinition> typeDefinitions)
        {
            foreach ((Type type, TypeDefinition definition) in typeDefinitions)
            {
                List<string>? failures = null;

                foreach (PropertyDefinition property in definition.Properties)
                {
                    if (!property.CanGet)
                    {
                        (failures ??= []).Add(Formatting.Format(Formatting.MemberNotReadable, property.Name));
                    }
                    else if (!property.ResolvedConverter.CanWrite)
                    {
                        (failures ??= []).Add(Formatting.Format(
                            Formatting.MemberConverterCannotWrite,
                            property.Name,
                            Throwing.DisplayName(property.MemberType),
                            Throwing.DisplayName(property.ResolvedConverter.GetType())));
                    }
                }

                if (failures is not null)
                {
                    Throwing.WhenTelemetryTypeCannotBeWritten(type, failures);
                }
            }
        }
    }
}
