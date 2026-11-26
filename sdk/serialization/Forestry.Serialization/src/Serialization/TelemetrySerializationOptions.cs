using System.Collections.Frozen;
using System.Reflection;
using Forestry.Serialization.Converters;
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
    public abstract partial class TelemetrySerializationOptions
    {
        private protected TelemetrySerializationOptions()
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

        #endregion

        #region Definitions
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
        private protected sealed class OptionsObservableList<T>(TelemetrySerializationOptions options) : ObservableList<T>
        {
            public override bool IsReadOnly => options.IsReadOnly;

            protected override void Before()
            {
                options.ThrowingAssertNotReadOnly();
            }
        }
    }
}
