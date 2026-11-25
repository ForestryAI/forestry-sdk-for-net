using System.Buffers;
using System.Globalization;

namespace Forestry.Serialization.Converters.Primitive
{
    /// <summary>
    /// Primitive converter for every type parsable from and formattable to UTF-8 (integers,
    /// <see cref="decimal"/>, <see cref="double"/>, ...), with invariant culture
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal sealed class Utf8SpanParsableConverter<T> : Converter<T> where T : IUtf8SpanParsable<T>, IUtf8SpanFormattable
    {
        public override T Read(ReadOnlySpan<byte> utf8)
        {
            if (T.TryParse(utf8, CultureInfo.InvariantCulture, out T? value))
            {
                return value;
            }

            throw new FormatException(Formatting.Format(Formatting.NotParsable, typeof(T).Name));
        }

        public override void Write(T value, IBufferWriter<byte> writer) => Utf8Writing.Write(value, default, writer);
    }

    /// <summary>
    /// Primitive factory of <see cref="Utf8SpanParsableConverter{T}"/>
    /// </summary>
    internal sealed class Utf8SpanParsableConverterFactory : ConverterFactory
    {
        public override bool CanConvert(Type type) =>
            type.IsValueType &&
            !type.IsGenericTypeDefinition &&
            typeof(IUtf8SpanFormattable).IsAssignableFrom(type) &&
            Array.Exists(type.GetInterfaces(), contract =>
                contract.IsGenericType &&
                contract.GetGenericTypeDefinition() == typeof(IUtf8SpanParsable<>) &&
                contract.GenericTypeArguments[0] == type);

        public override Converter CreateConverter(Type type, DeserializeOptions options) =>
            (Converter)Activator.CreateInstance(typeof(Utf8SpanParsableConverter<>).MakeGenericType(type))!;
    }
}
