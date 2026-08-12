namespace Forestry.Deserialize.Xml
{
    internal static partial class Constants
    {
        #region Markup
        public const byte LessThan = (byte)'<';

        public const byte GreaterThan = (byte)'>';

        public const byte Ampersand = (byte)'&';

        public const byte Semicolon = (byte)';';
        #endregion

        #region Delimeters
        public const byte Slash = (byte)'/';

        public const byte Space = (byte)' ';

        public const byte Equal = (byte)'=';

        public const byte Quote = (byte)'"';

        public const byte QuestionMark = (byte)'?';

        public const byte LineFeed = (byte)'\n';

        public const byte CarriageReturn = (byte)'\r';

        public const byte Tab = (byte)'\t';
        #endregion

        #region BOM
        public static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];
        #endregion

        #region Entities
        public static ReadOnlySpan<byte> LessThanValue => "&lt;"u8;

        public static ReadOnlySpan<byte> GreaterThanValue => "&gt;"u8;
        
        public static ReadOnlySpan<byte> AmpersandValue => "&amp;"u8;

        public static ReadOnlySpan<byte> ApostropheValue => "&apos;"u8;

        public static ReadOnlySpan<byte> QuoteValue => "&quot;"u8;
        #endregion

        #region Character Data
        public static ReadOnlySpan<byte> CharacterDataStart => "<![CDATA["u8;

        public static ReadOnlySpan<byte> CharacterDataEnd => "]]>"u8;
        #endregion

        #region Comment
        public static ReadOnlySpan<byte> CommentStart => "<!--"u8;

        public static ReadOnlySpan<byte> CommentEnd => "-->"u8;
        #endregion

        #region Attributes
        public static ReadOnlySpan<byte> NullAttributeName => "xsi:nil"u8;
        #endregion

        #region Declaration + Processing instruction + Document Type
        public static ReadOnlySpan<byte> DeclartionStart => "<?xml "u8;

        public static ReadOnlySpan<byte> ProcessingInstructionStart => "<?"u8;

        public static ReadOnlySpan<byte> DocumentType => "<!DOCTYPE "u8;
        #endregion

        #region Characters
        public const byte Colon = (byte)':';

        public const byte Underscore = (byte)'_';

        public const byte Hyphen = (byte)'-';

        public const byte Period = (byte)'.';
        #endregion

        #region XML characters
        /// <summary>
        /// Starting character for element or attribute names
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsNameStartByte(byte value) =>
            value == Colon ||
            (value >= (byte)'A' && value <= (byte)'Z') ||
            value == Underscore ||
            (value >= (byte)'a' && value <= (byte)'z') ||
            value >= 0x80;

        /// <summary>
        /// Characters after the starting for element or attribute names
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsNameByte(byte value) =>
            IsNameStartByte(value) ||
            value == Hyphen ||
            value == Period ||
            (value >= (byte)'0' && value <= (byte)'9');

        /// <summary>
        /// Characters inside a comment
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsCommentCharByte(byte value) =>
            value == Tab ||
            value == LineFeed ||
            value == CarriageReturn ||
            (value >= 0x20 && value <= 0x7F) ||
            value >= 0x80;
        #endregion
    }
}