using System.Reflection;
using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Policies;

namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// Builds rows as a media reads (research R2, R6, R7, R19; spec 002 R20-R25): <see cref="Start{T}"/>
    /// constructs a row, <c>Set</c> converts and sets its members, <see cref="Complete"/> completes it, an
    /// entity that no longer changes once it comes out. The media drives it the same way for every row
    /// type. One instance per deserialization; a media's shared reading class calls it, its enumerators
    /// expose it
    /// </summary>
    /// <remarks>
    /// Every row is in the enumeration's <see cref="DeserializationContext"/> from its construction to
    /// its completion. Construction, setting and completion go through the enumeration policies composed
    /// when the options locked; pushing and popping are fixed. At completion, policies decide whether the
    /// row comes out and how long it stays; what they leave undecided gets the default (comes out when
    /// its type was added, leaves). Creating a builder starts an enumeration of the options, and
    /// <see cref="Clear"/> ends it: the policies are cleared once, then the context is emptied.
    /// </remarks>
    public sealed class RowBuilder
    {
        internal RowBuilder(DeserializeOptions options)
        {
            options.BeginEnumeration();
            _options = options;
            _context = new DeserializationContext(options.MaxContextRows);
        }

        private bool _isEnded;

        private readonly DeserializeOptions _options;

        private readonly DeserializationContext _context;

        /// <summary>
        /// Number of rows started and not yet completed
        /// </summary>
        public int OpenRowCount => _context.OpenCount;

        /// <summary>
        /// A row of type <typeparamref name="T"/> starts: construct it and push it on the open rows. A
        /// type not added to the options throws (S0)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void Start<T>() where T : Row
        {
            TypeChains chains = _options.ThrowingGetTypeChains(typeof(T));
            _context.Push(chains, chains.Construct(_context));
        }

        /// <summary>
        /// A property of the innermost open row has this UTF-8 value: convert and set it
        /// </summary>
        /// <param name="property"></param>
        /// <param name="utf8"></param>
        /// <param name="context"></param>
        public void Set(PropertyInfo property, ReadOnlySpan<byte> utf8, in FieldContext context) =>
            SetMember(property, utf8, in context);

        /// <summary>
        /// A field of the innermost open row has this UTF-8 value: convert and set it
        /// </summary>
        /// <param name="field"></param>
        /// <param name="utf8"></param>
        /// <param name="context"></param>
        public void Set(FieldInfo field, ReadOnlySpan<byte> utf8, in FieldContext context) =>
            SetMember(field, utf8, in context);

        /// <summary>
        /// The innermost open row is complete: pop it, let its policies decide, keep it in the context or
        /// let it go, and return it when it comes out (a copy when the context also keeps it)
        /// </summary>
        /// <returns>The row to enumerate, or null</returns>
        public Row? Complete()
        {
            if (!_context.TryBeginCompletion(out ContextRow? row))
            {
                Throwing.WhenNoOpenRow();
            }

            row.Chains.Complete(row);
            row.EndCompletion(_options.Enumerates(row.Definition.Type));
            _context.EndCompletion(row);

            if (!row.IsEnumerated)
            {
                return null;
            }

            return row.Lifetime == Lifetime.Leave ? row.Row : row.Row.ShallowCopy();
        }

        /// <summary>
        /// End the enumeration: every enumeration policy is cleared, once, while the context still holds
        /// its rows; then the context is emptied, and rows still open are never enumerated
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

        private void SetMember(MemberInfo member, ReadOnlySpan<byte> utf8, in FieldContext context)
        {
            if (!_context.TryPeek(out ContextRow? row))
            {
                Throwing.WhenNoOpenRow();
            }

            TypeDefinition definition = row.Definition;

            if (!definition.TryGetProperty(member, out PropertyDefinition? property))
            {
                Throwing.WhenMemberNotOfRow(member, definition.Type);
            }

            property.ReadAndSet(row, utf8, in context, row.Chains.Setters[property.Index]);
        }
    }
}
