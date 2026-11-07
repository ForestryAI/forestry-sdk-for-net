using System.Reflection;
using Forestry.Deserialize.Definitions;
using Forestry.Deserialize.Policies;

namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// Builds rows as a media reads (research R2, R6, R7, R19): <see cref="Start{T}"/> constructs a row,
    /// <c>Set</c> converts and sets its members, <see cref="Complete"/> completes it, an entity that no
    /// longer changes. The media drives it the same way for every row type. One instance
    /// per deserialization; a media's shared reading class calls it, its enumerators expose it
    /// </summary>
    /// <remarks>
    /// <c>Start&lt;T&gt;()</c> constructs and pushes; <c>Set(member, utf8, context)</c> converts and
    /// sets; <c>Complete()</c> pops the top row and decides whether it is enumerated. Construction,
    /// setting and enumerating go through the enumeration policies composed when the options locked
    /// (research R19); pushing and popping are fixed. Creating a builder starts an enumeration of the
    /// options, and <see cref="Clear"/> ends it, clearing the policies once.
    /// </remarks>
    public sealed class RowBuilder
    {
        internal RowBuilder(DeserializeOptions options)
        {
            options.BeginEnumeration();
            _options = options;
        }

        private bool _isEnded;

        private readonly DeserializeOptions _options;

        private readonly DeserializationState _state = new();

        /// <summary>
        /// Number of rows started and not yet completed
        /// </summary>
        public int OpenRowCount => _state.OpenRowCount;

        /// <summary>
        /// A row of type <typeparamref name="T"/> starts: construct it and push it on the open-row
        /// stack. A type not added to the options throws (S0)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void Start<T>() where T : Row
        {
            TypeChains chains = _options.ThrowingGetTypeChains(typeof(T));
            _state.Push(chains, chains.Construct());
        }

        /// <summary>
        /// A property of the top open row has this UTF-8 value: convert and set it
        /// </summary>
        /// <param name="property"></param>
        /// <param name="utf8"></param>
        /// <param name="context"></param>
        public void Set(PropertyInfo property, ReadOnlySpan<byte> utf8, in FieldContext context) =>
            SetMember(property, utf8, in context);

        /// <summary>
        /// A field of the top open row has this UTF-8 value: convert and set it
        /// </summary>
        /// <param name="field"></param>
        /// <param name="utf8"></param>
        /// <param name="context"></param>
        public void Set(FieldInfo field, ReadOnlySpan<byte> utf8, in FieldContext context) =>
            SetMember(field, utf8, in context);

        /// <summary>
        /// The top open row is complete: pop it, and enumerate it when the options enumerate its type
        /// (or as its enumeration policies decide)
        /// </summary>
        /// <returns>The row to enumerate, or null for a row only a constructor needs</returns>
        public Row? Complete()
        {
            if (!_state.TryPop(out DeserializationState.OpenRow openRow))
            {
                Throwing.WhenNoOpenRow();
            }

            return openRow.Chains.Enumerate(openRow.Row);
        }

        /// <summary>
        /// Drop the state, rows still open are never enumerated, and end the enumeration: every
        /// enumeration policy is cleared, once
        /// </summary>
        internal void Clear()
        {
            _state.Clear();

            if (!_isEnded)
            {
                _isEnded = true;
                _options.EndEnumeration();
            }
        }

        private void SetMember(MemberInfo member, ReadOnlySpan<byte> utf8, in FieldContext context)
        {
            if (!_state.TryPeek(out DeserializationState.OpenRow openRow))
            {
                Throwing.WhenNoOpenRow();
            }

            TypeDefinition definition = openRow.Chains.Definition;

            if (!definition.TryGetProperty(member, out PropertyDefinition? property))
            {
                Throwing.WhenMemberNotOfRow(member, definition.Type);
            }

            property.ReadAndSet(openRow.Row, utf8, in context, openRow.Chains.Setters[property.Index]);
        }
    }
}
