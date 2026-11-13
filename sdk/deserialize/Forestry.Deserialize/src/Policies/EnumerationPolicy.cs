using System.Collections.Concurrent;
using System.Reflection;
using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Reading;

namespace Forestry.Deserialize.Policies
{
    /// <summary>
    /// A policy acting while rows are built and enumerated (research R7, R19; spec 002 R23, R26): when a
    /// row is constructed, when each member is set, when a row completes, and when the enumeration ends.
    /// Override only what the policy needs; it takes part in exactly the methods it overrides
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every method gets the enumeration's <see cref="DeserializationContext"/>: the rows open around the
    /// current one and the rows policies keep. Keep per-enumeration state there, not in the policy: an
    /// instance lives as long as the options it is attached to, by attribute (created once when the
    /// options lock) or through <c>options.Policies</c>, and <see cref="Clear"/> is called once when
    /// each enumeration ends (at its end, on an early stop and after a throw), before the context is
    /// emptied.
    /// </para>
    /// <para>
    /// Options with enumeration policies serve one enumeration at a time: one starting while another is
    /// still running on the same options throws. Use one options instance per concurrent deserialization.
    /// </para>
    /// <para>
    /// At a row's completion, policies decide whether it comes out (<see cref="ContextRow.Enumerate"/>)
    /// and how long it stays in the context (<see cref="ContextRow.Keep"/>), each at most once; what no
    /// policy decides gets the default: it comes out when its type was added to the options, and leaves.
    /// The fixed mechanics stay outside every policy: the row <see cref="Construct"/> returns is always
    /// pushed, and the completed row is always popped. Each <c>next</c> takes what the step needs, so
    /// chains are composed once and allocate nothing per row.
    /// </para>
    /// </remarks>
    public abstract class EnumerationPolicy : Policy
    {
        private static readonly ConcurrentDictionary<(Type Policy, string Method), bool> s_overrides = new();

        /// <summary>
        /// Enumeration policy
        /// </summary>
        protected EnumerationPolicy()
        {
        }

        /// <summary>
        /// A row of <paramref name="type"/> starts: construct it. The built-in step uses the chosen
        /// constructor
        /// </summary>
        /// <param name="type">The row type's definition</param>
        /// <param name="context">The enumeration's deserialization context</param>
        /// <param name="next">The next step: the next policy, or the built-in construction</param>
        /// <returns>The row to push on the open rows</returns>
        public virtual Row Construct(TypeDefinition type, DeserializationContext context, Func<DeserializationContext, Row> next) => next(context);

        /// <summary>
        /// A member of the row being built has this converted value: set it. The built-in step uses the
        /// member's setter and marks the member set
        /// </summary>
        /// <typeparam name="T">The member's type</typeparam>
        /// <param name="row">The row being built</param>
        /// <param name="property">The member's definition</param>
        /// <param name="value">The converted value</param>
        /// <param name="context">The enumeration's deserialization context</param>
        /// <param name="next">The next step: the next policy, or the built-in setter</param>
        public virtual void Set<T>(ContextRow row, PropertyDefinition<T> property, T value, DeserializationContext context, Action<ContextRow, T> next) => next(row, value);

        /// <summary>
        /// A row is complete: decide whether it comes out and how long it stays, read or update the
        /// context. The built-in step does nothing; decisions no policy takes get the defaults
        /// </summary>
        /// <param name="row">The row being completed, closed</param>
        /// <param name="context">The enumeration's deserialization context</param>
        /// <param name="next">The next step: the next policy, or the built-in step</param>
        public virtual void Complete(ContextRow row, DeserializationContext context, Action<ContextRow> next) => next(row);

        /// <summary>
        /// The enumeration ended (at its end, on an early stop or after a throw): drop any state kept
        /// for it. Called once per enumeration, before the context is emptied
        /// </summary>
        /// <param name="context">The enumeration's deserialization context, still holding its rows</param>
        public virtual void Clear(DeserializationContext context)
        {
        }

        /// <summary>
        /// Whether a policy type overrides one of these methods, checked once per type
        /// </summary>
        /// <param name="policyType"></param>
        /// <param name="method"><c>nameof</c> one of the virtual methods</param>
        /// <returns></returns>
        internal static bool Overrides(Type policyType, string method) =>
            s_overrides.GetOrAdd((policyType, method), static key =>
                key.Policy.GetMethod(key.Method, BindingFlags.Instance | BindingFlags.Public)!.DeclaringType != typeof(EnumerationPolicy));
    }
}
