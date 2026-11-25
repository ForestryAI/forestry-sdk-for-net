using Forestry.Serialization;
using Forestry.Serialization.Policies;
using Forestry.Serialization.Reading;

namespace Forestry.StanForD.Policies {
    /// <summary>
    /// A common definition (object, species group, operator, product) never comes out; it is kept in
    /// the deserialization context until the document ends, so stems and logs after it can find it,
    /// with its machine key and identification set
    /// </summary>
    /// <typeparam name="TDefinition">The definition's row type</typeparam>
    /// <param name="keyMember">The definition's key member, e.g. <c>nameof(ObjectDefinition.ObjectKey)</c></param>
    /// <param name="key">Reads the definition's key</param>
    internal sealed class CommonDefinitionPolicy<TDefinition>(string keyMember, Func<TDefinition, int> key) : EnumerationPolicy
        where TDefinition : Row
    {
        /// <inheritdoc/>
        /// <exception cref="InvalidDataException">The definition has no key, or no machine key is read
        /// before it</exception>
        public override void Complete(ContextRow row, DeserializeContext context, Action<ContextRow> next)
        {
            Guid machineKey = Harvesting.MachineKey(row, context, typeof(TDefinition).Name);

            if (!row.IsSet(keyMember))
            {
                throw new InvalidDataException($"A {typeof(TDefinition).Name} has no {keyMember}.");
            }

            row.Set(nameof(ObjectDefinition.MachineKey), machineKey);
            row.Set(nameof(ObjectDefinition.Id), Identification.Format(machineKey, key(row.Row.AsType<TDefinition>())));
            row.Enumerate(false);
            row.Keep(Lifetime.UntilEnd);

            next(row);
        }
    }
}
