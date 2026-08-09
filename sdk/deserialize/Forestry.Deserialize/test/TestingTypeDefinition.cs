using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Deserializers;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Concrete, non-generic TypeDefinition for testing. Sidesteps TypeDefinition&lt;T&gt;,
    /// which needs a compile-time T that a purely reflection-driven caller doesn't have.
    /// </summary>
    internal sealed class TestingTypeDefinition : TypeDefinition
    {
        internal TestingTypeDefinition(Type type, Deserializer deserializer, DeserializeOptions options)
            : base(type, deserializer, options)
        {
        }

        private protected override PropertyDefinition CreatePropertyDefinition(
            TypeDefinition declaringTypeDefinition,
            Type? declaringType,
            DeserializeOptions options
        ) => new TestingPropertyDefinition(Type, declaringType ?? declaringTypeDefinition.Type, declaringTypeDefinition, options)
        {
            TypeDefinition = this
        };

        private protected override PropertyDefinition CreateSelfReferencingPropertyDefinition()
        {
            return new TestingPropertyDefinition(this.Type, this.Type, this, Options)
            {
                TypeDefinition = this,
                IsSelfReferencedTypeDefinition = true
            };
        }
    }
}
