using System.Globalization;

namespace Forestry.Deserialize.Converters.BuiltIn
{
    /// <summary>
    /// Built-in converter for every type parsable from UTF-8 (integers, <see cref="decimal"/>,
    /// <see cref="double"/>, ...), with invariant culture
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal sealed class Utf8SpanParsableConverter<T> : Converter<T> where T : IUtf8SpanParsable<T>
    {
        public override T Convert(ReadOnlySpan<byte> utf8)
        {
            if (T.TryParse(utf8, CultureInfo.InvariantCulture, out T? value))
            {
                return value;
            }

            throw new FormatException(Formatting.Format(Formatting.NotParsable, typeof(T).Name));
        }
    }

    /// <summary>
    /// Built-in factory of <see cref="Utf8SpanParsableConverter{T}"/>
    /// </summary>
    internal sealed class Utf8SpanParsableConverterFactory : ConverterFactory
    {
        public override bool CanConvert(Type type) =>
            type.IsValueType &&
            !type.IsGenericTypeDefinition &&
            Array.Exists(type.GetInterfaces(), contract =>
                contract.IsGenericType &&
                contract.GetGenericTypeDefinition() == typeof(IUtf8SpanParsable<>) &&
                contract.GenericTypeArguments[0] == type);

        public override Converter CreateConverter(Type type, DeserializeOptions options) =>
            (Converter)Activator.CreateInstance(typeof(Utf8SpanParsableConverter<>).MakeGenericType(type))!;
    }
}
