using System.Collections.Concurrent;
using System.Reflection;
using Forestry.Deserialize.Definitions;

namespace Forestry.Deserialize.Policies
{
    /// <summary>
    /// A policy acting while rows are built and enumerated (research R7, R19): when a row is constructed,
    /// when each member is set, when a completed row may be enumerated, and when the enumeration ends.
    /// Override only what the policy needs; it takes part in exactly the methods it overrides
    /// </summary>
    /// <remarks>
    /// <para>
    /// An instance lives as long as the options it is attached to, by attribute (created once when the
    /// options lock) or through <c>options.Policies</c>. It may keep state across the rows of one
    /// enumeration and drop it in <see cref="Clear"/>, called once when each enumeration ends: at its
    /// end, on an early stop and after a throw.
    /// </para>
    /// <para>
    /// Because of that state, options with enumeration policies serve one enumeration at a time: one
    /// starting while another is still running on the same options throws. Use one options instance per
    /// concurrent deserialization.
    /// </para>
    /// <para>
    /// The fixed mechanics stay outside every policy: the row <see cref="Construct"/> returns is always
    /// pushed, and the completed row is always popped, whatever <see cref="Enumerate"/> returns.
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
        /// <param name="next">The next step: the next policy, or the built-in construction</param>
        /// <returns>The row to push on the open-row stack</returns>
        public virtual Row Construct(TypeDefinition type, Func<Row> next) => next();

        /// <summary>
        /// A member of the top open row has this converted value: set it. The built-in step uses the
        /// member's setter
        /// </summary>
        /// <typeparam name="T">The member's type</typeparam>
        /// <param name="row">The row being built</param>
        /// <param name="property">The member's definition</param>
        /// <param name="value">The converted value</param>
        /// <param name="next">The next step: the next policy, or the built-in setter</param>
        public virtual void Set<T>(Row row, PropertyDefinition<T> property, T value, Action<Row, T> next) => next(row, value);

        /// <summary>
        /// A row of <paramref name="type"/> is complete: the row to enumerate, or null to enumerate none.
        /// The built-in step returns the row when its type was added to the options, else null
        /// </summary>
        /// <param name="row">The completed row</param>
        /// <param name="type">The row type's definition</param>
        /// <param name="next">The next step: the next policy, or the built-in decision</param>
        /// <returns>The row to enumerate, or null</returns>
        public virtual Row? Enumerate(Row row, TypeDefinition type, Func<Row, Row?> next) => next(row);

        /// <summary>
        /// The enumeration ended (at its end, on an early stop or after a throw): drop any state kept
        /// for it. Called once per enumeration
        /// </summary>
        public virtual void Clear()
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
