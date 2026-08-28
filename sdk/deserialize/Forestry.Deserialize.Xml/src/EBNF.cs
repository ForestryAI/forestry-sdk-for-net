namespace Forestry.Deserialize.Xml {

    /// <summary>
    /// EBNF XML policies
    /// </summary>
    /// <seealso href="https://en.wikipedia.org/wiki/Extended_Backus%E2%80%93Naur_form"/>
    public static partial class EBNF
    {
        #region Delimeters
        public const byte Slash = (byte)'/';

        public const byte Space = (byte)' ';

        public const byte Equal = (byte)'=';

        public const byte DoubleQuote = (byte)'"';

        public const byte QuestionMark = (byte)'?';

        public const byte LineFeed = (byte)'\n';

        public const byte CarriageReturn = (byte)'\r';

        public const byte Tab = (byte)'\t';

        public const byte Colon = (byte)':';

        public const byte Underscore = (byte)'_';

        public const byte Hyphen = (byte)'-';

        public const byte Period = (byte)'.';
        #endregion

        #region terminal
        public static ReadOnlySpan<byte> StartingElementTerminal => "<"u8;
        #endregion

        #region BOM
        public static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];
        #endregion

        #region Names
        /// <summary>
        /// XML <c>NameStartChar</c> production. <c>":" | [A-Z] | "_" | [a-z]</c> is checked
        /// exactly, byte for byte, against the production. The remaining alternatives -
        /// <c>[#xC0-#xD6] | [#xD8-#xF6] | [#xF8-#x2FF] | [#x370-#x37D] | [#x37F-#x1FFF] |
        /// [#x200C-#x200D] | [#x2070-#x218F] | [#x2C00-#x2FEF] | [#x3001-#xD7FF] |
        /// [#xF900-#xFDCF] | [#xFDF0-#xFFFD] | [#x10000-#xEFFFF]</c> - are Unicode
        /// <em>codepoint</em> ranges, and <c>value &gt;= 0x80</c> below is NOT a byte-level
        /// translation of them: a single UTF-8 byte can't be range-checked against a codepoint
        /// range directly (codepoint <c>#xC0</c> is the two-byte UTF-8 sequence <c>0xC3 0x80</c>,
        /// not the single byte <c>0xC0</c>). <c>value &gt;= 0x80</c> instead accepts any UTF-8
        /// lead or continuation byte unconditionally - a strict over-approximation of the
        /// twelve ranges above, not an implementation of them. Concretely it: admits the two
        /// single-codepoint gaps the production deliberately excludes between its ranges
        /// (<c>#xD7</c>, between <c>#xC0-#xD6</c> and <c>#xD8-#xF6</c>; <c>#xF7</c>, between
        /// <c>#xD8-#xF6</c> and <c>#xF8-#x2FF</c>); never validates that the surrounding bytes
        /// form well-formed UTF-8; and passes byte values that can never legally appear in valid
        /// UTF-8 at all (<c>0xC0</c>/<c>0xC1</c>, <c>0xF5</c>-<c>0xFF</c>). It still does the job
        /// this function is actually called for - checked once per byte as the reader advances,
        /// every byte of a real multi-byte character (lead and continuation both) passes, so
        /// scanning doesn't stop mid-character - it just isn't a codepoint-range check standing
        /// in for the spec's twelve alternatives. Deliberate simplification: real StanForD names
        /// are ASCII-only, and decoding to a codepoint per byte to range-check the upper
        /// alternatives isn't worth it for something no real data exercises. Revisit if strict
        /// conformance against arbitrary (non-ASCII-named) XML ever actually matters.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <see cref="https://www.w3.org/TR/xml/#NT-NameStartChar"/>
        public static bool IsNameStartingCharacter(byte value) =>
            value == Colon ||
            (value >= (byte)'A' && value <= (byte)'Z') ||
            value == Underscore ||
            (value >= (byte)'a' && value <= (byte)'z') ||
            value >= 0x80;

        /// <summary>
        /// XML <c>NameChar</c> production: <see cref="IsNameStartingCharacter"/> plus the exact
        /// ASCII alternatives <c>"-" | "." | [0-9]</c>. The production also adds three more
        /// codepoint ranges beyond <c>NameStartChar</c>'s own (<c>#xB7</c>, <c>#x0300-#x036F</c>,
        /// <c>#x203F-#x2040</c>) - not enumerated separately here because
        /// <see cref="IsNameStartingCharacter"/>'s <c>value &gt;= 0x80</c> already accepts any
        /// byte in that space regardless; see that method's summary for exactly what that
        /// approximation does and doesn't check.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <see cref="https://www.w3.org/TR/xml/#NT-NameChar"/>
        public static bool IsNameCharacter(byte value) =>
            IsNameStartingCharacter(value) ||
            value == Hyphen ||
            value == Period ||
            (value >= (byte)'0' && value <= (byte)'9');
        #endregion

        #region Miscellaneous
        /// <summary>
        /// XML <c>Char</c> production - the base character production, referenced by
        /// <c>Comment</c>'s content (<see href="https://www.w3.org/TR/xml/#sec-comments"/>) among
        /// others. <c>#x9 | #xA | #xD</c> (tab/LF/CR) are checked exactly, and so is the ASCII
        /// slice of <c>[#x20-#xD7FF]</c> (<c>0x20-0x7F</c>; <c>0x7F</c>/DEL is included on
        /// purpose - the production's range technically permits it even though it reads like a
        /// C0 control at a glance). Everything above ASCII uses the same <c>value &gt;= 0x80</c>
        /// approximation as <see cref="IsNameStartingCharacter"/> - not a codepoint-range check
        /// against <c>[#xE000-#xFFFD]</c>/<c>[#x10000-#x10FFFF]</c>, just "any non-ASCII UTF-8
        /// byte is accepted"; see that method's summary for what that does and doesn't guarantee.
        /// Does NOT enforce <c>Comment</c>'s own "the string <c>--</c> must not occur within
        /// comments" constraint - that's sequential (needs to know whether the *previous* byte
        /// was also a hyphen), not something a per-byte classification can express. The caller's
        /// scanning loop has to track that itself.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <see cref="https://www.w3.org/TR/xml/#NT-Comment"/>
        public static bool IsCommentCharacter(byte value) =>
            value == Tab ||
            value == LineFeed ||
            value == CarriageReturn ||
            (value >= 0x20 && value <= 0x7F) ||
            value >= 0x80;
        #endregion

        /// <summary>
        /// document ::= prolog element miscellaneous*
        /// </summary>
        public enum Document: byte
        {
            None = (byte)0,

            Prolog = (byte)1,

            Element = (byte)2,

            Miscellaneous  = (byte)3,
        }
    }
}