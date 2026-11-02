using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Forestry.Deserialize.Converters;
using Forestry.Deserialize.Converters.BuiltIn;
using Forestry.Deserialize.Definitions;

namespace Forestry.Deserialize
{
    /// <summary>
    /// Definitions built once when the options lock, kept per options and never shared between
    /// options instances (research R5, S0)
    /// </summary>
    public abstract partial class DeserializeOptions
    {
        /// <summary>
        /// Row type lookup, fixed once locked
        /// </summary>
        private FrozenDictionary<Type, TypeDefinition>? _typeDefinitions;

        /// <summary>
        /// Converters resolved per type while definitions are built
        /// </summary>
        private Dictionary<Type, Converter?>? _resolvedConverters;

        /// <summary>
        /// Built-in converters, last in the precedence
        /// </summary>
        private static readonly Converter[] s_builtInConverters =
        [
            new StringConverter(),
            new BooleanConverter(),
            new DateTimeConverter(),
            new DateTimeOffsetConverter(),
            new NullableConverterFactory(),
            new Utf8SpanParsableConverterFactory()
        ];

        /// <summary>
        /// Type definition of an added row type once the options are read-only
        /// </summary>
        /// <param name="type"></param>
        /// <param name="typeDefinition"></param>
        /// <returns></returns>
        internal bool TryGetTypeDefinition(Type type, [NotNullWhen(true)] out TypeDefinition? typeDefinition)
        {
            if (_typeDefinitions is { } typeDefinitions)
            {
                return typeDefinitions.TryGetValue(type, out typeDefinition);
            }

            typeDefinition = null;
            return false;
        }

        /// <summary>
        /// Type definition of an added row type; any other type throws (S0)
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal TypeDefinition ThrowingGetTypeDefinition(Type type)
        {
            if (!TryGetTypeDefinition(type, out TypeDefinition? typeDefinition))
            {
                Throwing.WhenRowTypeNotAdded(type);
            }

            return typeDefinition;
        }

        /// <summary>
        /// Effective converter of a type: the developer's converters, then the media's defaults, then
        /// the built-ins; null when none handles it (research R14)
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal Converter? ResolveConverter(Type type)
        {
            _resolvedConverters ??= [];

            if (_resolvedConverters.TryGetValue(type, out Converter? resolved))
            {
                return resolved;
            }

            resolved = FindConverter(_converters, type) ?? FindConverter(MediaConverters, type) ?? FindConverter(s_builtInConverters, type);
            _resolvedConverters[type] = resolved;

            return resolved;
        }

        private Converter? FindConverter(IEnumerable<Converter>? converters, Type type)
        {
            if (converters is null)
            {
                return null;
            }

            foreach (Converter converter in converters)
            {
                if (converter.CanConvert(type))
                {
                    return converter is ConverterFactory factory ? factory.CreateConverter(type, this) : converter;
                }
            }

            return null;
        }

        /// <summary>
        /// Build the type definitions of the added row types, under the options' lock
        /// </summary>
        /// <returns></returns>
        private FrozenDictionary<Type, TypeDefinition> BuildTypeDefinitions()
        {
            if (Rows.Count == 0)
            {
                Throwing.WhenNoRowTypes();
            }

            Dictionary<Type, TypeDefinition> typeDefinitions = [];

            foreach (Type type in Rows)
            {
                typeDefinitions[type] = TypeDefinition.Create(type, this, isHandedOut: true);
            }

            _resolvedConverters = null;
            return typeDefinitions.ToFrozenDictionary();
        }
    }
}
