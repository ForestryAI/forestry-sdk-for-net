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

            SelfReferencingPropertyDefinition = CreateSelfReferencingPropertyDefinition();
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
                Debug.Assert(IsConfigurationImmutable);
                Debug.Assert(_elementTypeDefintion is null or { IsConfigurationMutable: true });

                TypeDefinition? value = _elementTypeDefintion;
                value?.AssertConfiguration();

                return value;
            }
            set
            {
                Debug.Assert(!IsReadOnly);
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
                Debug.Assert(IsConfigurationImmutable);
                Debug.Assert(_keyTypeDefinition is null or { IsConfigurationMutable: true });

                TypeDefinition? value = _keyTypeDefinition;
                value?.AssertConfiguration();

                return value;
            }
            set
            {
                Debug.Assert(!IsReadOnly);
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
                ThrowingWhenIsReadOnly();

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
                Debug.Assert(IsConfigurationImmutable is true && _propertyDefinitionsByName is not null);
                return _propertyDefinitionsByName;
            }
        }

        private Dictionary<string, PropertyDefinition>? _propertyDefinitionsByName;

        internal PropertyDefinition SelfReferencingPropertyDefinition { get; }

        private protected abstract PropertyDefinition CreateSelfReferencingPropertyDefinition();
        #endregion

        #region Configuration
        private volatile ConfigurationState _configurationState;

        private ExceptionDispatchInfo? _lastConfigureException;                          

        /// <summary>
        /// Flag true when <see cref="TypeDefinition"> is immutable and false when mutable
        /// </summary>
        public bool IsReadOnly { get; private set; }

        /// <summary>
        /// Set <see cref="TypeDefinition"> as immutable
        /// </summary>
        public void SetReadOnly() => IsReadOnly = true;

        /// <summary>
        /// Assert configuration <see cref="TypeDefinition"/> only when configuration state == None
        /// </summary>
        internal void AssertConfiguration()
        {
            if (!IsConfigurationImmutable)
            {
                SynchronizeConfigure();
            }

            void SynchronizeConfigure()
            {
                Options.SetReadOnly();
                SetReadOnly();

                // Before locking the type definition cache assert any configuration exception
                _lastConfigureException?.Throw();

                lock (Options.Cache)
                {
                    // When this thread has a redundant configuration mutation || another thread is mutating the configuration
                    if (_configurationState != ConfigurationState.None)
                    {
                        return;
                    }

                    // Before configuring assert any configuration exception
                    _lastConfigureException?.Throw();

                    try
                    {
                        _configurationState = ConfigurationState.Mutating;
                        Configure();
                        _configurationState = ConfigurationState.Immutable;
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
            Debug.Assert(Monitor.IsEntered(Options.Cache)); // Assert configuration locked
            Debug.Assert(Options.IsReadOnly);
            Debug.Assert(IsReadOnly);

            // TODO: Polymorphism

            if (Kind == TypeDefinitionKind.Object)
            {
                ConfigureProperties();
            }

            // When ElementType member from Deserializer is not null
            if (ElementType is not null)
            {
                _elementTypeDefintion ??= Options.ThrowingGetTypeDefinition(ElementType);
                _elementTypeDefintion.AssertConfiguration();
            }

            // When KeyType member from Deserializer is not null
            if (KeyType is not null)
            {
                _keyTypeDefinition ??= Options.ThrowingGetTypeDefinition(KeyType);
                _keyTypeDefinition.AssertConfiguration();
            }

            // TODO: Assert targets Options
        }

        internal bool IsConfigurationImmutable => _configurationState == ConfigurationState.Immutable;

        internal bool IsConfigurationMutable => _configurationState is not ConfigurationState.None;

        private enum ConfigurationState : byte
        {
            None = 0, // no mutation of the configuration is ongoing
            Mutating = 1,
            Immutable = 2
        }

        /// <summary>
        /// Throwing when definition is read only
        /// </summary>
        internal void ThrowingWhenIsReadOnly()
        {
            if (IsReadOnly)
            {
                Throwing.WhenTypeDefinitionIsReadOnly();
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