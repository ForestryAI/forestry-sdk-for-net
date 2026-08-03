using System.Diagnostics;
using System.Text;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Definitions
{
    public abstract partial class TypeDefinition
    {
        #region Configuration
        private ObservablePropertyDefinitionList? _properties;

        /// <summary>
        /// Property definitions apply to only object shapes otherwise empty
        /// </summary>
        internal ObservablePropertyDefinitionList Properties
        {
            get
            {
                return _properties ?? CreateProperties();

                ObservablePropertyDefinitionList CreateProperties()
                {
                    ObservablePropertyDefinitionList values = new(this);

                    ObservablePropertyDefinitionList? others = Interlocked.CompareExchange(ref _properties, values, null);
                    return others ?? values;
                }
            }
        }

        /// <summary>
        /// Configure <see cref="Deserialize.PropertyDefinition"/> values when <see cref="TypeDefinitionKind.Object"/>
        /// </summary>
        private void ConfigureProperties()
        {
            Debug.Assert(Kind == TypeDefinitionKind.Object);
            Debug.Assert(_propertyDefinitionsByName is null);

            ObservablePropertyDefinitionList properties = Properties;
            // TODO: 
            Dictionary<string, PropertyDefinition> propertyDefinitionsByName = new(properties.Count, StringComparer.Ordinal);

            for (int index = 0; index < properties.Count; index++)
            {
                PropertyDefinition propertyDefinition = properties[index];
                Debug.Assert(propertyDefinition.DeclaringTypeDefinition == this);

                // TODO: Extensions
                
                propertyDefinition.Index = index;
                // TODO: Required
                // TODO: Sorted
                // TODO: Faster lookup cache by name

                if (!propertyDefinitionsByName.TryAdd(propertyDefinition.Name, propertyDefinition))
                {
                    Throwing.WhenDuplicatePropertyName(Type, propertyDefinition.Name);
                }

                propertyDefinition.Configure();
                // TODO: has || is element then keep otherwise remove

                _propertyDefinitionsByName = propertyDefinitionsByName;
            }
        }

        /// <summary>
        /// Initialize property definition using a type definition from the options cache 
        /// else from a reflective instantiator
        /// </summary>
        /// <param name="memberType"></param>
        /// <param name="memberDeclaringType"></param>
        /// <returns></returns>
        internal PropertyDefinition CreatePropertyDefinition(
            Type memberType,
            Type? memberDeclaringType
        ) {
            PropertyDefinition propertyDefinition;

            if (Options.TryGetTypeDefinition(memberType, out TypeDefinition? typeDefinition))
            {
                propertyDefinition = typeDefinition.CreatePropertyDefinition(declaringTypeDefinition: this, memberDeclaringType, Options);
            } else
            {
                // NOTE: memberDeclaringType (e.g. a base class for an inherited member) is not
                // threaded through here - PropertyDefinitionReflectiveInstantiator only carries
                // (memberType, declaringTypeDefinition). Fine for members declared directly on
                // this type; inherited members fall back to declaringTypeDefinition.Type instead.
                propertyDefinition = Options.PropertyDefinitionReflectiveInstantiator(memberType, this, Options);
            }

            return propertyDefinition;
        }

        /// <summary>
        /// Create property definition from declaring derived || base type
        /// </summary>
        /// <param name="declaringTypeDefinition"></param>
        /// <param name="declaringType"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        private protected abstract PropertyDefinition CreatePropertyDefinition(
            TypeDefinition declaringTypeDefinition,
            Type? declaringType,
            DeserializeOptions options
        );

        /// <summary>
        /// Observable property definition list asserting that the type definition is 
        /// not set initialized and has an object shape
        /// </summary>
        internal sealed class ObservablePropertyDefinitionList : ObservableList<PropertyDefinition>
        {
            public ObservablePropertyDefinitionList(TypeDefinition target)
            {
                _typeDefinition = target;
            }

            private readonly TypeDefinition _typeDefinition;

            public override bool IsReadOnly => _typeDefinition._properties == this && _typeDefinition.IsInitialized || _typeDefinition.Kind != TypeDefinitionKind.Object;

            /// <summary>
            /// Before an list operation assert the declaring type definition has not been 
            /// set initialized or unexpected shape (kind) 
            /// </summary>
            protected override void Before()
            {
                if (_typeDefinition._properties == this)
                {
                    _typeDefinition.ThrowingWhenIsInitialized();
                }

                if (_typeDefinition.Kind != TypeDefinitionKind.Object)
                {
                    Throwing.WhenConfigurePropertiesWrongDeclaringTypeDefintion(_typeDefinition.Kind);
                }
            }

            /// <summary>
            /// Before an item operation
            /// </summary>
            /// <param name="item"></param>
            protected override void Before(PropertyDefinition item) => item.SetDeclaringTypeDefinition(_typeDefinition);
        }
        #endregion

        #region Get
        internal PropertyDefinition? GetPropertyDefinition(
            ReadOnlySpan<byte> name,
            out byte[] utf8Name
        )
        {
            Debug.Assert(IsConfigured);

            if (PropertyDefinitionsByName.TryGetValue(Encoding.UTF8.GetString(name), out PropertyDefinition? value) && name.SequenceEqual(value.Utf8Name))
            {
                // Exact match
                utf8Name = value.Utf8Name;

            } else
            {
                // Copy name
                utf8Name = name.ToArray();
            }
            
            return value;
        }
        #endregion
    }
}