using Forestry.Deserialize.Deserializers;

namespace Forestry.Deserialize.Definitions
{
    public sealed partial class TypeDefinition<T>: TypeDefinition
    {
        internal TypeDefinition(Deserializer deserializer, DeserializeOptions options): base(typeof(T), deserializer, options)
        {
            // TODO: Maybe an effective deserializer
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="declaringTypeDefinition"></param>
        /// <param name="declaringType"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        private protected override PropertyDefinition CreatePropertyDefinition(TypeDefinition declaringTypeDefinition, Type? declaringType, DeserializeOptions options)
        {
            return new PropertyDefinition<T>(declaringType ?? declaringTypeDefinition.Type, declaringTypeDefinition, options)
            {
                TypeDefinition = this
            };
        }

        /// <summary>
        /// Create a self referencing <see cref="PropertyDefinition"/> to this <see cref="TypeDefinition{T}"/>
        /// </summary>
        /// <returns></returns>
        private protected override PropertyDefinition CreateSelfReferencingPropertyDefinition()
        {
            return new PropertyDefinition<T>(declaringType: typeof(T), declaringTypeDefinition: this, Options)
            {
                TypeDefinition = this,
                IsSelfReferencedTypeDefinition = true
            };
        }
    }
}