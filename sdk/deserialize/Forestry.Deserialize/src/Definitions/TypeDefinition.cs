using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Forestry.Deserialize.Deserializers;

namespace Forestry.Deserialize.Definitions
{
    /// <summary>
    /// Type definition with 3 sections:
    ///  - shape
    ///  - configuration
    ///  - creation
    /// </summary>
    /// <remarks>No support for multiple media lines e.g. JSON lines <see cref="https://jsonlines.org/"/></remarks>
    public abstract partial class TypeDefinition
    {
        protected TypeDefinition(
            Type type,
            Deserializer deserializer,
            DeserializeOptions options
        )
        {
            Type = type;
            Deserializer = deserializer;
            Options = options;

            Kind = GetTypeDefinitionKind(type, deserializer);

            ElementType = deserializer.ElementType;
            KeyType = deserializer.KeyType;
        }

        #region Shape
        /// <summary>
        /// Targeted <see cref="Type"/> when deserializing
        /// </summary>
        /// <value></value>
        public Type Type { get; }

        /// <summary>
        /// Configured <see cref="Deserializer"/>
        /// </summary>
        /// <value></value>
        public virtual Deserializer Deserializer { get; } 

        /// <summary>
        /// Options that initialized this <see cref="TypeDefinition"/>
        /// </summary>
        /// <value></value>
        public virtual DeserializeOptions Options { get; } 

        /// <summary>
        /// Shapes this definition and set by the <see cref="Deserializer"/> e.g. only objects 
        /// have properties
        /// </summary>
        /// <value></value>
        public TypeDefinitionKind Kind { get; }

        /// <summary>
        /// Get <see cref="TypeDefinitionKind"/> using deserializer kind falling back on None when factory deserializer
        /// </summary>
        /// <param name="type"></param>
        /// <param name="deserializer"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private static TypeDefinitionKind GetTypeDefinitionKind(
            Type type,
            Deserializer deserializer
        )
        {
            // TODO: When type == typeof(object) maybe just return TypeDefinitionKind.None as simple type

            switch (deserializer.DeserializerKind)
            {
                case DeserializerKind.Value: return TypeDefinitionKind.None;
                case DeserializerKind.Object: return TypeDefinitionKind.Object;
                case DeserializerKind.Enumerable: return TypeDefinitionKind.Enumerable;
                case DeserializerKind.Dictionary: return TypeDefinitionKind.Dictionary;
                case DeserializerKind.None:
                {
                    // TODO: Assert when factory deserializer + Use throwing
                    return default;
                }
                default:
                {
                    // TODO: Use Throwing but return default
                    throw new InvalidOperationException();
                }
            }
        }

        /// <summary>
        /// Optional element type with <see cref="TypeDefinitionKind.Enumerable"/>
        /// </summary>
        /// <value></value>
        public Type? ElementType { get; }

        private TypeDefinition? _elementTypeDefintion;

        /// <summary>
        /// Optional element <see cref="TypeDefinition"/> with <see cref="TypeDefinitionKind.Enumerable"/>
        /// </summary>
        /// <value></value>
        internal TypeDefinition? ElementTypeDefinition
        {
            get
            {
                Debug.Assert(IsConfigured);
                Debug.Assert(_elementTypeDefintion is null or { IsConfiguring: true });

                TypeDefinition? value = _elementTypeDefintion;
                value?.SetConfiguration();

                return value;
            }
            set
            {
                Debug.Assert(!IsInitialized);
                Debug.Assert(value is null || value.Type == ElementType);

                _elementTypeDefintion = value;
            }
        }

        /// <summary>
        /// Optional key type with <see cref="IDictionary{TKey, TValue}"/>
        /// </summary>
        /// <value></value>
        public Type? KeyType { get; }

        private TypeDefinition? _keyTypeDefinition;

        /// <summary>
        /// Optional key <see cref="TypeDefinition"/> with <see cref="IDictionary{TKey, TValue}"/>
        /// </summary>
        /// <value></value>
        internal TypeDefinition? KeyTypeDefinition
        {
            get
            {
                Debug.Assert(IsConfigured);
                Debug.Assert(_keyTypeDefinition is null or { IsConfiguring: true });

                TypeDefinition? value = _keyTypeDefinition;
                value?.SetConfiguration();

                return value;
            }
            set
            {
                Debug.Assert(!IsInitialized);
                Debug.Assert(value is null || value.Type == KeyType);

                _keyTypeDefinition = value;
            }
        }

