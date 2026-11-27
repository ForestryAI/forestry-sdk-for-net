using System.Reflection;
using Forestry.Serialization.Converters;

namespace Forestry.Serialization.Tests.Fixtures
{
    /// <summary>
    /// An enum converter in a given form, as a format's enum converter is (spec 005 research R59)
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    public sealed class TestEnumConverter<TEnum>(EnumForm form) : EnumConverter<TEnum>(form) where TEnum : struct, Enum
    {
    }

    /// <summary>
    /// A factory that chooses per member, as a format's attribute-reading factory does (research R58): number
    /// form for members whose name ends in <c>Number</c>, name form otherwise
    /// </summary>
    public sealed class MemberAwareEnumFactory : ConverterFactory
    {
        public override bool UsesMember => true;

        public override bool CanConvert(Type type) => type.IsEnum;

        public override Converter CreateConverter(Type type, TelemetrySerializationOptions options) => Create(type, EnumForm.Name);

        public override Converter CreateConverter(Type type, MemberInfo member, TelemetrySerializationOptions options) =>
            Create(type, member.Name.EndsWith("Number", StringComparison.Ordinal) ? EnumForm.Number : EnumForm.Name);

        private static Converter Create(Type type, EnumForm form) =>
            (Converter)Activator.CreateInstance(typeof(TestEnumConverter<>).MakeGenericType(type), form)!;
    }
}
