using System.Diagnostics;

namespace Forestry.Xml.Reading
{
    /// <summary>
    /// Markup evaluation: a markup is evaluated to its end, segment by segment (spec 001 research R8, markup delimits)
    /// </summary>
    public ref partial struct Utf8XmlReader
    {
        /// <summary>
        /// One malformed markup makes the whole document malformed
        /// </summary>
        internal enum MarkupState : byte
        {
            /// <summary>
            /// Neither well-formed nor malformed, the context decides usage
            /// </summary>
            Unknown,
            WellFormed,
            Malformed,
        }

        /// <summary>
        /// Why a markup evaluation is malformed, so a delegate picks the category without
        /// another scan
        /// </summary>
        internal enum MarkupReason : byte
        {
            /// <summary>
            /// No reason beyond the markup state
            /// </summary>
            None,

            /// <summary>
            /// A byte the reader does not support, e.g. a non-ASCII name byte (rule 14)
            /// </summary>
            UnsupportedCharacter,

            /// <summary>
            /// A character or entity reference breaking its production (FR-005)
            /// </summary>
            MalformedReference,

            /// <summary>
            /// An entity other than the five predefined ones without a document type (WFC:
            /// Entity Declared)
            /// </summary>
            UndeclaredEntity,

            /// <summary>
            /// An entity other than the five predefined ones after a document type, which may
            /// declare it: not supported (S2)
            /// </summary>
            NotSupportedEntity,

            /// <summary>
            /// A name longer than its bound, at the first byte beyond it (S1, spec 001 US2)
            /// </summary>
            TooLong,
        }

        /// <summary>
        /// Evaluates a name non-terminal markup state
        /// </summary>
        /// <param name="characters"></param>
        /// <param name="isFirstCharacter"></param>
        /// <param name="nameLength"></param>
        /// <param name="reason"></param>
        /// <param name="budget">name bytes still allowed: a name byte at this index is Malformed
        /// with <see cref="MarkupReason.TooLong"/>, nothing beyond it is looked at (S1)</param>
        /// <returns></returns>
        internal static MarkupState EvaluateNameNonTerminalMarkup(
            ReadOnlySpan<byte> characters,
            bool isFirstCharacter,
            out int nameLength,
            out MarkupReason reason,
            int budget = int.MaxValue
        ) {
            static bool IsUnsupportedCharacterGuard(byte value) => value >= 0x80;

            reason = MarkupReason.None;

            for (int characterIndex = 0; characterIndex < characters.Length; characterIndex++)
            {
                if (IsUnsupportedCharacterGuard(characters[characterIndex]))
                {
                    nameLength = characterIndex;
                    reason = MarkupReason.UnsupportedCharacter;
                    return MarkupState.Malformed;  // S0
                }

                if (isFirstCharacter && characterIndex == 0 && !EBNF.IsNameStartingCharacter(characters[characterIndex]))
                {
                    nameLength = 0;
                    return MarkupState.Malformed;  // S1
                }

                if (!EBNF.IsNameCharacter(characters[characterIndex]))
                {
                    nameLength = characterIndex;
                    return MarkupState.WellFormed;  // S2 (fall through from S1)
                }

                if (characterIndex >= budget)
                {
                    nameLength = characterIndex;
                    reason = MarkupReason.TooLong;
                    return MarkupState.Malformed;  // the first name byte beyond the bound (spec 001 US2)
                }
            }

            nameLength = characters.Length;
            return MarkupState.Unknown;  // S3
        }

        internal interface IMarkup
        {
            int StartingTerminalLength { get; }

            MarkupState Evaluate(ReadOnlySpan<byte> characters, out int length, out MarkupReason reason);
        }

        internal struct NameMarkup: IMarkup
        {
            private bool _hasNameCharacter;

            private int _evaluated;

            /// <summary>
            /// Bound on the name in bytes, 0 for none (spec 001 US2: <c>MaxNameLength</c>, or the
            /// start-tag name's length for an end tag)
            /// </summary>
            public int MaxLength { get; init; }

            public int StartingTerminalLength
            {
                get;
                init {
                    field = value > 0 ? value : throw new InvalidOperationException();
                }
            }

            public MarkupState Evaluate(ReadOnlySpan<byte> characters, out int length, out MarkupReason reason)
            {
                int budget = MaxLength > 0 ? MaxLength - _evaluated : int.MaxValue;
                MarkupState markupState = EvaluateNameNonTerminalMarkup(characters, isFirstCharacter: !_hasNameCharacter, out length, out reason, budget);
                _hasNameCharacter |= length > 0;
                _evaluated += length;
                return markupState;
            }
        }

        /// <summary>
        /// Evaluates markup state for literals where the following are
        /// malformed characters:
        ///  - control bytes except tabs, line feeds and carriage returns
        ///  - less than
        ///  - a malformed reference (<see cref="ReferenceConstraint"/>)
        ///
        /// The only well-formed markup is by reaching the same quote byte character outside a
        /// reference; a reference is part of the literal, raw (spec 001 US1)
        /// </summary>
        /// <remarks>
        /// Logic could be faster with static search values if the character byte
        /// array is larger than 16–32 bytes
        /// </remarks>
        internal struct LiteralMarkup: IMarkup
        {
            public byte Quote;

            /// <summary>
            /// The reader read a document type, so an entity other than the five may be declared
            /// </summary>
            public bool IsDocumentType { get; init; }

            /// <summary>
            /// Bound on an entity name after a document type (spec 001 US2)
            /// </summary>
            public int MaxNameLength { get; init; }

            private ReferenceConstraint _reference;

            private bool _isInReference;

            private int _evaluated;

            /// <summary>
            /// At least one complete reference in the literal
            /// </summary>
            public bool HasReferences { get; private set; }

            /// <summary>
            /// Offset after the starting quote of the current reference's <c>&amp;</c>
            /// </summary>
            public int ReferenceStart { get; private set; }

            public readonly int StartingTerminalLength => 1;

            public MarkupState Evaluate(ReadOnlySpan<byte> characters, out int length, out MarkupReason reason)
            {
                reason = MarkupReason.None;
                length = 0;

                while (length < characters.Length)
                {
                    if (!_isInReference)
                    {
                        byte character = characters[length];

                        if (character == Quote)
                        {
                            return MarkupState.WellFormed;  // ending quote
                        }

                        switch (character)
                        {
                            case EBNF.LessThan:
                                return MarkupState.Malformed;  // S6
                            case EBNF.Ampersand:
                                _reference = new ReferenceConstraint { IsDocumentType = IsDocumentType, MaxNameLength = MaxNameLength };
                                _isInReference = true;
                                ReferenceStart = _evaluated + length;
                                break;  // S7, a reference: evaluated from its '&' below
                            case EBNF.Tab or EBNF.LineFeed or EBNF.CarriageReturn:
                                length++;
                                continue;
                            case < 0x20:
                                return MarkupState.Malformed;  // S8, a control byte except tabs, line feeds or carriage returns
                            default:
                                length++;
                                continue;
                        }
                    }

                    MarkupState state = _reference.Evaluate(characters[length..], out int referenceLength, out reason);
                    length += referenceLength;

                    if (state == MarkupState.Malformed)
                    {
                        return MarkupState.Malformed;  // the reference's first known byte, or its '&' when not supported
                    }

                    if (state == MarkupState.WellFormed)
                    {
                        _isInReference = false;
                        HasReferences = true;
                    }
                }

                _evaluated += length;
                return MarkupState.Unknown;  // no ending quote in this span
            }
        }

        /// <summary>
        /// Evaluates markup state for character data where the following are
        /// malformed characters:
        ///  - control bytes except tabs, line feeds and carriage returns
        ///  - a malformed reference (<see cref="ReferenceConstraint"/>)
        ///
        /// The less than (markup) outside a reference is well-formed, ending the character data.
        /// A reference is part of the text, raw (spec 001 US1). Every other byte is plain text.
        /// </summary>
        internal struct CharacterDataMarkup: IMarkup
        {
            /// <summary>
            /// The reader read a document type, so an entity other than the five may be declared
            /// </summary>
            public bool IsDocumentType { get; init; }

            /// <summary>
            /// Bound on an entity name after a document type (spec 001 US2)
            /// </summary>
            public int MaxNameLength { get; init; }

            private ReferenceConstraint _reference;

            private bool _isInReference;

            private int _evaluated;

            /// <summary>
            /// At least one complete reference in the character data
            /// </summary>
            public bool HasReferences { get; private set; }

            /// <summary>
            /// Offset of the current reference's <c>&amp;</c>
            /// </summary>
            public int ReferenceStart { get; private set; }

            /// <summary>
            /// Character data has no starting terminal, a quote in the scratch pad is content
            /// </summary>
            public readonly int StartingTerminalLength => 0;

            public MarkupState Evaluate(ReadOnlySpan<byte> characters, out int length, out MarkupReason reason)
            {
                reason = MarkupReason.None;
                length = 0;

                while (length < characters.Length)
                {
                    if (!_isInReference)
                    {
                        switch (characters[length])
                        {
                            case EBNF.LessThan:
                                return MarkupState.WellFormed;  // markup
                            case EBNF.Ampersand:
                                _reference = new ReferenceConstraint { IsDocumentType = IsDocumentType, MaxNameLength = MaxNameLength };
                                _isInReference = true;
                                ReferenceStart = _evaluated + length;
                                break;  // a reference: evaluated from its '&' below
                            case EBNF.Tab or EBNF.LineFeed or EBNF.CarriageReturn:
                                length++;
                                continue;
                            case < 0x20:
                                return MarkupState.Malformed;  // a control byte except tabs, line feeds or carriage returns
                            default:
                                length++;
                                continue;
                        }
                    }

                    MarkupState state = _reference.Evaluate(characters[length..], out int referenceLength, out reason);
                    length += referenceLength;

                    if (state == MarkupState.Malformed)
                    {
                        return MarkupState.Malformed;  // the reference's first known byte, or its '&' when not supported
                    }

                    if (state == MarkupState.WellFormed)
                    {
                        _isInReference = false;
                        HasReferences = true;
                    }
                }

                _evaluated += length;
                return MarkupState.Unknown;  // no markup in this span
            }
        }
    }
}
