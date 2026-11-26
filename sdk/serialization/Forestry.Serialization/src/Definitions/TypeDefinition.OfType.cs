using Forestry.Serialization.Converters;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Definitions
{
    /// <summary>
    /// Definition of the telemetry type <typeparamref name="T"/>, with the object converter that writes its
    /// telemetries (spec 004 research R49), as System.Text.Json's <c>JsonTypeInfo&lt;T&gt;</c> has its converter
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class TypeDefinition<T> : TypeDefinition where T : Telemetry
    {
        internal TypeDefinition(Type type, TelemetrySerializationOptions options, Func<Telemetry> constructor, PropertyDefinition[] properties)
            : base(type, options, constructor, properties)
        {
        }

        /// <summary>
        /// The object converter of <typeparamref name="T"/>, resolved from the options when the definition is
        /// built: the developer's, then the format's, then the default, which writes each property in turn
        /// </summary>
        public ObjectConverter<T> Converter { get; private set; } = null!;

        private protected override string? ResolveObjectConverter(TelemetrySerializationOptions options)
        {
            Converter? resolved = options.ResolveConverter(typeof(T));

            switch (resolved)
            {
                case null:
                    Converter = new ObjectDefaultConverter<T>(this);
                    return null;
                case ObjectConverter<T> objectConverter:
                    Converter = objectConverter;
                    return null;
                default:
                    return Formatting.Format(Formatting.TelemetryTypeConverterNotObjectConverter, Throwing.DisplayName(resolved.GetType()));
            }
        }

        internal override void WriteTelemetry(Telemetry telemetry, TelemetryWriter writer) => Converter.Write((T)telemetry, writer);
    }
}
