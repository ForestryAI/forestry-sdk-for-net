using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Reading;
using Forestry.Deserialize.Xml.Reading;

namespace Forestry.Deserialize.Xml
{
    public static partial class Deserialization
    {
        /// <summary>
        /// Positions the <see cref="PropertyDefinition"/> by name 
        /// on the <see cref="ReaderPosition"/>
        /// </summary>
        /// <param name="unescapedPropertyName"></param>
        /// <param name="readerPath"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        internal static PropertyDefinition PositionPropertyDefinition(
            ReadOnlySpan<byte> unescapedPropertyName,
            ref ReaderPath readerPath,
            DeserializeOptions options
        )
        {
            TypeDefinition typeDefinition = readerPath.Position.TypeDefinition;
            PropertyDefinition? propertyDefinition = typeDefinition.GetPropertyDefinition(unescapedPropertyName, out byte[] utf8Name);

            readerPath.Position.PropertyIndex++;
            readerPath.Position.PropertyUtf8Name = utf8Name;

            if (propertyDefinition is null)
            {
                // TODO: Potential dictionary extension support

                propertyDefinition = PropertyDefinition._Empty;
            }

            readerPath.Position.PropertyDefinition = propertyDefinition;
            return propertyDefinition;
        }

        internal static ReadOnlySpan<byte> GetPropertyName(
            ref Utf8XmlReader reader
        )
        {
            ReadOnlySpan<byte> value = reader.GetUnescapedValue();
            // TODO: bad-order properties

            return value;
        }
    }
}