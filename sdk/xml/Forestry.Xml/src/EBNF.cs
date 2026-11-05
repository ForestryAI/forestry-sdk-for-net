namespace Forestry.Xml {

    /// <summary>
    /// EBNF XML policies
    /// </summary>
    /// <seealso href="https://en.wikipedia.org/wiki/Extended_Backus%E2%80%93Naur_form"/>
    public static partial class EBNF
    {
        #region Delimeters
        /// <summary>The slash <c>/</c>, in end tags and empty element tags</summary>
        public const byte Slash = (byte)'/';

        /// <summary>The space, one of the four spacing bytes of the <c>S</c> production</summary>
        public const byte Space = (byte)' ';

        /// <summary>The equals sign <c>=</c> of the <c>Eq</c> production</summary>
        public const byte EqualsSign = (byte)'=';

        /// <summary>The double quote <c>"</c>, one of the two literal quotes</summary>
        public const byte DoubleQuote = (byte)'"';

        /// <summary>The question mark <c>?</c>, in declarations and processing instructions</summary>
        public const byte QuestionMark = (byte)'?';

        /// <summary>The line feed, which ends a line; one of the four spacing bytes</summary>
        public const byte LineFeed = (byte)'\n';

        /// <summary>The carriage return, one of the four spacing bytes</summary>
        public const byte CarriageReturn = (byte)'\r';

        /// <summary>The tab, one of the four spacing bytes</summary>
        public const byte Tab = (byte)'\t';

        /// <summary>The colon <c>:</c>, a name starting character</summary>
        public const byte Colon = (byte)':';

        /// <summary>The underscore <c>_</c>, a name starting character</summary>
        public const byte Underscore = (byte)'_';

        /// <summary>The hyphen <c>-</c>, a name character</summary>
        public const byte Hyphen = (byte)'-';

        /// <summary>The period <c>.</c>, a name character</summary>
        public const byte Period = (byte)'.';

        /// <summary>The less than <c>&lt;</c>, which starts markup</summary>
        public const byte LessThan = (byte)'<';

        /// <summary>The greater than <c>&gt;</c>, which ends tags</summary>
        public const byte GreaterThan = (byte)'>';

        /// <summary>The exclamation mark <c>!</c>, in comments and the document type</summary>
        public const byte ExclamationMark = (byte)'!';

        /// <summary>The single quote <c>'</c>, one of the two literal quotes</summary>
        public const byte SingleQuote = (byte)'\'';

        /// <summary>The ampersand <c>&amp;</c>, which starts a reference</summary>
        public const byte Ampersand = (byte)'&';
        #endregion

        #region Terminals
        /// <summary>The declaration's starting terminal <c>&lt;?xml</c></summary>
        public static ReadOnlySpan<byte> DeclarationStartingTerminal => "<?xml"u8;

        /// <summary>The declaration's ending terminal <c>?&gt;</c></summary>
        public static ReadOnlySpan<byte> DeclarationEndingTerminal => "?>"u8;

        /// <summary>The document type's starting terminal <c>&lt;!DOCTYPE</c></summary>
        public static ReadOnlySpan<byte> DocumentTypeStartingTerminal => "<!DOCTYPE"u8;

        /// <summary>The document type's ending terminal <c>&gt;</c></summary>
        public static ReadOnlySpan<byte> DocumentTypeEndingTerminal => ">"u8;

        /// <summary>The comment's starting terminal <c>&lt;!--</c></summary>
        public static ReadOnlySpan<byte> CommentStartingTerminal => "<!--"u8;

        /// <summary>The comment's ending terminal <c>--&gt;</c></summary>
        public static ReadOnlySpan<byte> CommentEndingTerminal => "-->"u8;

        /// <summary>The processing instruction's starting terminal <c>&lt;?</c></summary>
        public static ReadOnlySpan<byte> ProcessingInstructionStartingTerminal => "<?"u8;

        /// <summary>The processing instruction's ending terminal <c>?&gt;</c></summary>
        public static ReadOnlySpan<byte> ProcessingInstructionEndingTerminal => "?>"u8;

        /// <summary>The start tag's starting terminal <c>&lt;</c></summary>
        public static ReadOnlySpan<byte> StartTagStartingTerminal => "<"u8;

        /// <summary>The start tag's ending terminal <c>&gt;</c></summary>
        public static byte StartTagEndingTerminal => (byte)'>';

        /// <summary>The end tag's starting terminal <c>&lt;/</c></summary>
        public static ReadOnlySpan<byte> EndTagStartingTerminal => "</"u8;

        /// <summary>The empty element tag's ending terminal <c>/&gt;</c></summary>
        public static ReadOnlySpan<byte> EmptyElementTagEndingTerminal => "/>"u8;

        /// <summary>
        /// The two starting terminals of an attribute value, a double or a single quote
        /// </summary>
        public readonly ref struct AttributeValueStartingTerminals
        {
            /// <summary>The double quote starting terminal <c>"</c></summary>
            public ReadOnlySpan<byte> DoubleQuote { get; }

            /// <summary>The single quote starting terminal <c>'</c></summary>
            public ReadOnlySpan<byte> SingleQuote { get; }

            /// <summary>
            /// Attribute value starting terminals from a double and a single quote
            /// </summary>
            /// <param name="doubleQuote"></param>
            /// <param name="singleQuote"></param>
            public AttributeValueStartingTerminals(ReadOnlySpan<byte> doubleQuote, ReadOnlySpan<byte> singleQuote)
            {
                DoubleQuote = doubleQuote;
                SingleQuote = singleQuote;
            }
        }

        /// <summary>The attribute value's starting terminals <c>"</c> and <c>'</c></summary>
        public static AttributeValueStartingTerminals AttributeValueStartingTerminal => new("\""u8, "'"u8);

        /// <summary>
        /// Allowed within the POC scope (#17), not the full XML grammar
        /// </summary>
        /// <remarks>
        /// Ordered largest to smallest so the S0 assertion (a longer allowed starting
        /// terminal starts with the scratch pad) usually breaks early. Order affects
        /// only speed, not the result: S1 and S2 always check every terminal.
        /// </remarks>
        internal static readonly byte[][] _allowedStartingTerminals =
        [
            DocumentTypeStartingTerminal.ToArray(),
            DeclarationStartingTerminal.ToArray(),
            CommentStartingTerminal.ToArray(),
            ProcessingInstructionStartingTerminal.ToArray(),
            EndTagStartingTerminal.ToArray(),
            StartTagStartingTerminal.ToArray(),
            AttributeValueStartingTerminal.SingleQuote.ToArray(),
            AttributeValueStartingTerminal.DoubleQuote.ToArray()
        ];

        /// <summary>
        /// Is the character the first character of the starting terminal 
        /// of any allowed non-terminal
        /// </summary>
        /// <param name="character"></param>
        /// <returns></returns>
        internal static bool IsStartingTerminalsFirstCharacter(byte character)
        {
            foreach (byte[] terminal in EBNF._allowedStartingTerminals)
            {
                if (terminal[0] == character)
                {
                    return true;
                }
            }

            return false;
        }
        #endregion

        #region Entities
        /// <summary>The predefined entity name <c>amp</c>, for <c>&amp;</c></summary>
        public static ReadOnlySpan<byte> AmpersandEntityName => "amp"u8;

        /// <summary>The predefined entity name <c>lt</c>, for <c>&lt;</c></summary>
        public static ReadOnlySpan<byte> LessThanEntityName => "lt"u8;

        /// <summary>The predefined entity name <c>gt</c>, for <c>&gt;</c></summary>
        public static ReadOnlySpan<byte> GreaterThanEntityName => "gt"u8;

        /// <summary>The predefined entity name <c>quot</c>, for <c>"</c></summary>
        public static ReadOnlySpan<byte> QuoteEntityName => "quot"u8;

        /// <summary>The predefined entity name <c>apos</c>, for <c>'</c></summary>
        public static ReadOnlySpan<byte> ApostropheEntityName => "apos"u8;
        #endregion

        #region BOM
        /// <summary>The UTF-8 byte order mark <c>EF BB BF</c></summary>
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
        /// <seealso href="https://www.w3.org/TR/xml/#NT-NameStartChar"/>
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
        /// <seealso href="https://www.w3.org/TR/xml/#NT-NameChar"/>
        public static bool IsNameCharacter(byte value) =>
            IsNameStartingCharacter(value) ||
            value == Hyphen ||
            value == Period ||
            (value >= (byte)'0' && value <= (byte)'9');

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsCharacterData(byte value) => false;  // TODO:
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
        /// <seealso href="https://www.w3.org/TR/xml/#NT-Comment"/>
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
            /// <summary>Before the document</summary>
            None = (byte)0,

            /// <summary>The prolog, before the root element</summary>
            Prolog = (byte)1,

            /// <summary>The root element</summary>
            Element = (byte)2,

            /// <summary>Miscellaneous markup after the root element</summary>
            Miscellaneous  = (byte)3,
        }
    }
}