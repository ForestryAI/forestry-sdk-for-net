using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Forestry.Serialization.Converters.Primitive
{
    /// <summary>
    /// The built-in enum converter: names, as every format defaults to (spec 005 FR-002, research R59)
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    internal sealed class EnumDefaultConverter<TEnum>() : EnumConverter<TEnum>(EnumForm.Name) where TEnum : struct, Enum
    {
    }

    /// <summary>
    /// Creates the built-in enum converter for any enum type, among the primitives: a developer's or a
    /// format's enum converter comes first (R14, R58)
    /// </summary>
    internal sealed class EnumConverterFactory : ConverterFactory
    {
        public override bool CanConvert(Type type) => type.IsEnum;

        public override Converter CreateConverter(Type type, TelemetrySerializationOptions options)
        {
            try
            {
                return (Converter)Activator.CreateInstance(typeof(EnumDefaultConverter<>).MakeGenericType(type))!;
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                // A name clash (R62) fails as itself, not wrapped by reflection
                ExceptionDispatchInfo.Throw(exception.InnerException);
                throw;
            }
        }
    }
}
