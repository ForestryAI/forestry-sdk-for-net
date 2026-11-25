using System.Globalization;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.Policies;
using Forestry.Serialization.Reading;

namespace Forestry.Serialization.Xml.Tests.Fixtures
{
    /// <summary>
    /// An enumeration policy overriding every method of this slice: it records each call, then calls the
    /// built-in step, so telemetries stay as without it (tasks.md T049, research R19)
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

        public override Telemetry Construct(TypeDefinition type, DeserializeContext context, Func<DeserializeContext, Telemetry> next)
        {
            Recording.Record($"{_prefix}construct {type.Type.Name}");
            return next(context);
        }

        public override void Set<T>(ContextTelemetry telemetry, PropertyDefinition<T> property, T value, DeserializeContext context, Action<ContextTelemetry, T> next)
        {
            Recording.Record($"{_prefix}set {property.Name}={Convert.ToString(value, CultureInfo.InvariantCulture)}");
            next(telemetry, value);
        }

        public override void Complete(ContextTelemetry telemetry, DeserializeContext context, Action<ContextTelemetry> next)
        {
            Recording.Record($"{_prefix}enumerate {telemetry.Definition.Type.Name}");
            next(telemetry);
        }

        public override void Clear(DeserializeContext context) => Recording.RecordCleared(this, $"{_prefix}clear");
    }
}
