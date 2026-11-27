using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Forestry.Serialization.Converters;

namespace Forestry.Serialization.JsonLines.Converters
{
    /// <summary>
    /// JSON Lines' enum converter, in the form its member asks for (spec 005 research R65)
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    internal sealed class JsonLinesEnumConverter<TEnum>(EnumForm form) : EnumConverter<TEnum>(form) where TEnum : struct, Enum
    {
    }

    /// <summary>
    /// Creates JSON Lines' enum converters per member: <see cref="JsonLinesEnumFormAttribute"/> on the member,
    /// or names; one converter per enum type and form (spec 005 research R58, R65)
    /// </summary>
    internal sealed class JsonLinesEnumConverterFactory : ConverterFactory
    {
        private readonly ConcurrentDictionary<(Type Type, EnumForm Form), Converter> _converters = new();

        public override bool UsesMember => true;

        public override bool CanConvert(Type type) => type.IsEnum;

        public override Converter CreateConverter(Type type, TelemetrySerializationOptions options) => Create(type, EnumForm.Name);

        public override Converter CreateConverter(Type type, MemberInfo member, TelemetrySerializationOptions options) =>
            Create(type, member.GetCustomAttribute<JsonLinesEnumFormAttribute>()?.Form ?? EnumForm.Name);

        private Converter Create(Type type, EnumForm form) => _converters.GetOrAdd((type, form), static key =>
        {
            try
            {
                return (Converter)Activator.CreateInstance(typeof(JsonLinesEnumConverter<>).MakeGenericType(key.Type), key.Form)!;
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                // A member name clash fails as itself, not wrapped by reflection
                ExceptionDispatchInfo.Throw(exception.InnerException);
                throw;
            }
        });
    }
}
