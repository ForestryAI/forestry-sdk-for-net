using Forestry.Deserialize.Definitions;

namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// One row in the deserialization context (spec 002, research R21-R23): the row, whether it is open,
    /// which of its members are set, and the completion actions policies took on it
    /// </summary>
    /// <remarks>
    /// <para>
    /// A row is in the context from its construction until its completion (open), and afterwards while a
    /// policy keeps it (<see cref="Keep"/>). At its completion, policies decide, each decision at most
    /// once: whether it comes out (<see cref="Enumerate"/>) and how long it stays (<see cref="Keep"/>);
    /// undecided, it comes out when its type was added to the options and leaves.
    /// </para>
    /// <para>
    /// Set members are tracked when the document sets a member (empty nodes included) or a policy sets
    /// one through <see cref="Set{TValue}(string, TValue)"/>; plain C# assignment to <see cref="Row"/> is
    /// not tracked. A row that came out no longer changes: setting one of its members throws.
    /// </para>
    /// </remarks>
    public sealed class ContextRow
    {
        internal ContextRow(DeserializationContext context, Policies.TypeChains chains, Row row, long sequence, long? parentId)
        {
            Container = new ContainerRelationship(sequence, parentId);
            Context = context;
            Chains = chains;
            Definition = chains.Definition;
            Row = row;
            Sequence = sequence;

            int properties = Definition.Properties.Count;

            if (properties > BitsPerWord)
            {
                _moreSetBits = new ulong[(properties - 1) / BitsPerWord];
            }
        }

        private const int BitsPerWord = 64;

        /// <summary>
        /// Set members 0-63, by property index
        /// </summary>
        private ulong _setBits;

        /// <summary>
        /// Set members from 64 on, only for types with more than 64 members
        /// </summary>
        private readonly ulong[]? _moreSetBits;

        private Actions _actions;

        private bool _enumerated;

        private Lifetime _lifetime;

        /// <summary>
        /// The row: the context's own instance
        /// </summary>
        public Row Row { get; }

        /// <summary>
        /// The row type's definition
        /// </summary>
        public TypeDefinition Definition { get; }

        /// <summary>
        /// Where the row sits in the document's containment: its id and its parent's (spec 002 US2). Other
        /// relationship groups add their own relationships later (research R21)
        /// </summary>
        public ContainerRelationship Container { get; }

        /// <summary>
        /// Open from its construction until its completion
        /// </summary>
        public bool IsOpen { get; internal set; } = true;

        /// <summary>
        /// The row type's definition with its composed policies
        /// </summary>
        internal Policies.TypeChains Chains { get; }

        /// <summary>
        /// The context the row is in
        /// </summary>
        internal DeserializationContext Context { get; }

        /// <summary>
        /// Construction order within the enumeration
        /// </summary>
        internal long Sequence { get; }

        /// <summary>
        /// Its completion is over: the decisions are final
        /// </summary>
        internal bool IsCompleted { get; private set; }

        /// <summary>
        /// Released by a policy, or the enumeration ended
        /// </summary>
        internal bool IsReleased => (_actions & Actions.Released) != 0;

        /// <summary>
        /// The decision, once completed: it came out
        /// </summary>
        internal bool IsEnumerated => _enumerated;

        /// <summary>
        /// The decision, once completed: how long it stays
        /// </summary>
        internal Lifetime Lifetime => _lifetime;

        /// <summary>
        /// Whether a member was set, by the document (an empty node included) or by a policy through
        /// <see cref="Set{TValue}(string, TValue)"/>; plain C# assignment to <see cref="Row"/> is not tracked
        /// </summary>
        /// <param name="memberName">The member's C# name, e.g. <c>nameof(Site.SiteKey)</c></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">The row type has no such member</exception>
        public bool IsSet(string memberName) => IsSet(ThrowingGetProperty(memberName));

        /// <summary>
        /// Whether a member was set
        /// </summary>
        /// <param name="property">One of the row type's property definitions</param>
        /// <returns></returns>
        public bool IsSet(PropertyDefinition property)
        {
            ArgumentNullException.ThrowIfNull(property);

            int index = property.Index;
            return index < BitsPerWord
                ? (_setBits & (1UL << index)) != 0
                : (_moreSetBits![(index / BitsPerWord) - 1] & (1UL << (index % BitsPerWord))) != 0;
        }

        /// <summary>
        /// Set a member through its setter and mark it set. A row that came out no longer changes:
        /// setting one of its members throws
        /// </summary>
        /// <typeparam name="TValue">The member's type</typeparam>
        /// <param name="memberName">The member's C# name, e.g. <c>nameof(Order.SiteKey)</c></param>
        /// <param name="value"></param>
        /// <exception cref="ArgumentException">The row type has no such member, or not of type
        /// <typeparamref name="TValue"/></exception>
        /// <exception cref="InvalidOperationException">The row came out already</exception>
        public void Set<TValue>(string memberName, TValue value)
        {
            PropertyDefinition property = ThrowingGetProperty(memberName);

            if (property is not PropertyDefinition<TValue> typed)
            {
                Throwing.WhenMemberValueOfOtherType(Definition.Type, property, typeof(TValue));
                return;
            }

            if (IsCompleted && _enumerated)
            {
                Throwing.WhenSetAfterEnumerated(Definition.Type, memberName);
            }

            typed.SetValue(Row, value);
            MarkSet(property.Index);
        }

        /// <summary>
        /// Decide whether the row comes out. Only on the row being completed, during its own completion,
        /// and at most once
        /// </summary>
        /// <param name="enumerated"></param>
        /// <exception cref="InvalidOperationException">Not this row's completion, or decided already</exception>
        public void Enumerate(bool enumerated)
        {
            TakeCompletionAction(Actions.Enumerated, nameof(Enumerate));
            _enumerated = enumerated;
        }

        /// <summary>
        /// Decide how long the row stays in the context after its completion. Only on the row being
        /// completed, during its own completion, and at most once
        /// </summary>
        /// <param name="lifetime"></param>
        /// <exception cref="InvalidOperationException">Not this row's completion, or decided already</exception>
        public void Keep(Lifetime lifetime)
        {
            TakeCompletionAction(Actions.Lifetime, nameof(Keep));
            _lifetime = lifetime;
        }

        /// <summary>
        /// Mark the member at <paramref name="index"/> set
        /// </summary>
        /// <param name="index"></param>
        internal void MarkSet(int index)
        {
            if (index < BitsPerWord)
            {
                _setBits |= 1UL << index;
            }
            else
            {
                _moreSetBits![(index / BitsPerWord) - 1] |= 1UL << (index % BitsPerWord);
            }
        }

        /// <summary>
        /// The completion chain ran: decisions no policy took get the containment default (spec 002 Q3)
        /// </summary>
        /// <param name="enumeratedByDefault">The options enumerate the row's type</param>
        internal void EndCompletion(bool enumeratedByDefault)
        {
            if ((_actions & Actions.Enumerated) == 0)
            {
                _enumerated = enumeratedByDefault;
            }

            IsCompleted = true;
        }

        /// <summary>
        /// A policy released the row: at most once, only a kept, closed row not kept until the end
        /// </summary>
        internal void Release()
        {
            if (IsOpen || !IsCompleted || _lifetime == Lifetime.Leave || (_actions & Actions.Released) != 0)
            {
                Throwing.WhenReleaseNotAllowed(Definition.Type, IsOpen, (_actions & Actions.Released) != 0);
            }

            if (_lifetime == Lifetime.UntilEnd)
            {
                Throwing.WhenReleaseUntilEnd(Definition.Type);
            }

            _actions |= Actions.Released;
        }

        private void TakeCompletionAction(Actions action, string name)
        {
            if (!ReferenceEquals(Context.Completing, this))
            {
                Throwing.WhenCompletionActionOutsideCompletion(Definition.Type, name);
            }

            if ((_actions & action) != 0)
            {
                Throwing.WhenCompletionActionTwice(Definition.Type, name);
            }

            _actions |= action;
        }

        private PropertyDefinition ThrowingGetProperty(string memberName)
        {
            ArgumentNullException.ThrowIfNull(memberName);

            if (!Definition.TryGetPropertyIndex(memberName, out int index))
            {
                Throwing.WhenUnknownMember(Definition.Type, memberName);
            }

            return Definition.Properties[index];
        }

        [Flags]
        private enum Actions : byte
        {
            None = 0,

            Enumerated = 1,

            Lifetime = 2,

            Released = 4
        }
    }
}
