using System.Reflection;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.Policies;

namespace Forestry.Serialization.Reading
{
    /// <summary>
    /// Builds telemetries as a format reads (research R2, R6, R7, R19; spec 002 R20-R25): <see cref="Start{T}"/>
    /// constructs a telemetry, <c>Set</c> converts and sets its members, <see cref="Complete"/> completes it, an
    /// entity that no longer changes once it comes out. The format drives it the same way for every telemetry
    /// type. One instance per deserialization; a format's shared reading class calls it, its enumerators
    /// expose it
    /// </summary>
    /// <remarks>
    /// Every telemetry is in the enumeration's <see cref="DeserializeContext"/> from its construction to
    /// its completion. Construction, setting and completion go through the enumeration policies composed
    /// when the options locked; pushing and popping are fixed. At completion, policies decide whether the
    /// telemetry comes out and how long it stays; what they leave undecided gets the default (comes out when
    /// its type was added, leaves). Creating a builder starts an enumeration of the options, and
    /// <see cref="Clear"/> ends it: the policies are cleared once, then the context is emptied.
    /// </remarks>
    public sealed class TelemetryBuilder
    {
        internal TelemetryBuilder(DeserializeOptions options)
        {
            options.BeginEnumeration();
            _options = options;
            _context = new DeserializeContext(options.MaxContextTelemetries);
        }

        private bool _isEnded;

        private readonly DeserializeOptions _options;

        private readonly DeserializeContext _context;

        /// <summary>
        /// Number of telemetries started and not yet completed
        /// </summary>
        public int OpenTelemetryCount => _context.OpenCount;

        /// <summary>
        /// A telemetry of type <typeparamref name="T"/> starts: construct it and push it on the open telemetries. A
        /// type not added to the options throws (S0)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void Start<T>() where T : Telemetry
        {
            TypeChains chains = _options.ThrowingGetTypeChains(typeof(T));
            _context.Push(chains, chains.Construct(_context));
        }

        /// <summary>
        /// A property of the innermost open telemetry has this UTF-8 value: convert and set it
        /// </summary>
        /// <param name="property"></param>
        /// <param name="utf8"></param>
        /// <param name="information"></param>
        public void Set(PropertyInfo property, ReadOnlySpan<byte> utf8, in ReadingInformation information) =>
            SetMember(property, utf8, in information);

        /// <summary>
        /// A field of the innermost open telemetry has this UTF-8 value: convert and set it
        /// </summary>
        /// <param name="field"></param>
        /// <param name="utf8"></param>
        /// <param name="information"></param>
        public void Set(FieldInfo field, ReadOnlySpan<byte> utf8, in ReadingInformation information) =>
            SetMember(field, utf8, in information);

        /// <summary>
        /// The innermost open telemetry is complete: pop it, let its policies decide, keep it in the context or
        /// let it go, and return it when it comes out (a copy when the context also keeps it)
        /// </summary>
        /// <returns>The telemetry to enumerate, or null</returns>
        public Telemetry? Complete()
        {
            if (!_context.TryBeginCompletion(out ContextTelemetry? telemetry))
            {
                Throwing.WhenNoOpenTelemetry();
            }

            telemetry.Chains.Complete(telemetry);
            telemetry.EndCompletion(_options.Enumerates(telemetry.Definition.Type));
            _context.EndCompletion(telemetry);

            if (!telemetry.IsEnumerated)
            {
                return null;
            }

            return telemetry.Lifetime == Lifetime.Leave ? telemetry.Telemetry : telemetry.Telemetry.ShallowCopy();
        }

        /// <summary>
        /// End the enumeration: every enumeration policy is cleared, once, while the context still holds
        /// its telemetries; then the context is emptied, and telemetries still open are never enumerated
        /// </summary>
        internal void Clear()
        {
            if (_isEnded)
            {
                return;
            }

            _isEnded = true;

            try
            {
                _options.EndEnumeration(_context);
            }
            finally
            {
                _context.Clear();
            }
        }

        private void SetMember(MemberInfo member, ReadOnlySpan<byte> utf8, in ReadingInformation information)
        {
            if (!_context.TryPeek(out ContextTelemetry? telemetry))
            {
                Throwing.WhenNoOpenTelemetry();
            }

            TypeDefinition definition = telemetry.Definition;

            if (!definition.TryGetProperty(member, out PropertyDefinition? property))
            {
                Throwing.WhenMemberNotOfTelemetry(member, definition.Type);
            }

            property.ReadAndSet(telemetry, utf8, in information, telemetry.Chains.Setters[property.Index]);
        }
    }
}
