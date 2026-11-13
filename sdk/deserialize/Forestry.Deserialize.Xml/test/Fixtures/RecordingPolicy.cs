using System.Globalization;
using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Policies;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// An enumeration policy overriding every method of this slice: it records each call, then calls the
    /// built-in step, so rows stay as without it (tasks.md T049, research R19)
    /// </summary>
    /// <remarks>
    /// Records, e.g. for <c>Log</c>: <c>construct Log</c>, <c>set LogKey=1</c>, <c>enumerate Log</c> (at its
    /// completion, spec 002's <c>Complete</c>; the record keeps 001's word) and,
    /// when an enumeration ends, <c>clear</c>. A label prefixes every record, so policies in a chain can
    /// be told apart: <c>first construct Log</c>.
    /// </remarks>
    public sealed class RecordingPolicy : EnumerationPolicy
    {
        public RecordingPolicy() : this(string.Empty)
        {
        }

        public RecordingPolicy(string label)
        {
            _prefix = label.Length == 0 ? string.Empty : label + " ";
        }

        private readonly string _prefix;

        public override Row Construct(TypeDefinition type, DeserializationContext context, Func<DeserializationContext, Row> next)
        {
            Recording.Record($"{_prefix}construct {type.Type.Name}");
            return next(context);
        }

        public override void Set<T>(ContextRow row, PropertyDefinition<T> property, T value, DeserializationContext context, Action<ContextRow, T> next)
        {
            Recording.Record($"{_prefix}set {property.Name}={Convert.ToString(value, CultureInfo.InvariantCulture)}");
            next(row, value);
        }

        public override void Complete(ContextRow row, DeserializationContext context, Action<ContextRow> next)
        {
            Recording.Record($"{_prefix}enumerate {row.Definition.Type.Name}");
            next(row);
        }

        public override void Clear(DeserializationContext context) => Recording.RecordCleared(this, $"{_prefix}clear");
    }
}
