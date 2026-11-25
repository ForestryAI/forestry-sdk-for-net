using Forestry.Serialization;
using Forestry.Serialization.Policies;
using Forestry.Serialization.Reading;

namespace Forestry.StanForD.Policies {
    /// <summary>
    /// A common definition (object, species group, operator, product) never comes out; it is kept in
    /// the deserialization context until the document ends, so stems and logs after it can find it,
    /// with its machine key and identification set
    /// </summary>
    /// <typeparam name="TDefinition">The definition's telemetry type</typeparam>
    /// <param name="keyMember">The definition's key member, e.g. <c>nameof(ObjectDefinition.ObjectKey)</c></param>
    /// <param name="key">Reads the definition's key</param>
    internal sealed class CommonDefinitionPolicy<TDefinition>(string keyMember, Func<TDefinition, int> key) : EnumerationPolicy
        where TDefinition : Telemetry
    {
        /// <inheritdoc/>
        /// <exception cref="InvalidDataException">The definition has no key, or no machine key is read
        /// before it</exception>
        public override void Complete(ContextTelemetry telemetry, DeserializeContext context, Action<ContextTelemetry> next)
        {
            Guid machineKey = Harvesting.MachineKey(telemetry, context, typeof(TDefinition).Name);

            if (!telemetry.IsSet(keyMember))
            {
                throw new InvalidDataException($"A {typeof(TDefinition).Name} has no {keyMember}.");
            }

            telemetry.Set(nameof(ObjectDefinition.MachineKey), machineKey);
            telemetry.Set(nameof(ObjectDefinition.Id), Identification.Format(machineKey, key(telemetry.Telemetry.AsType<TDefinition>())));
            telemetry.Enumerate(false);
            telemetry.Keep(Lifetime.UntilEnd);

            next(telemetry);
        }
    }
}
