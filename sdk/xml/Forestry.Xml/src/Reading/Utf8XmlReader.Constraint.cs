using System.Diagnostics;

namespace Forestry.Xml.Reading
{
    /// <summary>
    /// Constraints: a constraint is evaluated over a delimited value, or one reference, before anything is committed (spec 001 research R8, constraint checks)
    /// </summary>
    public ref partial struct Utf8XmlReader
    {
        /// <summary>
        /// A check over an opaque value, evaluated segment by segment after the markup delimited it
        /// and before anything is committed: markup delimits, constraint checks (spec 001 research
        /// R8). The opaque readers take one as a generic struct, so the span and sequence paths
        /// share it without a delegate
        /// </summary>
        internal interface IConstraint
        {
            /// <summary>
            /// Evaluate the next chunk of the value after its starting terminal: Unknown to go on,
            /// WellFormed or Malformed once decided
            /// </summary>
            MarkupState Evaluate(ReadOnlySpan<byte> characters);

            /// <summary>
            /// When malformed, the offending byte's index from the value's first byte after its
            /// starting terminal
            /// </summary>
            int OffendingIndex { get; }

            /// <summary>
            /// When malformed, why
            /// </summary>
            Throwing.Category Category { get; }

            /// <summary>
            /// When malformed, what
            /// </summary>
            Throwing.Markup Markup { get; }
        }

        /// <summary>
        /// The constraint of an opaque value with nothing to check, always well-formed
        /// </summary>
        internal struct NoConstraint : IConstraint
        {
            public readonly MarkupState Evaluate(ReadOnlySpan<byte> characters) => MarkupState.WellFormed;

            public readonly int OffendingIndex => 0;

            public readonly Throwing.Category Category => default;

            public readonly Throwing.Markup Markup => default;
        }

        /// <summary>
        /// Scans a PI target (S5 + S6) in a single loop, one character at a time, carrying
        /// its state across the segments of a value sequence
        /// </summary>
        /// <remarks>
        /// S5: the target is a <c>Name</c> (a <c>NameStartChar</c> then zero or more
        /// <c>NameChar</c>) followed by spacing or the ending terminal <c>?&gt;</c>.  S6: the
        /// target does not equal <c>xml</c> in any case, tracked while scanning so no second
        /// pass is needed.  <c>?</c> is never a <c>NameChar</c>, so stopping at the first
        /// <c>?</c> never cuts a well-formed target short.
        /// </remarks>
        internal struct ProcessingInstructionTargetConstraint : IConstraint
        {
            /// <summary>
            /// Characters scanned in the target
            /// </summary>
            private int _length;

            /// <summary>
            /// The target scanned so far equals the start of <c>xml</c> in any case
            /// </summary>
            private bool _isXml;

            /// <summary>
            /// The target ended on <c>?</c> so the next character must be <c>&gt;</c>
            /// </summary>
            private bool _isEndingTerminal;

            /// <summary>
            /// Characters scanned after the starting terminal, across segments
            /// </summary>
            private int _scanned;

            /// <summary>
            /// Index after the starting terminal of the offending character when malformed:
            /// the target's start when it equals <c>xml</c> (S6)
            /// </summary>
            internal int OffendingIndex { get; private set; }

            /// <summary>
            /// Bound on the target in bytes, 0 for none (<c>MaxNameLength</c>, spec 001 US2)
            /// </summary>
            internal int MaxLength { get; init; }

            /// <summary>
            /// Malformed because the target is longer than <see cref="MaxLength"/>, at the first
            /// byte beyond it
            /// </summary>
            internal bool IsTooLong { get; private set; }

            /// <summary>
            /// Limit when the target is too long, otherwise its naming rules (S5 + S6)
            /// </summary>
            public readonly Throwing.Category Category => IsTooLong ? Throwing.Category.Limit : Throwing.Category.Name;

            public readonly Throwing.Markup Markup => Throwing.Markup.ProcessingInstructionTarget;

            readonly int IConstraint.OffendingIndex => OffendingIndex;

            /// <summary>
            /// Evaluate the character terminals after the starting terminal <c>&lt;?</c>
            /// </summary>
            /// <param name="characters"></param>
            /// <returns></returns>
            public MarkupState Evaluate(ReadOnlySpan<byte> characters)
            {
                foreach (byte character in characters)
                {
                    OffendingIndex = _scanned++;

                    if (_isEndingTerminal)
                    {
                        return character == EBNF.GreaterThan ? MarkupState.WellFormed : MarkupState.Malformed;  // S5
                    }

                    if (character is EBNF.Space or EBNF.Tab or EBNF.CarriageReturn or EBNF.LineFeed or EBNF.QuestionMark)
                    {
                        if (_length == 0)
                        {
                            return MarkupState.Malformed;  // S5 empty target
                        }

                        if (_isXml && _length == 3)
                        {
                            OffendingIndex = 0;
                            return MarkupState.Malformed;  // S6
                        }

                        if (character != EBNF.QuestionMark)
                        {
                            return MarkupState.WellFormed;
                        }

                        _isEndingTerminal = true;
                        continue;
                    }

                    if (_length == 0 ? !EBNF.IsNameStartingCharacter(character) : !EBNF.IsNameCharacter(character))
                    {
                        return MarkupState.Malformed;  // S5
                    }

                    if (MaxLength > 0 && _length >= MaxLength)
                    {
                        IsTooLong = true;
                        return MarkupState.Malformed;  // the first target byte beyond MaxNameLength (spec 001 US2)
                    }

                    // ASCII letters only differ by the 0x20 bit in case, and only 'X'/'x',
                    // 'M'/'m' and 'L'/'l' match "xml" with that bit set
                    _isXml = _length < 3
                        && (_length == 0 || _isXml)
                        && (byte)(character | 0x20) == "xml"u8[_length];
                    _length++;
                }

                return MarkupState.Unknown;
            }
        }

        /// <summary>
        /// A document type's internal subset (<c>'[' intSubset ']'</c>) is an opaque inside the
        /// opaque, delimited by its own <c>[</c> and <c>]</c>: not supported until that is built,
        /// at the first <c>[</c> outside quoted literals (spec 001 FR-006, US1-8)
        /// </summary>
        internal struct DocumentTypeConstraint : IConstraint
        {
            private byte _quote;

            private int _scanned;

            public int OffendingIndex { get; private set; }

            public readonly Throwing.Category Category => Throwing.Category.NotSupported;

            public readonly Throwing.Markup Markup => Throwing.Markup.DocumentType;

            public MarkupState Evaluate(ReadOnlySpan<byte> characters)
            {
                foreach (byte character in characters)
                {
                    int index = _scanned++;

                    if (_quote != 0)
                    {
                        if (character == _quote)
                        {
                            _quote = 0;
                        }
                    }
                    else if (character is EBNF.DoubleQuote or EBNF.SingleQuote)
                    {
                        _quote = character;
                    }
                    else if (character == (byte)'[')
                    {
                        OffendingIndex = index;
                        return MarkupState.Malformed;
                    }
                    else if (character == EBNF.GreaterThan)
                    {
                        return MarkupState.WellFormed;
                    }
                }

                return MarkupState.Unknown;
            }
        }

        /// <summary>
        /// The declaration's encoding name must be <c>UTF-8</c>, ignoring ASCII letter case
        /// (spec 001 FR-014, XML 1.0 §4.3.3, research R8): the first <c>encoding</c> preceded by
        /// spacing, then <c>S? '=' S?</c>, a quote and the name up to the matching quote. Another
        /// name is not supported at its first byte, or malformed (Expected) when a skipped UTF-8
        /// BOM contradicts it. Any other shape is left unchecked, as the rest of the declaration.
        /// </summary>
        internal struct DeclarationEncodingConstraint : IConstraint
        {
            private enum State : byte
            {
                Seeking,
                Keyword,
                BeforeEquals,
                AfterEquals,
                Name,
            }

            private static ReadOnlySpan<byte> Keyword => "encoding"u8;

            private static ReadOnlySpan<byte> Utf8Name => "UTF-8"u8;

            private State _state;

            private int _scanned;

            private int _keywordLength;

            private bool _isAfterSpacing;

            private bool _isAfterQuestionMark;

            private byte _quote;

            private int _nameLength;

            /// <summary>
            /// A UTF-8 byte order mark was skipped, so another name contradicts it (FR-014)
            /// </summary>
            public bool HasUtf8ByteOrderMark { get; init; }

            public int OffendingIndex { get; private set; }

            /// <summary>
            /// Expected when a UTF-8 BOM came first (the declaration contradicts it), otherwise not
            /// supported (another encoding)
            /// </summary>
            public readonly Throwing.Category Category => HasUtf8ByteOrderMark ? Throwing.Category.Expected : Throwing.Category.NotSupported;

            public readonly Throwing.Markup Markup => Throwing.Markup.Encoding;

            public MarkupState Evaluate(ReadOnlySpan<byte> characters)
            {
                for (int i = 0; i < characters.Length; i++)
                {
                    byte character = characters[i];
                    int index = _scanned + i;

                    switch (_state)
                    {
                        case State.Name:
                            // One path for a span and for segments: the run up to the quote (or the
                            // chunk's end) is compared with the matching slice of "UTF-8", letters only
                            int quote = characters[i..].IndexOf(_quote);
                            ReadOnlySpan<byte> run = quote < 0 ? characters[i..] : characters[i..(i + quote)];

                            if (_nameLength + run.Length > Utf8Name.Length
                                || !System.Text.Ascii.EqualsIgnoreCase(run, Utf8Name.Slice(_nameLength, run.Length)))
                            {
                                return MarkupState.Malformed;
                            }

                            _nameLength += run.Length;
                            if (quote < 0)
                            {
                                _scanned += characters.Length;
                                return MarkupState.Unknown;
                            }

                            return _nameLength == Utf8Name.Length ? MarkupState.WellFormed : MarkupState.Malformed;

                        case State.Keyword:
                            if (character == Keyword[_keywordLength])
                            {
                                if (++_keywordLength == Keyword.Length)
                                {
                                    _state = State.BeforeEquals;
                                }
                                break;
                            }

                            _state = State.Seeking;  // not the keyword: this byte is seeking again
                            goto case State.Seeking;

                        case State.BeforeEquals:
                            if (IsSpacing(character))
                            {
                                break;
                            }

                            if (character != EBNF.EqualsSign)
                            {
                                return MarkupState.WellFormed;  // another shape, left unchecked
                            }

                            _state = State.AfterEquals;
                            break;

                        case State.AfterEquals:
                            if (IsSpacing(character))
                            {
                                break;
                            }

                            if (character is not (EBNF.DoubleQuote or EBNF.SingleQuote))
                            {
                                return MarkupState.WellFormed;  // another shape, left unchecked
                            }

                            _quote = character;
                            _state = State.Name;
                            OffendingIndex = index + 1;  // the name's first byte
                            break;

                        case State.Seeking:
                            if (_isAfterQuestionMark && character == EBNF.GreaterThan)
                            {
                                return MarkupState.WellFormed;  // '?>' without an encoding
                            }

                            if (_isAfterSpacing && character == Keyword[0])
                            {
                                _state = State.Keyword;
                                _keywordLength = 1;
                            }
                            break;
                    }

                    _isAfterSpacing = IsSpacing(character);
                    _isAfterQuestionMark = character == EBNF.QuestionMark;
                }

                _scanned += characters.Length;
                return MarkupState.Unknown;
            }

            private static bool IsSpacing(byte character) =>
                character is EBNF.Space or EBNF.Tab or EBNF.CarriageReturn or EBNF.LineFeed;
        }

        /// <summary>
        /// Checks one character reference (<c>&amp;#…;</c>, <c>&amp;#x…;</c>) or predefined entity
        /// reference (<c>&amp;amp;</c> <c>&amp;lt;</c> <c>&amp;gt;</c> <c>&amp;quot;</c>
        /// <c>&amp;apos;</c>) from its <c>&amp;</c>, chunk by chunk with its state in the struct
        /// (productions [66] CharRef, [68] EntityRef; WFC Legal Character, WFC Entity Declared).
        /// It only checks, it never unescapes (spec 001 R1).
        /// </summary>
        /// <remarks>
        /// Per chunk, <c>length</c> is the chunk's length when Unknown, the bytes through
        /// <c>;</c> when WellFormed, and the offending byte's index when Malformed, so the
        /// lengths summed over the chunks are the offset from the <c>&amp;</c>. Positions follow
        /// research.md R2: a code point is never above U+10FFFF (checked per digit, so it never
        /// wraps), a Char is checked at <c>;</c>, and an entity name is checked byte by byte
        /// against the five predefined names (<see cref="EBNF.AmpersandEntityName"/> and the
        /// others). After a document type an entity name no predefined name fully matches may be
        /// declared there, which the reader never processes (S2): NotSupportedEntity, reported
        /// at the <c>&amp;</c> (<see cref="ReferenceStart"/>).
        /// </remarks>
        internal struct ReferenceConstraint
        {
            private enum State : byte
            {
                None,
                Ampersand,
                Hash,
                HexMarker,
                Decimal,
                Hex,
                EntityName,
            }

            /// <summary>
            /// Which predefined name is being matched: <c>None</c> until it is chosen
            /// </summary>
            private enum Entity : byte
            {
                None,
                Ampersand,
                LessThan,
                GreaterThan,
                Quote,
                Apostrophe,
                Other,
            }

            private const int MaxCodePoint = 0x10FFFF;

            private State _state;

            private Entity _entity;

            private int _entityLength;

            private int _codePoint;

            /// <summary>
            /// The reader read a document type, so an entity other than the five may be declared
            /// </summary>
            public bool IsDocumentType { get; init; }

            /// <summary>
            /// Bound on an entity name after a document type (spec 001 US2)
            /// </summary>
            public int MaxNameLength { get; init; }

            /// <summary>
            /// At least one complete reference was evaluated
            /// </summary>
            public bool HasReferences { get; private set; }

            /// <summary>
            /// Offset of the reference's <c>&amp;</c> from where this evaluation started, where
            /// a not supported entity is reported
            /// </summary>
            public readonly int ReferenceStart => 0;

            public MarkupState Evaluate(ReadOnlySpan<byte> characters, out int length, out MarkupReason reason)
            {
                reason = MarkupReason.None;

                for (length = 0; length < characters.Length; length++)
                {
                    byte character = characters[length];

                    switch (_state)
                    {
                        case State.None:
                            System.Diagnostics.Debug.Assert(character == EBNF.Ampersand, "A reference starts at its '&'.");
                            _state = State.Ampersand;
                            continue;

                        case State.Ampersand:
                            if (character == (byte)'#')
                            {
                                _state = State.Hash;
                                continue;
                            }

                            if (!EBNF.IsNameStartingCharacter(character))
                            {
                                return Malformed(MarkupReason.MalformedReference, out reason);  // bare '&', '&;'
                            }

                            _state = State.EntityName;
                            _entity = character switch
                            {
                                (byte)'l' => Entity.LessThan,
                                (byte)'g' => Entity.GreaterThan,
                                (byte)'q' => Entity.Quote,
                                (byte)'a' => Entity.None,  // the second byte chooses amp or apos
                                _ => Entity.Other,
                            };
                            _entityLength = 1;

                            if (_entity == Entity.Other && !IsDocumentType)
                            {
                                return Malformed(MarkupReason.UndeclaredEntity, out reason);  // no predefined name starts so
                            }
                            continue;

                        case State.EntityName:
                            if (character == (byte)';')
                            {
                                if (IsComplete())
                                {
                                    return WellFormed(ref length);
                                }

                                return Malformed(IsDocumentType ? MarkupReason.NotSupportedEntity : MarkupReason.UndeclaredEntity, out reason);
                            }

                            if (Continues(character))
                            {
                                _entityLength++;
                                continue;
                            }

                            if (!EBNF.IsNameCharacter(character))
                            {
                                return Malformed(MarkupReason.MalformedReference, out reason);  // missing ';'
                            }

                            if (!IsDocumentType)
                            {
                                return Malformed(MarkupReason.UndeclaredEntity, out reason);  // no predefined name continues so
                            }

                            if (MaxNameLength > 0 && _entityLength >= MaxNameLength)
                            {
                                return Malformed(MarkupReason.TooLong, out reason);  // the first byte beyond MaxNameLength (spec 001 US2)
                            }

                            _entity = Entity.Other;  // may be declared in the document type, scanned as a name to ';'
                            _entityLength++;
                            continue;

                        case State.Hash:
                            if (character == (byte)'x')
                            {
                                _state = State.HexMarker;
                                continue;
                            }

                            if (IsDecimalDigit(character))
                            {
                                _state = State.Decimal;
                                if (!TryAddDigit(10, character - (byte)'0'))
                                {
                                    return Malformed(MarkupReason.MalformedReference, out reason);
                                }
                                continue;
                            }

                            return Malformed(MarkupReason.MalformedReference, out reason);  // '&#;', '&#X', a non-digit

                        case State.HexMarker:
                            if (TryHexDigit(character, out int first))
                            {
                                _state = State.Hex;
                                if (!TryAddDigit(16, first))
                                {
                                    return Malformed(MarkupReason.MalformedReference, out reason);
                                }
                                continue;
                            }

                            return Malformed(MarkupReason.MalformedReference, out reason);  // '&#x;', a non-digit

                        case State.Decimal:
                        case State.Hex:
                            if (character == (byte)';')
                            {
                                if (!IsChar(_codePoint))
                                {
                                    return Malformed(MarkupReason.MalformedReference, out reason);  // WFC: Legal Character
                                }

                                return WellFormed(ref length);
                            }

                            bool isDigit = _state == State.Decimal
                                ? TryDecimalDigit(character, out int digit)
                                : TryHexDigit(character, out digit);

                            if (!isDigit || !TryAddDigit(_state == State.Decimal ? 10 : 16, digit))
                            {
                                return Malformed(MarkupReason.MalformedReference, out reason);  // a non-digit, missing ';', above U+10FFFF
                            }
                            continue;
                    }
                }

                return MarkupState.Unknown;  // the chunk ended inside the reference
            }

            private MarkupState WellFormed(ref int length)
            {
                length++;  // through ';'
                HasReferences = true;
                _state = State.None;
                _entity = Entity.None;
                _entityLength = 0;
                _codePoint = 0;

                return MarkupState.WellFormed;
            }

            private static MarkupState Malformed(MarkupReason why, out MarkupReason reason)
            {
                reason = why;
                return MarkupState.Malformed;
            }

            /// <summary>
            /// The chosen predefined name, or the second byte after <c>a</c> choosing it
            /// </summary>
            private bool Continues(byte character)
            {
                if (_entity == Entity.None && _entityLength == 1)
                {
                    _entity = character switch
                    {
                        (byte)'m' => Entity.Ampersand,
                        (byte)'p' => Entity.Apostrophe,
                        _ => Entity.None,
                    };

                    return _entity != Entity.None;
                }

                ReadOnlySpan<byte> name = Name(_entity);
                return _entityLength < name.Length && name[_entityLength] == character;
            }

            private readonly bool IsComplete() =>
                _entity is not (Entity.None or Entity.Other) && _entityLength == Name(_entity).Length;

            private static ReadOnlySpan<byte> Name(Entity entity) => entity switch
            {
                Entity.Ampersand => EBNF.AmpersandEntityName,
                Entity.LessThan => EBNF.LessThanEntityName,
                Entity.GreaterThan => EBNF.GreaterThanEntityName,
                Entity.Quote => EBNF.QuoteEntityName,
                Entity.Apostrophe => EBNF.ApostropheEntityName,
                _ => [],
            };

            /// <summary>
            /// Leading zeros add nothing; a digit taking the code point above U+10FFFF fails,
            /// so the value never wraps around
            /// </summary>
            private bool TryAddDigit(int radix, int digit)
            {
                int codePoint = _codePoint * radix + digit;
                if (codePoint > MaxCodePoint)
                {
                    return false;
                }

                _codePoint = codePoint;
                return true;
            }

            private static bool IsDecimalDigit(byte character) => character is >= (byte)'0' and <= (byte)'9';

            private static bool TryDecimalDigit(byte character, out int digit)
            {
                digit = IsDecimalDigit(character) ? character - (byte)'0' : -1;
                return digit >= 0;
            }

            private static bool TryHexDigit(byte character, out int digit)
            {
                digit = character switch
                {
                    >= (byte)'0' and <= (byte)'9' => character - (byte)'0',
                    >= (byte)'a' and <= (byte)'f' => character - (byte)'a' + 10,
                    >= (byte)'A' and <= (byte)'F' => character - (byte)'A' + 10,
                    _ => -1,
                };

                return digit >= 0;
            }

            /// <summary>
            /// The Char production: <c>#x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] |
            /// [#x10000-#x10FFFF]</c>
            /// </summary>
            private static bool IsChar(int codePoint) =>
                codePoint is 0x9 or 0xA or 0xD
                    or (>= 0x20 and <= 0xD7FF)
                    or (>= 0xE000 and <= 0xFFFD)
                    or (>= 0x10000 and <= MaxCodePoint);
        }
    }
}
