using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Forestry.Serialization.Converters;

namespace Forestry.Serialization.Xml.Converters
{
    /// <summary>
    /// The XML enum converter, in the form its member asks for (spec 005 research R64)
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    internal sealed class XmlEnumConverter<TEnum>(EnumForm form) : EnumConverter<TEnum>(form) where TEnum : struct, Enum
    {
    }

    /// <summary>
    /// Creates XML enum converters per member: <see cref="XmlEnumFormAttribute"/> on the member, or names; one
    /// converter per enum type and form (spec 005 research R58, R64)
    /// </summary>
    internal sealed class XmlEnumConverterFactory : ConverterFactory
    {
        private readonly ConcurrentDictionary<(Type Type, EnumForm Form), Converter> _converters = new();

        public override bool UsesMember => true;

        public override bool CanConvert(Type type) => type.IsEnum;

        public override Converter CreateConverter(Type type, TelemetrySerializationOptions options) => Create(type, EnumForm.Name);

        public override Converter CreateConverter(Type type, MemberInfo member, TelemetrySerializationOptions options) =>
            Create(type, member.GetCustomAttribute<XmlEnumFormAttribute>()?.Form ?? EnumForm.Name);

        private Converter Create(Type type, EnumForm form) => _converters.GetOrAdd((type, form), static key =>
        {
            try
            {
                return (Converter)Activator.CreateInstance(typeof(XmlEnumConverter<>).MakeGenericType(key.Type), key.Form)!;
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
