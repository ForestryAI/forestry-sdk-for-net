using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Converters.Primitive;
using Forestry.Serialization.Definitions;

namespace Forestry.Serialization
{
    /// <summary>
    /// Definitions and converters resolved once when the options lock, kept per options and never shared
    /// between options instances (research R5, S0; spec 004 R44)
    /// </summary>
    public abstract partial class TelemetrySerializationOptions
    {
        #region Converters
        /// <summary>
        /// Converters resolved per type while definitions are built
        /// </summary>
        private Dictionary<Type, Converter?>? _resolvedConverters;

        /// <summary>
        /// Primitive converters, last in the precedence
        /// </summary>
        private static readonly Converter[] s_primitiveConverters =
        [
            new StringConverter(),
            new BooleanConverter(),
            new DateTimeConverter(),
            new DateTimeOffsetConverter(),
            new DateOnlyConverter(),
            new TimeOnlyConverter(),
            new TimeSpanConverter(),
            new EnumConverterFactory(),
            new NullableConverterFactory(),
            new Utf8SpanParsableConverterFactory()
        ];

        /// <summary>
        /// Resolved converter of a type, on a member when one is given: the developer's converters, then the
        /// format's defaults, then the primitives; null when none handles it (research R14). Shared by type,
        /// except a converter from a factory that <see cref="ConverterFactory.UsesMember"/>, created per member
        /// (spec 005 research R58)
        /// </summary>
        /// <param name="type"></param>
        /// <param name="member">The telemetry member the converter is for; none for a telemetry type's own converter</param>
        /// <returns></returns>
        internal Converter? ResolveConverter(Type type, MemberInfo? member = null)
        {
            _resolvedConverters ??= [];

            if (_resolvedConverters.TryGetValue(type, out Converter? resolved))
            {
                return resolved;
            }

            bool shared = true;
            resolved = FindConverter(_converters, type, member, ref shared)
                ?? FindConverter(FormatConverters, type, member, ref shared)
                ?? FindConverter(s_primitiveConverters, type, member, ref shared);

            if (shared)
            {
                _resolvedConverters[type] = resolved;
            }

            return resolved;
        }

        private Converter? FindConverter(IEnumerable<Converter>? converters, Type type, MemberInfo? member, ref bool shared)
        {
            if (converters is null)
            {
                return null;
            }

            foreach (Converter converter in converters)
            {
                if (!converter.CanConvert(type))
                {
                    continue;
                }

                if (converter is not ConverterFactory factory)
                {
                    return converter;
                }

                if (factory.UsesMember)
                {
                    shared = false;
                    return member is null ? factory.CreateConverter(type, this) : factory.CreateConverter(type, member, this);
                }

                return factory.CreateConverter(type, this);
            }

            return null;
        }
        #endregion

        #region Definitions
        /// <summary>
        /// Telemetry type lookup, fixed once locked
        /// </summary>
        private FrozenDictionary<Type, TypeDefinition>? _typeDefinitions;

        /// <summary>
        /// Type definition of an added telemetry type once the options are read-only
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
        /// Type definition of an added telemetry type; any other type throws (S0)
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal TypeDefinition ThrowingGetTypeDefinition(Type type)
        {
            if (!TryGetTypeDefinition(type, out TypeDefinition? typeDefinition))
            {
                Throwing.WhenTelemetryTypeNotAdded(type);
            }

            return typeDefinition;
        }

        /// <summary>
        /// Build the type definitions of the added telemetry types under the options' lock
        /// </summary>
        /// <returns></returns>
        private FrozenDictionary<Type, TypeDefinition> BuildTypeDefinitions()
        {
            if (Telemetries.Count == 0)
            {
                Throwing.WhenNoTelemetryTypes();
            }

            // A failed build leaves the options unlocked: forget what it resolved (a converter may have been added since)
            _resolvedConverters = null;

            Dictionary<Type, TypeDefinition> typeDefinitions = [];

            foreach (Type type in Telemetries)
            {
                typeDefinitions[type] = TypeDefinition.Create(type, this, IncludeMember(type));
            }

            _resolvedConverters = null;
            return typeDefinitions.ToFrozenDictionary();
        }
        #endregion
    }
}
