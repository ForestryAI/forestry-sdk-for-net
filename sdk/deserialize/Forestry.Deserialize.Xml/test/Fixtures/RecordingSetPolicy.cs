using System.Globalization;
using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Policies;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// An enumeration policy overriding only <c>Set</c>, so it takes part in no other method (tasks.md
    /// T049, T088, research R19): records <c>set LogKey=1</c>, then calls the built-in setter
    /// </summary>
    public sealed class RecordingSetPolicy : EnumerationPolicy
    {
        public override void Set<T>(ContextRow row, PropertyDefinition<T> property, T value, DeserializationContext context, Action<ContextRow, T> next)
        {
            Recording.Record($"set {property.Name}={Convert.ToString(value, CultureInfo.InvariantCulture)}");
            next(row, value);
        }
    }
}
