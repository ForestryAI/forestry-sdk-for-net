using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Converters.Primitive;
using Forestry.Serialization.Definitions;

namespace Forestry.Serialization
{
    /// <summary>
    /// What reading and writing share (spec 004 research R44): the telemetry types, the converters, and the
    /// type and property definitions built from them when the options lock. <see cref="DeserializeOptions"/>
    /// reads, calling a converter's <see cref="Converter{T}.Read"/>; <see cref="SerializeOptions"/> writes,
    /// calling its <see cref="Converter{T}.Write"/>
    /// </summary>
    /// <remarks>
    /// Only <see cref="DeserializeOptions"/> and <see cref="SerializeOptions"/> derive from it; a format
    /// package derives from those. Each options instance builds and locks its own definitions, never shared
    /// with another instance (research R5, S0).
    /// </remarks>
    public abstract class TelemetrySerializerOptions
    {
        private protected TelemetrySerializerOptions()
        {
            Telemetries = new TelemetryTypes(this);
        }

        #region Telemetry types
        /// <summary>
        /// Telemetry types added explicitly: only their telemetries are read or written (research R12)
        /// </summary>
        public TelemetryTypes Telemetries { get; }
        #endregion

        #region Converters
        /// <summary>
        /// Converters added by the developer; they win over the format's defaults and the primitives
        /// (research R14). In <see cref="DeserializeOptions"/> a converter is used to read
        /// (<see cref="Converter{T}.Read"/>); in <see cref="SerializeOptions"/>, to write
        /// (<see cref="Converter{T}.Write"/>). The same converter can be added to both
        /// </summary>
        public IList<Converter> Converters => _converters ??= new OptionsObservableList<Converter>(this);

        private OptionsObservableList<Converter>? _converters;

        /// <summary>
        /// The format's default converters, after the developer's, before the primitives (e.g. XML Schema
        /// booleans when reading). In <see cref="DeserializeOptions"/> they read; in
        /// <see cref="SerializeOptions"/> they write
        /// </summary>
        protected virtual IEnumerable<Converter> FormatConverters => [];

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
            new NullableConverterFactory(),
            new Utf8SpanParsableConverterFactory()
        ];

        /// <summary>
        /// Resolved converter of a type: the developer's converters, then the format's defaults, then
        /// the primitives; null when none handles it (research R14)
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

            resolved = FindConverter(_converters, type) ?? FindConverter(FormatConverters, type) ?? FindConverter(s_primitiveConverters, type);
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
        /// Before the definitions are built, under the options' lock (e.g. the deserialize options resolve
        /// their policies)
        /// </summary>
        private protected virtual void OnBuildingDefinitions()
        {
        }

        /// <summary>
        /// Which members of a telemetry type are mapped (research R44). Default: every member the type
        /// definition maps; the deserialize options apply their definition policies
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private protected virtual Func<MemberInfo, bool> IncludeMember(Type type) => static _ => true;

        /// <summary>
        /// After the definitions are built, under the options' lock; throws to reject them (research R44)
        /// </summary>
        /// <param name="typeDefinitions"></param>
        private protected virtual void OnDefinitionsBuilt(FrozenDictionary<Type, TypeDefinition> typeDefinitions)
        {
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

        #region Read-only
        /// <summary>
        /// Locked for changes: the definitions are built
        /// </summary>
        public bool IsReadOnly => _isReadOnly;

        private volatile bool _isReadOnly;

        private readonly object _gate = new();

        /// <summary>
        /// Lock the options and build their definitions, as the first use does; throws when an added
        /// telemetry type cannot be defined. Idempotent
        /// </summary>
        public void MakeReadOnly()
        {
            if (_isReadOnly)
            {
                return;
            }

            lock (_gate)
            {
                if (_isReadOnly)
                {
                    return;
                }

                OnBuildingDefinitions();
                _typeDefinitions = BuildTypeDefinitions();
                OnDefinitionsBuilt(_typeDefinitions);
                OnMakingReadOnly();
                _isReadOnly = true;
            }
        }

        /// <summary>
        /// The format builds what it derives from the definitions, once, before the options are locked;
        /// it throws to reject them, at the call like any definition error (FR-008)
        /// </summary>
        protected virtual void OnMakingReadOnly() { }

        /// <summary>
        /// Throws when read-only
        /// </summary>
        internal void ThrowingAssertNotReadOnly()
        {
            if (_isReadOnly)
            {
                Throwing.WhenOptionsAreReadOnly();
            }
        }
        #endregion

        /// <summary>
        /// List locked with the options; private protected, since formats only need the
        /// <see cref="IList{T}"/> it is exposed as (research R44)
        /// </summary>
        private protected sealed class OptionsObservableList<T>(TelemetrySerializerOptions options) : ObservableList<T>
        {
            public override bool IsReadOnly => options.IsReadOnly;

            protected override void Before()
            {
                options.ThrowingAssertNotReadOnly();
            }
        }
    }
}
