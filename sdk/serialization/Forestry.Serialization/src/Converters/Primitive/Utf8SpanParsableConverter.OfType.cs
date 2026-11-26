using System.Globalization;
using System.Runtime.CompilerServices;
using Forestry.Serialization.Writing;

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

        /// <summary>
        /// Write through the writer's overload for the value's type, so a writer that overrides, say,
        /// <see cref="TelemetryWriter.WriteValue(decimal)"/> is called for decimals (spec 004 research R49, option
        /// A): the JIT compiles this method per <typeparamref name="T"/> and keeps only the matching branch,
        /// with no boxing
        /// </summary>
        /// <param name="value"></param>
        /// <param name="writer"></param>
        public override void Write(T value, TelemetryWriter writer)
        {
            if (typeof(T) == typeof(int))
            {
                writer.WriteValue(Unsafe.As<T, int>(ref value));
            }
            else if (typeof(T) == typeof(long))
            {
                writer.WriteValue(Unsafe.As<T, long>(ref value));
            }
            else if (typeof(T) == typeof(double))
            {
                writer.WriteValue(Unsafe.As<T, double>(ref value));
            }
            else if (typeof(T) == typeof(decimal))
            {
                writer.WriteValue(Unsafe.As<T, decimal>(ref value));
            }
            else if (typeof(T) == typeof(Guid))
            {
                writer.WriteValue(Unsafe.As<T, Guid>(ref value));
            }
            else
            {
                writer.WriteValue(value);
            }
        }
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

        public override Converter CreateConverter(Type type, TelemetrySerializationOptions options) =>
            (Converter)Activator.CreateInstance(typeof(Utf8SpanParsableConverter<>).MakeGenericType(type))!;
    }
}
