using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Policies;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Xml.Tests.Fixtures
{
    /// <summary>
    /// An enumeration policy whose behavior each test writes in place, so a test method shows all it
    /// relies on (spec 002, tasks.md T001, research R23, R26)
    /// </summary>
    /// <remarks>
    /// Each hook runs before the built-in step (<c>next</c>), which always runs, so rows are built as
    /// without the policy unless a hook takes an action (<c>Enumerate</c>, <c>Keep</c>, <c>Release</c>)
    /// or sets a member. A hook left null does nothing.
    /// <para>
    /// Test-only: hooks capture the test's local variables to record what they saw, which is what a
    /// test needs and what live policies must not do. Captured state lives outside the deserialization
    /// context, so it outlives the enumeration, is shared by every enumeration of the options, and is
    /// not cleared by <c>Clear</c>. Live policies keep per-enumeration state in the context (#103).
    /// </para>
    /// </remarks>
    public sealed class ContextPolicy : EnumerationPolicy
    {
        /// <summary>
        /// At a member's set, before it is set: the row being built, the member, the converted value
        /// </summary>
        public Action<ContextRow, PropertyDefinition, object?, DeserializationContext>? OnSet { get; init; }

        /// <summary>
        /// At the row's completion: take actions on it, read or update the context
        /// </summary>
        public Action<ContextRow, DeserializationContext>? OnComplete { get; init; }

        /// <summary>
        /// When the enumeration ends, before the context is emptied
        /// </summary>
        public Action<DeserializationContext>? OnClear { get; init; }

        public override void Set<T>(ContextRow row, PropertyDefinition<T> property, T value, DeserializationContext context, Action<ContextRow, T> next)
        {
            OnSet?.Invoke(row, property, value, context);
            next(row, value);
        }

        public override void Complete(ContextRow row, DeserializationContext context, Action<ContextRow> next)
        {
            OnComplete?.Invoke(row, context);
            next(row);
        }

        public override void Clear(DeserializationContext context) => OnClear?.Invoke(context);
    }
}
