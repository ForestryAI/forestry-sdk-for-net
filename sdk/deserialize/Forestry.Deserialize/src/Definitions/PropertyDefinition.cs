using System.Diagnostics;

namespace Forestry.Deserialize.Definitions
{
    public abstract class PropertyDefinition(
        Type type,
        Type declaringType,
        TypeDefinition? declaringTypeDefinition,
        DeserializeOptions options
    )
    {
        internal static readonly PropertyDefinition _Empty = CreateEmptyPropertyDefinition();

        internal static PropertyDefinition CreateEmptyPropertyDefinition()
        {
            PropertyDefinition value = new PropertyDefinition<object>(typeof(object), declaringTypeDefinition: null, options: null!)
            {
                Name = string.Empty
            };

            // TODO: Debug assertions

            return value;
        }

        #region Shape
        /// <summary>
        /// Property <see cref="Type"/>
        /// </summary>
        /// <value></value>
        public Type Type { get; } = type;

        /// <summary>
        /// Declaring <see cref="Type"/>
        /// </summary>
        /// <value></value>
        public Type DeclaringType { get; } = declaringType;

        /// <summary>
        /// Declaring <see cref="TypeDefinition"/>
        /// </summary>
        /// <value></value>
        public TypeDefinition? DeclaringTypeDefinition { get; private set; } = declaringTypeDefinition;

        /// <summary>
        /// Options
        /// </summary>
        /// <value></value>
        public virtual DeserializeOptions Options { get; } = options;

        /// <summary>
        /// Unescaped property name e.g. escaped newlines or tabs from REST APIs
        /// </summary>
        /// <value></value>
        public string Name
        {
            get
            {
                Debug.Assert(_name is not null);
                return _name;
            }
            set
            {
                ThrowingWhenIsReadOnly();
                ArgumentNullException.ThrowIfNull(value);

                _name = value;
            }
        }

        private string? _name;

        /// <summary>
        /// UTF-8 encoded property name
        /// </summary>
        internal byte[] Utf8Name { get; private set; } = null!;

        /// <summary>
        /// Property <see cref="TypeDefinition"/>
        /// </summary>
        /// <value></value>
        public TypeDefinition TypeDefinition
        {
            get
            {
                Debug.Assert(_typeDefinition?.IsConfigurationMutable == true);

                TypeDefinition value = _typeDefinition;
                value.AssertConfiguration();

                return value;
            }
            set
            {
                _typeDefinition = value;
            }
        }

        private TypeDefinition? _typeDefinition;

        /// <summary>
        /// Property index in declaring <see cref="TypeDefinition"/> sealed set when 
        /// this <see cref="PropertyDefinition"/> is configured
        /// </summary>
        internal int Index
        {
            get
            {
                Debug.Assert(IsConfigured);
                return _index;
            }
            set
            {
                Debug.Assert(!IsConfigured);
                _index = value;
            }
        }

        private int _index;

        /// <summary>
        /// Required property
        /// </summary>
        public bool IsRequired
        {
            get => _isRequired;
            set
            {
                ThrowingWhenIsReadOnly();
                _isRequired = value;
            }
        }

        private protected bool _isRequired;

        internal bool IsSelfReferencedTypeDefinition { get; init; }

        // TODO: Maybe property order metadata
        #endregion

        #region Configuration
        /// <summary>
        /// Asserts is configured
        /// </summary>
        /// <value></value>
        internal bool IsConfigured { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        internal void Configure()
        {
            Debug.Assert(DeclaringTypeDefinition is not null);
            Debug.Assert(!IsConfigured);

            // Synchronize configuration of declaring <see cref="TypeDefinition"/>
            // TODO: Ingore property conditions
            _typeDefinition ??= Options.GetTypeDefinition(Type);
            _typeDefinition.AssertConfiguration();
            
            Utf8Name = System.Text.Encoding.UTF8.GetBytes(Name);

            IsConfigured = true;
        }

        /// <summary>
        /// Throw when definition is read only
        /// </summary>
        private protected void ThrowingWhenIsReadOnly()
        {
            DeclaringTypeDefinition?.ThrowingWhenIsReadOnly();
        }

        /// <summary>
        /// Set declaring type definition only once else throw
        /// </summary>
        /// <param name="parent"></param>
        internal void SetDeclaringTypeDefinition(TypeDefinition parent)
        {
            if (DeclaringTypeDefinition is null)
            {
                DeclaringTypeDefinition = parent;
            }
            else if (DeclaringTypeDefinition != parent)
            {
                Throwing.WhenWrongDeclaringTypeDefintion(this);
            }
        }
        #endregion
    }
}