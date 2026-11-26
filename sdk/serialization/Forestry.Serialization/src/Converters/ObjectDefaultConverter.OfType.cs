using Forestry.Serialization.Definitions;
using Forestry.Serialization.Writing;

namespace Forestry.Serialization.Converters
{
    /// <summary>
    /// The default object converter (spec 004 research R49; System.Text.Json's <c>ObjectDefaultConverter&lt;T&gt;</c>):
    /// the writer's start, each property definition writing its member (a missing value through the writer's
    /// <c>WriteNull</c>, otherwise its name and the property's resolved converter), the writer's end
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal sealed class ObjectDefaultConverter<T>(TypeDefinition<T> definition) : ObjectConverter<T> where T : Telemetry
    {
        public override void Write(T value, TelemetryWriter writer)
        {
            writer.WriteStartTelemetry(definition);

            IReadOnlyList<PropertyDefinition> properties = definition.Properties;

            for (int i = 0; i < properties.Count; i++)
            {
                properties[i].WriteMember(value, writer);
            }

            writer.WriteEndTelemetry();
        }
    }
}
