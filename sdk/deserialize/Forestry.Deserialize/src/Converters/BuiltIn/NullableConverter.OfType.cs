namespace Forestry.Deserialize.Converters.BuiltIn
{
    /// <summary>
    /// Built-in converter for <see cref="Nullable{T}"/> over the converter of <typeparamref name="T"/>;
    /// an empty value is null before any converter is called (research R14)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal sealed class NullableConverter<T>(Converter<T> converter) : Converter<T?> where T : struct
    {
        public override T? Convert(ReadOnlySpan<byte> utf8) => converter.Convert(utf8);
    }

    /// <summary>
    /// Built-in factory of <see cref="NullableConverter{T}"/>
    /// </summary>
    internal sealed class NullableConverterFactory : ConverterFactory
    {
        public override bool CanConvert(Type type) => Nullable.GetUnderlyingType(type) is not null;

        public override Converter CreateConverter(Type type, DeserializeOptions options)
        {
            Type underlying = Nullable.GetUnderlyingType(type)!;
            Converter? converter = options.ResolveConverter(underlying);

            if (converter is null)
            {
                Throwing.WhenNoConverter(type);
            }

            return (Converter)Activator.CreateInstance(typeof(NullableConverter<>).MakeGenericType(underlying), converter)!;
        }
    }
}