        /// <summary>
        /// Optional element collection reference by name
        /// </summary>
        public string? ElementCollection
        {
            get => _elementCollection;
            set
            {
                ThrowingWhenIsInitialized();

                if (value is null || !Options.CollectionNamingPolicy.TryEnforce(value))
                {
                    throw new InvalidOperationException();  // TODO: Use Throwing
                }

                _elementCollection = value;
            }
        }

        private string? _elementCollection;

        /// <summary>
        /// <see cref="Value"/> children by name
        /// </summary>
        internal Dictionary<string, PropertyDefinition> PropertyDefinitionsByName
        {
            get
            {
                Debug.Assert(IsConfigured is true && _propertyDefinitionsByName is not null);
                return _propertyDefinitionsByName;
            }
        }

        private Dictionary<string, PropertyDefinition>? _propertyDefinitionsByName;
        #endregion

        #region Configuration
        private volatile ConfigurationState _configurationState;

        private ExceptionDispatchInfo? _lastConfigureException;                          

        public bool IsInitialized { get; private set; }

        public void SetInitialized() => IsInitialized = true;

        /// <summary>
        /// Only configure when <see cref="TypeDefinition"/> is not otherwise short-circuit
        /// </summary>
        internal void SetConfiguration()
        {
            if (!IsConfigured)
            {
                SynchronizeConfigure();
            }

            void SynchronizeConfigure()
            {
                Options.SetReadOnly();
                SetInitialized();

                // Before locking the type definition cache assert any configuration exception
                _lastConfigureException?.Throw();

                lock (Options.Cache)
                {
                    // When this thread has a redundant configuring || another thread has configured
                    if (_configurationState != ConfigurationState.None)
                    {
                        return;
                    }

                    // Before configuring assert any configuration exception
                    _lastConfigureException?.Throw();

                    try
                    {
                        _configurationState = ConfigurationState.Configuring;
                        Configure();
                        _configurationState = ConfigurationState.Configured;
                    }
                    catch (Exception e)
                    {
                        _lastConfigureException = ExceptionDispatchInfo.Capture(e);
                        _configurationState = ConfigurationState.None;
                        throw;
                    }
                }
            }
        }

        private void Configure()
        {
            Debug.Assert(Monitor.IsEntered(Options.Cache)); // Assert locked
            Debug.Assert(Options.IsReadOnly);
            Debug.Assert(IsInitialized);

            if (Kind == TypeDefinitionKind.Object)
            {
                ConfigureProperties();
            }

            // When ElementType member from Deserializer is not null
            if (ElementType is not null)
            {
                _elementTypeDefintion ??= Options.ThrowingGetTypeDefinition(ElementType);
                _elementTypeDefintion.SetConfiguration();
            }

            // When KeyType member from Deserializer is not null
            if (KeyType is not null)
            {
                _keyTypeDefinition ??= Options.ThrowingGetTypeDefinition(KeyType);
                _keyTypeDefinition.SetConfiguration();
            }

            // TODO: Assert targets Options
        }

        internal bool IsConfigured => _configurationState == ConfigurationState.Configured;

        internal bool IsConfiguring => _configurationState is not ConfigurationState.None;

        private enum ConfigurationState : byte
        {
            None = 0,
            Configuring = 1,
            Configured = 2
        }

        internal void ThrowingWhenIsInitialized()
        {
            if (IsInitialized)
            {
                Throwing.WhenTypeDefinitionIsInitialized();
            }
        }
        #endregion

        #region Creation
        /// <summary>
        /// Use deserializer to initialize the <see cref="TypeDefinition"/> otherwise failback on 
        /// reflection
        /// </summary>
        /// <param name="type"></param>
        /// <param name="deserializer"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        internal static TypeDefinition GetTypeDefinition(
            Type type,
            Deserializer deserializer,
            DeserializeOptions options
        )
        {
            TypeDefinition typeDefinition = deserializer.Type == type ?
                deserializer.InitializeTypeDefinition(options) :
                options.TypeDefinitionReflectiveInstantiator(type, deserializer, options);

            Debug.Assert(typeDefinition.Type == type);
            return typeDefinition;
        }

        /// <summary>
        /// Default <see cref="Object"/> type
        /// </summary>
        internal static readonly Type SystemObjectType = typeof(object);

        /// <summary>
        /// Default <see cref="ValueType"/> type
        /// </summary>
        internal static readonly Type SystemValueType = typeof(ValueType);
        #endregion
    }
}