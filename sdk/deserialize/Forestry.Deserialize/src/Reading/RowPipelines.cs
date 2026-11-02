using System.Reflection;
using Forestry.Deserialize.Definitions;

namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// The core's two pipelines a media drives while reading (research R2, R6, R7): the type pipeline
    /// constructs and completes rows, the member pipeline converts and sets their members. One instance
    /// per deserialization; a media's shared reading class calls it, its enumerators expose it
    /// </summary>
    /// <remarks>
    /// Minimal slot order of this slice: <c>Pipeline&lt;T&gt;()</c> constructs and pushes;
    /// <c>Pipeline(member, utf8, context)</c> converts and sets; <c>Pipeline()</c> completes the top
    /// row, hands it out and pops it. Policies per slot come with US4.
    /// </remarks>
    public sealed class RowPipelines
    {
        internal RowPipelines(DeserializeOptions options)
        {
            _options = options;
        }

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
        public void Pipeline<T>() where T : Row
        {
            TypeDefinition typeDefinition = _options.ThrowingGetTypeDefinition(typeof(T));
            _state.Push(typeDefinition, typeDefinition.Construct());
        }

        /// <summary>
        /// A property of the top open row has this UTF-8 value: convert and set it
        /// </summary>
        /// <param name="property"></param>
        /// <param name="utf8"></param>
        /// <param name="context"></param>
        public void Pipeline(PropertyInfo property, ReadOnlySpan<byte> utf8, in FieldContext context) =>
            SetMember(property, utf8, in context);

        /// <summary>
        /// A field of the top open row has this UTF-8 value: convert and set it
        /// </summary>
        /// <param name="field"></param>
        /// <param name="utf8"></param>
        /// <param name="context"></param>
        public void Pipeline(FieldInfo field, ReadOnlySpan<byte> utf8, in FieldContext context) =>
            SetMember(field, utf8, in context);

        /// <summary>
        /// The top open row is complete: pop it and hand it out when the options enumerate its type
        /// </summary>
        /// <returns>The row to hand out, or null for a row only a constructor needs</returns>
        public Row? Pipeline()
        {
            if (!_state.TryPop(out DeserializationState.OpenRow openRow))
            {
                Throwing.WhenNoOpenRow();
            }

            return _options.Enumerates(openRow.TypeDefinition.Type) ? openRow.Row : null;
        }

        /// <summary>
        /// Drop the state; rows still open are never handed out
        /// </summary>
        internal void Clear() => _state.Clear();

        private void SetMember(MemberInfo member, ReadOnlySpan<byte> utf8, in FieldContext context)
        {
            if (!_state.TryPeek(out DeserializationState.OpenRow openRow))
            {
                Throwing.WhenNoOpenRow();
            }

            if (!openRow.TypeDefinition.TryGetProperty(member, out PropertyDefinition? property))
            {
                Throwing.WhenMemberNotOfRow(member, openRow.TypeDefinition.Type);
            }

            property.ReadAndSet(openRow.Row, utf8, in context);
        }
    }
}
