
namespace Forestry.Deserialize
{
    /// <summary>
    /// Deserialize options responsible for getting a <see cref="Deserializer"/> by <see cref="Type"/> and 
    /// deserialization policies
    /// </summary>
    public abstract partial class DeserializeOptions
    {
        #region TypeDefinition
        public Deserializer GetDeserializer(Type type)
        {
            TypeDefinition typeDefinition = GetTypeDefinition(type);
            return typeDefinition.Deserializer;
        }
        #endregion

        #region Configuration
        /// <summary>
        /// Read only the options are locked for changes
        /// </summary>
        public bool IsReadOnly => _isReadOnly;
        private volatile bool _isReadOnly;

        /// <summary>
        /// Idempotent set as read only
        /// </summary>
        public void SetReadOnly()
        {
            _isReadOnly = true;
        }

        /// <summary>
        /// Throws when read only
        /// </summary>
        internal void ThrowingAssertReadOnly()
        {
            if (_isReadOnly)
            {
                Throwing.WhenDeserializeOptionsIsReadOnly();
            }
        }

        /// <summary>
        /// Reflective type definition instantiator
        /// </summary>
        internal abstract Func<Type, Deserializer, DeserializeOptions, TypeDefinition> TypeDefinitionReflectiveInstantiator { get; }

        /// <summary>
        /// Reflective property definition instantiator
        /// </summary>
        internal abstract Func<Type, TypeDefinition, DeserializeOptions, PropertyDefinition> PropertyDefinitionReflectiveInstantiator { get; }

        /// <summary>
        /// Deserializer provider of simple and factory deserializers
        /// </summary>
        internal abstract IDeserializerProvider DeserializerProvider { get; }

        /// <summary>
        /// Reflection only type definition provider when sdk developing
        /// </summary>
        public TypeDefinitionProvider TypeDefinitionProvider => _typeDefinitionProvider;

        private readonly TypeDefinitionProvider _typeDefinitionProvider = new();
        #endregion

        #region Policies
        /// <summary>
        /// Naming policy enforced on collection element names (e.g. <see cref="TypeDefinition.ElementCollection"/>)
        /// </summary>
        public virtual NamingPolicy CollectionNamingPolicy { get; set; } = NamingPolicy.Default;

        /// <summary>
        /// Naming policy applied to reflected property/field names; null leaves member names as-is
        /// </summary>
        public virtual NamingPolicy? ElementNamingPolicy { get; set; }

        /// <summary>
        /// Policy asserting when a reflected property is excluded. Defaults to including everything
        /// </summary>
        public virtual IIgnorePropertyPolicy IgnorePropertyPolicy { get; set; } = NeverIgnorePropertyPolicy.Instance;

        /// <summary>
        /// Policy asserting when a reflected field is included. Defaults to excluding all fields
        /// </summary>
        public virtual IIncludeFieldPolicy IncludeFieldPolicy { get; set; } = NeverIncludeFieldPolicy.Instance;

        private sealed class NeverIgnorePropertyPolicy : IIgnorePropertyPolicy
        {
            public static readonly NeverIgnorePropertyPolicy Instance = new();

            public bool TryEnforce(System.Reflection.PropertyInfo property) => false;
        }

        private sealed class NeverIncludeFieldPolicy : IIncludeFieldPolicy
        {
            public static readonly NeverIncludeFieldPolicy Instance = new();

            public bool TryEnforce(System.Reflection.FieldInfo field) => false;
        }
        #endregion

        #region Customization
        /// <summary>
        /// User defined deserializers
        /// </summary>
        public IList<Deserializer> UserDefinedDeserializers
        {
            get => _userDefinedDeserializers ??= new(this);
        }

        private OptionsObservableList<Deserializer>? _userDefinedDeserializers;

        /// <summary>
        /// Get user defined deserializer
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal Deserializer? GetUserDefinedDeserializer(Type type)
        {
            if (_userDefinedDeserializers is { } values)
            {
                foreach (Deserializer value in values)
                {
                    if (value.CanDeserialize(type))
                    {
                        return value;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Observable options
        /// </summary>
        private sealed class OptionsObservableList<T>: ObservableList<T>
        {
            public OptionsObservableList(
                DeserializeOptions options,
                IList<T>? elements = null
            ) : base(elements) {
                _options = options;
            }

            private readonly DeserializeOptions _options;

            public override bool IsReadOnly => _options.IsReadOnly;

            protected override void Before()
            {
                _options.ThrowingAssertReadOnly();
            }
        }
        #endregion
    }
}